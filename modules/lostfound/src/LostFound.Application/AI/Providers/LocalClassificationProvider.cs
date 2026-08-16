using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LostFound.AI.Concepts;
using LostFound.AI.Core;
using LostFound.AI.Query;

namespace LostFound.AI.Providers
{
    /// <summary>
    /// PHASE-VALIDATION-06 Local-First classification fallback. Classifies an
    /// item entirely offline, reusing infrastructure that already exists
    /// rather than introducing a new pipeline or a new inference engine: the
    /// existing local query pipeline (<see cref="IQueryPipeline"/> - language
    /// detection, normalization, tokenization, entity recognition against
    /// the knowledge graph's real vocabulary, and concept resolution -
    /// Phase 2A Part 3 / Phase 2B Part 1) supplies Color/Brand/Material/
    /// Category/Object entities and, when it can resolve one, a concept.
    ///
    /// Real E2E finding (AI-Classification-Ontology-Vocabulary-Architecture
    /// investigation): this class used to run its OWN, second, private
    /// semantic-similarity concept-matching tier here (embed the description,
    /// compare by cosine similarity against every concept's canonical-name
    /// embedding) whenever the query pipeline's own concept resolution came
    /// up empty. That tier has been removed - not because semantic matching
    /// stopped being valuable, but because <see cref="IConceptResolver"/>
    /// (which <see cref="IQueryPipeline"/> already calls internally, Stage 9)
    /// now does exact-then-semantic resolution itself, as the single shared
    /// implementation every consumer - this class, QueryPipeline/Search,
    /// RankingEngine's metadata scoring - depends on. Duplicating the same
    /// technique here a second time would let the local classification
    /// pipeline and the ranking pipeline silently drift out of agreement
    /// with each other over time (e.g. a different similarity threshold, or
    /// a fix applied to only one copy); a single implementation cannot drift
    /// from itself.
    ///
    /// Deliberately does not attempt local image classification: no local
    /// vision/captioning model exists in this workspace, and adding one
    /// would be a new external dependency the spec asks to avoid. A
    /// description-less (image-only) report degrades to an empty result
    /// here, same as the remote-provider failure path did before this
    /// fallback existed - classification remains an enrichment, never a
    /// hard requirement (see LostFound.AI.Core.ClassificationEngine).
    /// </summary>
    internal sealed class LocalClassificationProvider(
        IQueryPipeline queryPipeline,
        IConceptRepository conceptRepository,
        ILogger<LocalClassificationProvider> logger) : ILocalClassificationProvider
    {
        private const int MaxTags = 8;

        public string ProviderName => "Local";

        public async Task<ItemClassificationResult> ClassifyAsync(
            string? description, byte[]? imageBytes, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                logger.LogInformation(
                    "LocalClassificationProvider: no description text available (image-only report); local " +
                    "classification requires text, so this item continues without AI classification.");
                return new ItemClassificationResult();
            }

            // The existing local query pipeline already does language
            // detection, normalization, tokenization, entity recognition,
            // and concept resolution (exact alias/synonym match, then a
            // semantic-similarity fallback - see IConceptResolver) in one call.
            var query = await queryPipeline.ProcessAsync(description, cancellationToken);

            Concept? resolvedConcept = query.ResolvedConceptId.HasValue
                ? await conceptRepository.GetByIdAsync(query.ResolvedConceptId.Value, cancellationToken)
                : null;

            // Guard against a real, observed failure mode (PHASE-VALIDATION-06
            // Test 2 evidence): IEntityRecognizer runs against CorrectedTokens
            // (post spell-correction), and ISpellCorrectionService's small
            // seed-ontology dictionary can "correct" a genuinely unrelated
            // word into a nearby vocabulary term by edit distance (an input
            // containing neither "pet" nor "bag" nor "watch" still produced
            // Object entities for all three, ahead of the one genuine match,
            // "keys"). QueryPipeline's own concept resolution (Stage 9)
            // always resolves from whichever Object entity it found FIRST,
            // untrusted, so a spell-correction artifact ahead of the genuine
            // entity propagates into a wrong concept.
            //
            // The check must compare the entity QueryPipeline actually used
            // against the query's own NormalizedText - NOT against
            // resolvedConcept.CanonicalName, which is always English
            // regardless of query language (an earlier version of this guard
            // did that and incorrectly discarded genuine Arabic/Urdu
            // resolutions, e.g. "هاتف" -> Phone, because "هاتف" != "Phone").
            // When the first Object entity is itself genuine (present in
            // NormalizedText, i.e. actually typed - true for the vast
            // majority of single- or non-English-language queries, which
            // ISpellCorrectionService does not corrupt the same way), trust
            // the resolution regardless of what language it's in. Only
            // discard it when that first entity is a fabricated
            // spell-correction substitution - a null resolvedConcept is
            // handled gracefully throughout BuildResult below (the same
            // "honest no-signal beats a confidently wrong guess" principle
            // already established for this class - see the FirstMeaningfulToken
            // removal note there). This is a local, read-only safeguard: it
            // does not change IQueryPipeline/ISpellCorrectionService/
            // IEntityRecognizer themselves, which the live search path also
            // depends on.
            var firstObjectEntity = query.Entities.FirstOrDefault(e => e.Type == EntityType.Object);
            if (resolvedConcept != null && firstObjectEntity != null &&
                !query.NormalizedText.Contains(firstObjectEntity.Value, StringComparison.OrdinalIgnoreCase))
            {
                resolvedConcept = null;
            }

            var result = BuildResult(description, query, resolvedConcept);

            logger.LogInformation(
                "LocalClassificationProvider: classified locally via [{Source}]. " +
                "Category={Category}, ObjectType={ObjectType}, Color={Color}, Brand={Brand}, Tags=[{Tags}].",
                resolvedConcept != null ? "concept-resolution" : "none",
                result.CategoryName, result.ObjectType, result.Color, result.Brand, string.Join(", ", result.Tags));

            return result;
        }

