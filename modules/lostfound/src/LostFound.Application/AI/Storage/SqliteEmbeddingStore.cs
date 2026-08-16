using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Storage
{
    // Also declared as IVectorStore - see StorageAbstractions.cs for why
    // that's a marker interface rather than a second implementation.
    internal sealed class SqliteEmbeddingStore : IEmbeddingStore, IVectorStore
    {
        private readonly EmbeddingSqliteConnectionFactory _connectionFactory;

        public SqliteEmbeddingStore(EmbeddingSqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<float[]?> TryGetAsync(string cacheKey, string modelVersion, CancellationToken cancellationToken = default)
        {
            using var connection = _connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT vector_json FROM embedding_cache WHERE cache_key = $key AND model_version = $version LIMIT 1;";
            command.Parameters.AddWithValue("$key", cacheKey);
            command.Parameters.AddWithValue("$version", modelVersion);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is string json ? JsonSerializer.Deserialize<float[]>(json) : null;
        }

        public async Task SaveAsync(string cacheKey, string modelVersion, float[] embedding, CancellationToken cancellationToken = default)
        {
            using var connection = _connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO embedding_cache (cache_key, model_version, vector_json, created_at_utc)
                VALUES ($key, $version, $vector, $createdAt)
                ON CONFLICT(cache_key, model_version) DO UPDATE SET vector_json = excluded.vector_json;
                """;
            command.Parameters.AddWithValue("$key", cacheKey);
            command.Parameters.AddWithValue("$version", modelVersion);
            command.Parameters.AddWithValue("$vector", JsonSerializer.Serialize(embedding));
            command.Parameters.AddWithValue("$createdAt", System.DateTime.UtcNow.ToString("O"));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task InvalidateModelVersionAsync(string modelVersion, CancellationToken cancellationToken = default)
        {
            using var connection = _connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM embedding_cache WHERE model_version = $version;";
            command.Parameters.AddWithValue("$version", modelVersion);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
