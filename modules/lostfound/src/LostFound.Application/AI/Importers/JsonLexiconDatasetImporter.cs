using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Graph;

namespace LostFound.AI.Importers
{
    /// <summary>
    /// PHASE-VALIDATION-08. The generic, data-driven replacement for "add
    /// another hardcoded <c>Entry(...)</c> call to LostFoundSeedDataImporter"
    /// - see the Root Cause Analysis section of
    /// PHASE-VALIDATION-08-GEMINI-ENTITY-RECOGNITION-AND-LEXICON.md for why
    /// that approach does not scale. This class contains NO vocabulary of
    /// its own (no concept, brand, or object name is written in this file) -
    /// it is a pure, generic reader: it discovers every <c>*.json</c> file
    /// embedded from <c>AI/Importers/Lexicon/</c> (see the
    /// <c>EmbeddedResource</c> item in LostFound.Application.csproj),
    /// deserializes each into <see cref="LexiconFile"/>, and expands every
    /// <see cref="LexiconConcept"/> into the same per-language
    /// <see cref="RawConceptRecord"/> shape <see cref="LostFoundSeedDataImporter"/>
    /// already produces - from that point on it goes through the exact same
    /// <see cref="IImportCoordinator"/> pipeline (validate, normalize,
    /// dedupe, canonicalize, build relationships, persist) as every other
    /// source. Growing the ontology by a brand, an object type, or a whole
    /// new domain is therefore a DATA change (add/edit a JSON file) with
    /// zero source-code change - the scalability the spec requires.
    ///
    /// This is one of potentially many <see cref="IDatasetImporter"/>
    /// implementations (see also <see cref="WikidataDatasetImporter"/> for a
    /// live external source) - <see cref="LostFoundImportersServiceCollectionExtensions"/>
    /// registers each one as one more line, never a change to this class or
    /// <see cref="IImportCoordinator"/>.
    /// </summary>
    internal sealed class JsonLexiconDatasetImporter : IDatasetImporter
    {
        public string DatasetName => "lostfound-lexicon";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public Task<DatasetSnapshot> FetchAsync(CancellationToken cancellationToken = default)
        {
            var assembly = typeof(JsonLexiconDatasetImporter).Assembly;
            var resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.Contains(".AI.Importers.Lexicon.", StringComparison.Ordinal) &&
                               name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.Ordinal) // deterministic across runs
                .ToList();

            var concepts = new List<RawConceptRecord>();
            var relationships = new List<RawRelationshipRecord>();
            var versionParts = new List<string>();

            foreach (var resourceName in resourceNames)
            {
                var file = ReadLexiconFile(assembly, resourceName);
                if (file == null)
                {
                    continue;
                }

                versionParts.Add($"{file.DatasetName}@{file.DatasetVersion}");
                ExpandConcepts(file, concepts, relationships);
            }

            // The combined version is a deterministic function of every
            // bundled file's own (name, version) pair - editing any one
            // file's content without bumping ITS version would not be
            // detected, exactly like every other dataset here; bumping a
            // file's own datasetVersion is what signals "re-import me" (see
            // IImportCoordinator's ImportMode.Incremental skip check).
            var combinedVersion = versionParts.Count > 0
                ? string.Join(";", versionParts)
                : "empty";

            return Task.FromResult(new DatasetSnapshot(DatasetName, combinedVersion, DateTime.UtcNow, concepts, relationships));
        }

        private static LexiconFile? ReadLexiconFile(Assembly assembly, string resourceName)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return null;
            }

            return JsonSerializer.Deserialize<LexiconFile>(stream, SerializerOptions);
        }

        private static void ExpandConcepts(
            LexiconFile file, List<RawConceptRecord> concepts, List<RawRelationshipRecord> relationships)
        {
            var englishNameByKey = file.Concepts
                .Where(c => c.Names.ContainsKey("en"))
                .ToDictionary(c => c.Key, c => c.Names["en"], StringComparer.OrdinalIgnoreCase);

            foreach (var concept in file.Concepts)
            {
                foreach (var (language, name) in concept.Names)
                {
                    concepts.Add(new RawConceptRecord(
                        SourceDataset: file.DatasetName,
                        SourceId: $"lexicon:{concept.Key}",
                        CanonicalName: name,
                        LanguageCode: language,
                        Synonyms: WordsFor(concept.Synonyms, language),
                        Aliases: WordsFor(concept.Aliases, language),
                        DialectWords: WordsFor(concept.DialectWords, language),
                        CommonMisspellings: WordsFor(concept.CommonMisspellings, language),
                        Categories: string.IsNullOrWhiteSpace(concept.Category)
                            ? Array.Empty<string>()
                            : new[] { concept.Category },
                        ParentNames: concept.ParentKey != null && englishNameByKey.TryGetValue(concept.ParentKey, out var parentName)
                            ? new[] { parentName }
                            : Array.Empty<string>()));
                }

                if (concept.ParentKey != null && englishNameByKey.TryGetValue(concept.ParentKey, out var parent) &&
                    concept.Names.TryGetValue("en", out var childName))
                {
                    relationships.Add(new RawRelationshipRecord(file.DatasetName, childName, parent, RelationshipType.IsA));
                }
            }
        }

        private static IReadOnlyList<string> WordsFor(Dictionary<string, List<string>>? wordsByLanguage, string language) =>
            wordsByLanguage != null && wordsByLanguage.TryGetValue(language, out var words)
                ? words
                : Array.Empty<string>();

        // ---- deserialization models - authoring shape only, never a
        // second source of vocabulary truth beyond the JSON files themselves ----

        private sealed class LexiconFile
        {
            [JsonPropertyName("datasetName")]
            public string DatasetName { get; set; } = "";

            [JsonPropertyName("datasetVersion")]
            public string DatasetVersion { get; set; } = "";

            [JsonPropertyName("concepts")]
            public List<LexiconConcept> Concepts { get; set; } = new();
        }

        private sealed class LexiconConcept
        {
            [JsonPropertyName("key")]
            public string Key { get; set; } = "";

            [JsonPropertyName("category")]
            public string? Category { get; set; }

            [JsonPropertyName("parentKey")]
            public string? ParentKey { get; set; }

            [JsonPropertyName("names")]
            public Dictionary<string, string> Names { get; set; } = new();

            [JsonPropertyName("synonyms")]
            public Dictionary<string, List<string>>? Synonyms { get; set; }

            [JsonPropertyName("aliases")]
            public Dictionary<string, List<string>>? Aliases { get; set; }

            [JsonPropertyName("dialectWords")]
            public Dictionary<string, List<string>>? DialectWords { get; set; }

            [JsonPropertyName("commonMisspellings")]
            public Dictionary<string, List<string>>? CommonMisspellings { get; set; }
        }
    }
}
