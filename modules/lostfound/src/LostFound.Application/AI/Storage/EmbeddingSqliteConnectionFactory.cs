using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using LostFound.AI.Configuration;

namespace LostFound.AI.Storage
{
    // Single shared SQLite database ("embeddings.db" by default) for both
    // model version history (EmbeddingModelManager) and the persistent
    // embedding cache (SqliteEmbeddingStore) - one file, one schema owner,
    // per the Part 2 spec's "Storage must be abstracted behind interfaces"
    // requirement. Registered as a singleton so schema creation runs once.
    internal sealed class EmbeddingSqliteConnectionFactory
    {
        private readonly string _connectionString;

        public EmbeddingSqliteConnectionFactory(IOptions<LocalAiRuntimeOptions> options)
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
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS embedding_models (
                    name TEXT NOT NULL,
                    version TEXT NOT NULL,
                    status TEXT NOT NULL,
                    installed_at_utc TEXT NULL,
                    install_path TEXT NULL,
                    sha256_checksum TEXT NULL,
                    is_active INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (name, version)
                );

                CREATE TABLE IF NOT EXISTS embedding_cache (
                    cache_key TEXT NOT NULL,
                    model_version TEXT NOT NULL,
                    vector_json TEXT NOT NULL,
                    created_at_utc TEXT NOT NULL,
                    PRIMARY KEY (cache_key, model_version)
                );
                """;
            command.ExecuteNonQuery();
        }
    }
}
