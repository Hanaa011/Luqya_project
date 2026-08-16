using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using LostFound.AI.Configuration;

namespace LostFound.AI.Storage
{
    // Separate database file ("knowledge.db" by default) from Part 2's
    // embeddings.db - concepts/relationships and cached embedding vectors
    // have entirely different lifecycles (the knowledge graph is
    // imported/curated data; the embedding cache is disposable and rebuilt
    // from source on demand), so sharing one file/schema would couple two
    // unrelated concerns for no benefit.
    internal sealed class KnowledgeSqliteConnectionFactory
    {
        private readonly string _connectionString;

        public KnowledgeSqliteConnectionFactory(IOptions<KnowledgeGraphOptions> options)
        {
            var databasePath = options.Value.DatabasePath;
            var directory = Path.GetDirectoryName(databasePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _connectionString = $"Data Source={databasePath}";

            EnsureSchema();
        }

        public SqliteConnection CreateOpenConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private void EnsureSchema()
        {
            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();

            // concepts/concept_history share the exact same column set (see
            // SqliteConceptRepository's shared row-mapping) so a version can
            // be archived and later restored without any field-by-field
            // translation.
            const string conceptColumns = """
                id TEXT NOT NULL,
                canonical_name TEXT NOT NULL,
                localized_names_json TEXT NOT NULL,
                synonyms_json TEXT NOT NULL,
                aliases_json TEXT NOT NULL,
                dialect_words_json TEXT NOT NULL,
                misspellings_json TEXT NOT NULL,
                singular_forms_json TEXT NOT NULL,
                plural_forms_json TEXT NOT NULL,
                categories_json TEXT NOT NULL,
                brands_json TEXT NOT NULL,
                materials_json TEXT NOT NULL,
                colors_json TEXT NOT NULL,
                typical_locations_json TEXT NOT NULL,
                typical_uses_json TEXT NOT NULL,
                metadata_json TEXT NOT NULL,
                embedding_reference TEXT NULL,
                popularity_score REAL NOT NULL DEFAULT 0,
                confidence_score REAL NOT NULL DEFAULT 1,
                version INTEGER NOT NULL DEFAULT 1,
                source_dataset TEXT NULL,
                imported_at_utc TEXT NOT NULL,
                language_availability_json TEXT NOT NULL,
                embedding_version TEXT NULL,
                is_active INTEGER NOT NULL DEFAULT 1
                """;

            command.CommandText = $"""
                CREATE TABLE IF NOT EXISTS concepts (
                    {conceptColumns},
                    PRIMARY KEY (id)
                );

                CREATE TABLE IF NOT EXISTS concept_history (
                    history_id TEXT NOT NULL PRIMARY KEY,
                    {conceptColumns}
                );
                CREATE INDEX IF NOT EXISTS ix_concept_history_concept_id ON concept_history(id);

                CREATE TABLE IF NOT EXISTS concept_relationships (
                    id TEXT NOT NULL PRIMARY KEY,
                    source_concept_id TEXT NOT NULL,
                    target_concept_id TEXT NOT NULL,
                    relationship_type TEXT NOT NULL,
                    weight REAL NOT NULL DEFAULT 1,
                    version INTEGER NOT NULL DEFAULT 1,
                    source_dataset TEXT NULL,
                    created_at_utc TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_concept_relationships_source ON concept_relationships(source_concept_id, relationship_type);
                CREATE INDEX IF NOT EXISTS ix_concept_relationships_target ON concept_relationships(target_concept_id, relationship_type);

                CREATE TABLE IF NOT EXISTS dataset_imports (
                    id TEXT NOT NULL PRIMARY KEY,
                    dataset_name TEXT NOT NULL,
                    dataset_version TEXT NOT NULL,
                    build_id TEXT NOT NULL,
                    status TEXT NOT NULL,
                    imported_at_utc TEXT NOT NULL,
                    concept_count INTEGER NOT NULL,
                    relationship_count INTEGER NOT NULL,
                    duplicate_group_count INTEGER NOT NULL,
                    validation_failure_count INTEGER NOT NULL,
                    elapsed_ms INTEGER NOT NULL,
                    error_message TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_dataset_imports_name ON dataset_imports(dataset_name, imported_at_utc);
                """;
            command.ExecuteNonQuery();
        }
    }
}
