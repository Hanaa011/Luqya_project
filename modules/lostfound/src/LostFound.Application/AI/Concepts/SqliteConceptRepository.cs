using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using LostFound.AI.Storage;

namespace LostFound.AI.Concepts
{
    internal sealed class SqliteConceptRepository(KnowledgeSqliteConnectionFactory connectionFactory) : IConceptRepository
    {
        private const string SelectColumns = """
            id, canonical_name, localized_names_json, synonyms_json, aliases_json, dialect_words_json,
            misspellings_json, singular_forms_json, plural_forms_json, categories_json, brands_json,
            materials_json, colors_json, typical_locations_json, typical_uses_json, metadata_json,
            embedding_reference, popularity_score, confidence_score, version, source_dataset,
            imported_at_utc, language_availability_json, embedding_version, is_active
            """;

        public async Task<Concept?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            using var connection = connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM concepts WHERE id = $id LIMIT 1;";
            command.Parameters.AddWithValue("$id", id.ToString());

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? ReadConcept(reader) : null;
        }

        public async Task<Concept?> FindByCanonicalNameAsync(string canonicalName, CancellationToken cancellationToken = default)
        {
            using var connection = connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM concepts WHERE canonical_name = $name AND is_active = 1 LIMIT 1;";
            command.Parameters.AddWithValue("$name", canonicalName);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? ReadConcept(reader) : null;
        }

        public async Task<IReadOnlyList<Concept>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            using var connection = connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM concepts WHERE is_active = 1;";