        private static ItemClassificationResult BuildResult(
            string description, SemanticQuery query, Concept? resolvedConcept)
        {
            var objectEntity = GetGenuineEntity(query, EntityType.Object);
            var colorEntity = GetGenuineEntity(query, EntityType.Color);
            var brandEntity = GetGenuineEntity(query, EntityType.Brand);
            var materialEntity = GetGenuineEntity(query, EntityType.Material);
            var categoryEntity = GetGenuineEntity(query, EntityType.Category);

            // Real E2E finding (Search<->Matching Ranking Unification
            // validation): this used to fall back a THIRD time, to
            // FirstMeaningfulToken - "the longest remaining token in the
            // sentence" - whenever neither concept resolution nor entity
            // recognition found anything. Reviewing every real production
            // occurrence of that fallback firing (grep for "classified
            // locally via [none]" across a full 62-report Arabic dataset run)
            // showed a 100% failure rate: it never once produced a genuine
            // object name, only prepositions ("بالقرب" - "nearby"), location
            // nouns ("المكتبه" - "the library", for an actual bicycle
            // report), adjectives ("سوداء" - "black"), and possessive phrases
            // ("لونها" - "its color") - because word LENGTH has no
            // relationship to grammatical role in Arabic. Each of those wrong
            // guesses was then persisted as Report.AiObjectType with full
            // confidence and fed into every downstream object-type-match
            // bonus/penalty and into the embedding text, actively steering
            // matching toward the wrong cluster of candidates (e.g. a
            // misclassified bank card became a "بالقرب" query that matched
            // nothing, instead of a "Bank Card" query that would have found
            // its real pair). A null ObjectType is already handled safely
            // everywhere downstream (RankingEngine and AiMatchingService both
            // treat a missing object type as neutral/unknown, never as a
            // penalty), so omitting it here trades a confidently-wrong guess
            // for an honest "no signal" - strictly safer given the evidence.
            var objectType = resolvedConcept?.CanonicalName ?? objectEntity?.Value;

            var categoryName = categoryEntity?.Value
                ?? resolvedConcept?.Categories.FirstOrDefault();

            var color = colorEntity?.Value
                ?? resolvedConcept?.Colors.FirstOrDefault();

            var brand = brandEntity?.Value
                ?? resolvedConcept?.Brands.FirstOrDefault();

            var tags = new List<string>();
            void AddTag(string? tag)
            {
                if (!string.IsNullOrWhiteSpace(tag) && tags.Count < MaxTags &&
                    !tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    tags.Add(tag.Trim());
                }
            }

            AddTag(objectType);
            AddTag(categoryName);
            AddTag(color);
            AddTag(brand);
            AddTag(materialEntity?.Value);
            foreach (var category in resolvedConcept?.Categories ?? Array.Empty<string>())
            {
                AddTag(category);
            }
            foreach (var expanded in query.ExpandedTerms.Take(MaxTags))
            {
                AddTag(expanded.Term);
            }

            // Model/serial-style tokens (e.g. "s24", "a54") never appear in
            // the ontology as concepts - there is no bounded, sensible list
            // of every product model, and treating them as concepts would
            // be a product catalog, not domain knowledge. They are still
            // valuable tags, so fall back to the same "distinctive token"
            // heuristic AiMatchingService already uses for exact-model-
            // mention scoring: any token containing a digit is specific
            // enough to be worth keeping, regardless of which object/brand
            // it came from - fully generic, not tied to any one model.
            foreach (var token in query.Tokens)
            {
                if (tags.Count >= MaxTags)
                {
                    break;
                }

                if (token.Length >= 2 && token.Any(char.IsDigit))
                {
                    AddTag(token);
                }
            }

            var searchText = !string.IsNullOrWhiteSpace(query.FinalSemanticText)
                ? query.FinalSemanticText
                : description.Trim();

            var explanation = resolvedConcept != null
                ? $"Classified locally (Local-First fallback) via concept '{resolvedConcept.CanonicalName}'."
                : "Classified locally (Local-First fallback) via entity recognition; no concept could be resolved.";

            return new ItemClassificationResult
            {
                CategoryName = categoryName,
                ObjectType = objectType,
                Color = color,
                Brand = brand,
                Tags = tags,
                SearchText = searchText,
                Explanation = explanation,
                SearchReason = "local-classification-fallback",
                ImageCaption = null
            };
        }

        // Only trusts a recognized entity whose value is actually present in
        // the query's own NormalizedText (the pre-spell-correction text) -
        // see the PHASE-VALIDATION-06 remarks on ClassifyAsync for why this
        // guard exists (spell-correction can otherwise hallucinate a
        // vocabulary word that was never in the input).
        private static RecognizedEntity? GetGenuineEntity(SemanticQuery query, EntityType type) =>
            query.Entities.FirstOrDefault(e =>
                e.Type == type && query.NormalizedText.Contains(e.Value, StringComparison.OrdinalIgnoreCase));
    }
}
