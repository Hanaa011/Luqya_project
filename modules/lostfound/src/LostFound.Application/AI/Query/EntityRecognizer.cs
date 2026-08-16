using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LostFound.AI.Concepts;

namespace LostFound.AI.Query
{
    /// <summary>
    /// Rule-based extraction: Object/Color/Brand/Material/Category/Location
    /// come from matching tokens against the knowledge graph's real
    /// vocabulary (Phase 2A Part 3's Concept fields, plus a small curated
    /// list of generic location nouns since the seed dataset doesn't
    /// populate TypicalLocations); DateTime/Quantity come from regex
    /// patterns. Appropriate for a closed, curated domain vocabulary - not
    /// a trained sequence-labeling model.
    /// </summary>
    /// <remarks>
    /// PHASE-VALIDATION-07: two independent, generic mechanisms live here.
    ///
    /// (1) Attribute-role concepts. A concept can represent an object TYPE
    /// ("Charger", tagged Categories=["Electronics"]) or a concept can
    /// itself BE a recognizable value of an attribute dimension ("Samsung",
    /// tagged Categories=["Brand"]; "White", tagged Categories=["Color"]).
    /// <see cref="AttributeRoleCategories"/> is the (small, fixed) map from
    /// the latter kind of category tag to the EntityType it grants - every
    /// concept carrying that tag is indexed under every language it has a
    /// localized name/synonym/alias/dialect word/misspelling for. Adding a
    /// new brand, color, or material to the ontology (a data change in the
    /// importer/seed dataset) is therefore immediately recognizable here
    /// with no change to this class - this is what PHASE-VALIDATION-07
    /// found missing: the seed ontology previously contained zero concepts
    /// carrying a Brand/Color/Material role tag, so
    /// <see cref="EntityType.Brand"/>/<see cref="EntityType.Color"/> could
    /// never be recognized regardless of how correct this matching logic
    /// was.
    ///
    /// (2) Consistent character normalization. Runtime query tokens reach
    /// <see cref="RecognizeAsync"/> already passed through
    /// <see cref="ITextNormalizer"/> (Arabic hamza/alef-maksura/taa-marbuta
    /// collapsing, etc. - see QueryPipeline Stage 2, which runs before
    /// tokenization). The vocabulary built here must fold every indexed
    /// word through the same per-language rules
    /// (<see cref="IConceptNormalizer"/>, the exact service
    /// InMemoryAliasResolver already uses for the same reason) before
    /// adding it to the dictionary - otherwise a correctly-authored
    /// ontology entry like Arabic "أبيض" (white, with hamza) never matches
    /// the normalized runtime token "ابيض" (hamza collapsed to bare alef),
    /// silently failing recognition for any concept whose surface form
    /// contains one of those characters, in any language, for any entity
    /// type - not just newly-added Brand/Color concepts.
    /// </remarks>
    internal sealed class EntityRecognizer(
        IConceptRepository conceptRepository,
        IConceptNormalizer conceptNormalizer,
        ILogger<EntityRecognizer> logger) : IEntityRecognizer
    {
        private static readonly Regex QuantityPattern = new(@"^\d+(\.\d+)?$", RegexOptions.Compiled);

        // Concept.Categories values that mark a concept as itself being a
        // recognizable attribute value, rather than an object-type concept
        // that merely belongs to a taxonomy category. Generic and
        // data-driven: any concept tagged this way in the ontology becomes
        // recognizable automatically (see the class remarks above) - this
        // map only says WHICH EntityType a given role tag grants, never
        // names a specific brand/color/material.
        private static readonly IReadOnlyDictionary<string, EntityType> AttributeRoleCategories =
            new Dictionary<string, EntityType>(StringComparer.OrdinalIgnoreCase)
            {
                ["Brand"] = EntityType.Brand,
                ["Color"] = EntityType.Color,
                ["Material"] = EntityType.Material
            };

        // A small, curated fallback for common generic location nouns -
        // real but limited (no gazetteer of actual place names exists in
        // this workspace).
        private static readonly HashSet<string> GenericLocationWords = new(StringComparer.Ordinal)
        {
            "airport", "mall", "school", "university", "park", "station", "mosque", "market", "hospital",
            "مطار", "مول", "مدرسة", "جامعة", "حديقة", "محطة", "مسجد", "سوق", "مستشفى"
        };

        private readonly SemaphoreSlim _buildLock = new(1, 1);
        private Dictionary<string, EntityType>? _vocabularyIndex;

        // Longest concept name this workspace's seed data actually needs to
        // match as one phrase (e.g. "Android Phone") - kept small so the
        // n-gram scan below stays cheap.
        private const int MaxPhraseLengthInTokens = 3;

        public async Task<IReadOnlyList<RecognizedEntity>> RecognizeAsync(
            IReadOnlyList<string> tokens, string languageCode, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            logger.LogDebug("EntityRecognizer: RecognizeAsync START - {TokenCount} token(s), language '{LanguageCode}'.", tokens.Count, languageCode);

            var vocabulary = await EnsureVocabularyAsync(cancellationToken);
            var entities = new List<RecognizedEntity>();

            // Greedy longest-match: try the longest phrase starting at each
            // position first, so a multi-word concept name ("Android Phone")
            // is recognized as ONE entity instead of fragmenting into
            // single-token matches that can point at an unrelated concept
            // (a bare "phone" token matching the separate "Phone" concept).
            var i = 0;
            while (i < tokens.Count)
            {
                var matchedLength = 0;

                for (var length = Math.Min(MaxPhraseLengthInTokens, tokens.Count - i); length >= 1; length--)
                {
                    var phrase = string.Join(' ', tokens.Skip(i).Take(length));
                    if (vocabulary.TryGetValue(phrase, out var entityType))
                    {
                        entities.Add(new RecognizedEntity(entityType, phrase, phrase, 0.8));
                        logger.LogTrace("EntityRecognizer: matched vocabulary phrase '{Phrase}' -> {EntityType} at position {Position}.", phrase, entityType, i);
                        matchedLength = length;
                        break;
                    }
                }

                if (matchedLength > 0)
                {
                    i += matchedLength;
                    continue;
                }

                var token = tokens[i];

                if (GenericLocationWords.Contains(token))
                {
                    entities.Add(new RecognizedEntity(EntityType.Location, token, token, 0.6));
                    logger.LogTrace("EntityRecognizer: matched generic location word '{Token}' at position {Position}.", token, i);
                    i++;
                    continue;
                }

                if (QuantityPattern.IsMatch(token))
                {
                    // ITextNormalizer (Phase 2B Part 1) strips separators
                    // like '/' and '-' to spaces before tokenization ever
                    // sees them, so a typed date such as "01/01/2026" always
                    // arrives here as separate numeric tokens ["01","01","2026"],
                    // never as one token containing a slash - a per-token
                    // date regex could never match. Detect a date from a
                    // SEQUENCE of small numeric tokens instead.
                    if (TryMatchDateSequence(tokens, i, out var dateText, out var consumed))
                    {
                        entities.Add(new RecognizedEntity(EntityType.DateTime, dateText, dateText, 0.7));
                        logger.LogTrace("EntityRecognizer: matched date sequence '{DateText}' at position {Position} ({Consumed} token(s)).", dateText, i, consumed);
                        i += consumed;
                        continue;
                    }

                    entities.Add(new RecognizedEntity(EntityType.Quantity, token, token, 0.9));
                    logger.LogTrace("EntityRecognizer: matched quantity '{Token}' at position {Position}.", token, i);
                }

                i++;
            }

            logger.LogDebug(
                "EntityRecognizer: RecognizeAsync END - {EntityCount} entity(ies) found [{Entities}] ({ElapsedMs} ms).",
                entities.Count,
                string.Join(", ", entities.Select(e => $"{e.Type}:{e.Value}")),
                stopwatch.ElapsedMilliseconds);

            return entities;
        }

        private static bool TryMatchDateSequence(IReadOnlyList<string> tokens, int startIndex, out string dateText, out int consumed)
        {
            dateText = string.Empty;
            consumed = 0;

            if (startIndex + 1 >= tokens.Count || !IsDayOrMonth(tokens[startIndex]) || !IsDayOrMonth(tokens[startIndex + 1]))
            {
                return false;
            }

            if (startIndex + 2 < tokens.Count && IsYear(tokens[startIndex + 2]))
            {
                dateText = $"{tokens[startIndex]}/{tokens[startIndex + 1]}/{tokens[startIndex + 2]}";
                consumed = 3;
                return true;
            }

            dateText = $"{tokens[startIndex]}/{tokens[startIndex + 1]}";
            consumed = 2;
            return true;
        }

        private static bool IsDayOrMonth(string token) => int.TryParse(token, out var value) && value is >= 1 and <= 31;

        private static bool IsYear(string token) => (token.Length == 2 || token.Length == 4) && int.TryParse(token, out _);

        private async Task<Dictionary<string, EntityType>> EnsureVocabularyAsync(CancellationToken cancellationToken)
        {
            if (_vocabularyIndex != null)
            {
                logger.LogTrace("EntityRecognizer: vocabulary index already built; reusing cached index ({EntryCount} entries).", _vocabularyIndex.Count);
                return _vocabularyIndex;
            }

            logger.LogDebug("EntityRecognizer: EnsureVocabularyAsync START - waiting for build lock.");
            await _buildLock.WaitAsync(cancellationToken);
            try
            {
                if (_vocabularyIndex != null)
                {
                    logger.LogDebug("EntityRecognizer: EnsureVocabularyAsync END - vocabulary was built by another caller while waiting ({EntryCount} entries).", _vocabularyIndex.Count);
                    return _vocabularyIndex;
                }

                var stopwatch = Stopwatch.StartNew();
                logger.LogInformation("EntityRecognizer: vocabulary build START - loading concepts from repository.");

                var index = new Dictionary<string, EntityType>(StringComparer.Ordinal);
                var concepts = await conceptRepository.GetAllAsync(cancellationToken);

                // Flat, denormalized attribute lists (Concept.Colors/Brands/
                // Materials/Categories/TypicalLocations) carry no per-word
                // language, so there is no per-language rule to apply to
                // them - case-folding is the most that can be done here.
                void IndexFlat(IEnumerable<string> words, EntityType type)
                {
                    foreach (var word in words)
                    {
                        var normalized = word.Trim().ToLowerInvariant();
                        if (normalized.Length > 0)
                        {
                            index.TryAdd(normalized, type);
                        }
                    }
                }

                // Every OTHER vocabulary source below is language-tagged, so
                // it must go through the same per-language character
                // normalization runtime query tokens already went through
                // (see the class remarks) before being case-folded.
                void IndexWord(string? word, string languageCode, EntityType type)
                {
                    if (string.IsNullOrWhiteSpace(word))
                    {
                        return;
                    }

                    var normalized = conceptNormalizer.Normalize(word, languageCode).Trim().ToLowerInvariant();
                    if (normalized.Length > 0)
                    {
                        index.TryAdd(normalized, type);
                    }
                }

                void IndexWordsByLanguage(IReadOnlyDictionary<string, IReadOnlyList<string>> wordsByLanguage, EntityType type)
                {
                    foreach (var (languageCode, words) in wordsByLanguage)
                    {
                        foreach (var word in words)
                        {
                            IndexWord(word, languageCode, type);
                        }
                    }
                }

                foreach (var concept in concepts)
                {
                    IndexFlat(concept.Colors, EntityType.Color);
                    IndexFlat(concept.Brands, EntityType.Brand);
                    IndexFlat(concept.Materials, EntityType.Material);
                    IndexFlat(concept.Categories, EntityType.Category);
                    IndexFlat(concept.TypicalLocations, EntityType.Location);

                    // Does this concept itself represent a brand/color/
                    // material value, or is it a plain object-type concept
                    // (Bag, Charger, Camera, ...)? Either way, EVERY lexical
                    // surface form the concept has - canonical name, in every
                    // language, plus every synonym/alias/dialect word/
                    // misspelling - becomes a vocabulary entry for its
                    // EntityType. Before this fix, only the plain-object
                    // branch existed and it indexed LocalizedNames alone,
                    // so a concept's own colloquial aliases (e.g. "Bag"'s
                    // Arabic alias "شنطة", as opposed to its canonical
                    // "حقيبة") were invisible to entity recognition even
                    // though they were already stored, curated ontology
                    // data and already used correctly by concept
                    // resolution (InMemoryAliasResolver indexes the exact
                    // same fields). A query using only the alias therefore
                    // never produced an Object entity for that concept,
                    // and QueryPipeline's resolveText fell through to
                    // whichever OTHER object word the query happened to
                    // contain - see the Hybrid-Semantic-Search-Evaluation
                    // report's root cause analysis. There is no longer a
                    // separate code path for the two cases - only the
                    // target EntityType differs.
                    var attributeType = concept.Categories
                        .Select(category => AttributeRoleCategories.TryGetValue(category, out var type) ? (EntityType?)type : null)
                        .FirstOrDefault(type => type.HasValue);
                    var entityType = attributeType ?? EntityType.Object;

                    foreach (var (languageCode, name) in concept.LocalizedNames)
                    {
                        IndexWord(name, languageCode, entityType);
                    }

                    IndexWordsByLanguage(concept.Synonyms, entityType);
                    IndexWordsByLanguage(concept.Aliases, entityType);
                    IndexWordsByLanguage(concept.DialectWords, entityType);
                    IndexWordsByLanguage(concept.CommonMisspellings, entityType);
                }

                _vocabularyIndex = index;

                logger.LogInformation(
                    "EntityRecognizer: vocabulary build END - {ConceptCount} concept(s) indexed into {EntryCount} vocabulary entries ({ElapsedMs} ms).",
                    concepts.Count, index.Count, stopwatch.ElapsedMilliseconds);

                return index;
            }
            finally
            {
                _buildLock.Release();
            }
        }
    }
}