            var results = new List<Concept>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(ReadConcept(reader));
            }

            return results;
        }

        public async Task UpsertAsync(Concept concept, CancellationToken cancellationToken = default)
        {
            using var connection = connectionFactory.CreateOpenConnection();
            using var transaction = connection.BeginTransaction();

            // Archive whatever is currently live (if anything) before
            // overwriting it - see IConceptRepository.GetHistoryAsync/RollbackAsync.
            using (var archive = connection.CreateCommand())
            {
                archive.Transaction = transaction;
                archive.CommandText = $"""
                    INSERT INTO concept_history (history_id, {SelectColumns})
                    SELECT $historyId, {SelectColumns} FROM concepts WHERE id = $id;
                    """;
                archive.Parameters.AddWithValue("$historyId", Guid.NewGuid().ToString());
                archive.Parameters.AddWithValue("$id", concept.Id.ToString());
                await archive.ExecuteNonQueryAsync(cancellationToken);
            }

            using (var upsert = connection.CreateCommand())
            {
                upsert.Transaction = transaction;
                upsert.CommandText = $"""
                    INSERT INTO concepts ({SelectColumns})
                    VALUES ($id, $canonicalName, $localizedNames, $synonyms, $aliases, $dialectWords, $misspellings,
                            $singularForms, $pluralForms, $categories, $brands, $materials, $colors, $typicalLocations,
                            $typicalUses, $metadata, $embeddingReference, $popularity, $confidence, $version,
                            $sourceDataset, $importedAt, $languageAvailability, $embeddingVersion, $isActive)
                    ON CONFLICT(id) DO UPDATE SET
                        canonical_name = excluded.canonical_name,
                        localized_names_json = excluded.localized_names_json,
                        synonyms_json = excluded.synonyms_json,
                        aliases_json = excluded.aliases_json,
                        dialect_words_json = excluded.dialect_words_json,
                        misspellings_json = excluded.misspellings_json,
                        singular_forms_json = excluded.singular_forms_json,
                        plural_forms_json = excluded.plural_forms_json,
                        categories_json = excluded.categories_json,
                        brands_json = excluded.brands_json,
                        materials_json = excluded.materials_json,
                        colors_json = excluded.colors_json,
                        typical_locations_json = excluded.typical_locations_json,
                        typical_uses_json = excluded.typical_uses_json,
                        metadata_json = excluded.metadata_json,
                        embedding_reference = excluded.embedding_reference,
                        popularity_score = excluded.popularity_score,
                        confidence_score = excluded.confidence_score,
                        version = excluded.version,
                        source_dataset = excluded.source_dataset,
                        imported_at_utc = excluded.imported_at_utc,
                        language_availability_json = excluded.language_availability_json,
                        embedding_version = excluded.embedding_version,
                        is_active = excluded.is_active;
                    """;
                BindConceptParameters(upsert, concept);
                await upsert.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
        }

        public async Task<IReadOnlyList<Concept>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default)
        {
            using var connection = connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM concept_history WHERE id = $id ORDER BY version DESC;";
            command.Parameters.AddWithValue("$id", id.ToString());

            var results = new List<Concept>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(ReadConcept(reader));
            }

            return results;
        }

        public async Task RollbackAsync(Guid id, int version, CancellationToken cancellationToken = default)
        {
            var history = await GetHistoryAsync(id, cancellationToken);
            var target = history.FirstOrDefault(c => c.Version == version)
                ?? throw new InvalidOperationException($"No archived version {version} found for concept '{id}'.");

            // Re-upserting bumps this back to being the live row (and, per
            // UpsertAsync, archives whatever was live immediately before the
            // rollback too - a rollback is itself a recorded history event,
            // never a silent overwrite).
            await UpsertAsync(target with { Version = target.Version + 1 }, cancellationToken);
        }

        public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            using var connection = connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE concepts SET is_active = 0 WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id.ToString());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // ---- mapping helpers -------------------------------------------------

        private static void BindConceptParameters(SqliteCommand command, Concept concept)
        {
            command.Parameters.AddWithValue("$id", concept.Id.ToString());
            command.Parameters.AddWithValue("$canonicalName", concept.CanonicalName);
            command.Parameters.AddWithValue("$localizedNames", JsonSerializer.Serialize(concept.LocalizedNames));
            command.Parameters.AddWithValue("$synonyms", JsonSerializer.Serialize(concept.Synonyms));
            command.Parameters.AddWithValue("$aliases", JsonSerializer.Serialize(concept.Aliases));
            command.Parameters.AddWithValue("$dialectWords", JsonSerializer.Serialize(concept.DialectWords));
            command.Parameters.AddWithValue("$misspellings", JsonSerializer.Serialize(concept.CommonMisspellings));
            command.Parameters.AddWithValue("$singularForms", JsonSerializer.Serialize(concept.SingularForms));
            command.Parameters.AddWithValue("$pluralForms", JsonSerializer.Serialize(concept.PluralForms));
            command.Parameters.AddWithValue("$categories", JsonSerializer.Serialize(concept.Categories));
            command.Parameters.AddWithValue("$brands", JsonSerializer.Serialize(concept.Brands));
            command.Parameters.AddWithValue("$materials", JsonSerializer.Serialize(concept.Materials));
            command.Parameters.AddWithValue("$colors", JsonSerializer.Serialize(concept.Colors));
            command.Parameters.AddWithValue("$typicalLocations", JsonSerializer.Serialize(concept.TypicalLocations));
            command.Parameters.AddWithValue("$typicalUses", JsonSerializer.Serialize(concept.TypicalUses));
            command.Parameters.AddWithValue("$metadata", JsonSerializer.Serialize(concept.Metadata));
            command.Parameters.AddWithValue("$embeddingReference", (object?)concept.EmbeddingReference ?? DBNull.Value);
            command.Parameters.AddWithValue("$popularity", concept.PopularityScore);
            command.Parameters.AddWithValue("$confidence", concept.ConfidenceScore);
            command.Parameters.AddWithValue("$version", concept.Version);
            command.Parameters.AddWithValue("$sourceDataset", (object?)concept.SourceDataset ?? DBNull.Value);
            command.Parameters.AddWithValue("$importedAt", concept.ImportedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$languageAvailability", JsonSerializer.Serialize(concept.LanguageAvailability));
            command.Parameters.AddWithValue("$embeddingVersion", (object?)concept.EmbeddingVersion ?? DBNull.Value);
            command.Parameters.AddWithValue("$isActive", concept.IsActive ? 1 : 0);
        }

        private static Concept ReadConcept(SqliteDataReader reader) => new()
        {
            Id = Guid.Parse(reader.GetString(0)),
            CanonicalName = reader.GetString(1),
            LocalizedNames = ReadDictionary(reader, 2),
            Synonyms = ReadListDictionary(reader, 3),
            Aliases = ReadListDictionary(reader, 4),
            DialectWords = ReadListDictionary(reader, 5),
            CommonMisspellings = ReadListDictionary(reader, 6),
            SingularForms = ReadDictionary(reader, 7),
            PluralForms = ReadDictionary(reader, 8),
            Categories = ReadList(reader, 9),
            Brands = ReadList(reader, 10),
            Materials = ReadList(reader, 11),
            Colors = ReadList(reader, 12),
            TypicalLocations = ReadList(reader, 13),
            TypicalUses = ReadList(reader, 14),
            Metadata = ReadDictionary(reader, 15),
            EmbeddingReference = reader.IsDBNull(16) ? null : reader.GetString(16),
            PopularityScore = reader.GetDouble(17),
            ConfidenceScore = reader.GetDouble(18),
            Version = reader.GetInt32(19),
            SourceDataset = reader.IsDBNull(20) ? null : reader.GetString(20),
            ImportedAtUtc = DateTime.Parse(reader.GetString(21), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            LanguageAvailability = ReadList(reader, 22),
            EmbeddingVersion = reader.IsDBNull(23) ? null : reader.GetString(23),
            IsActive = reader.GetInt32(24) == 1
        };

        private static Dictionary<string, string> ReadDictionary(SqliteDataReader reader, int ordinal) =>
            JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(ordinal)) ?? new();

        private static Dictionary<string, IReadOnlyList<string>> ReadListDictionary(SqliteDataReader reader, int ordinal) =>
            JsonSerializer.Deserialize<Dictionary<string, List<string>>>(reader.GetString(ordinal))
                ?.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value)
            ?? new Dictionary<string, IReadOnlyList<string>>();

        private static List<string> ReadList(SqliteDataReader reader, int ordinal) =>
            JsonSerializer.Deserialize<List<string>>(reader.GetString(ordinal)) ?? new();
    }
}
