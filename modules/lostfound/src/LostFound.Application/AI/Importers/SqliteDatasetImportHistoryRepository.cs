using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Storage;

namespace LostFound.AI.Importers
{
    // Also declared as IMetadataStore - see Storage/StorageAbstractions.cs
    // for why that's a marker interface rather than a second implementation.
    internal sealed class SqliteDatasetImportHistoryRepository(KnowledgeSqliteConnectionFactory connectionFactory) : IDatasetImportHistoryRepository, IMetadataStore
    {
        private const string SelectColumns =
            "id, dataset_name, dataset_version, build_id, status, imported_at_utc, concept_count, " +
            "relationship_count, duplicate_group_count, validation_failure_count, elapsed_ms, error_message";

        public async Task RecordAsync(DatasetImportRecord record, CancellationToken cancellationToken = default)
        {
            using var connection = connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                INSERT INTO dataset_imports ({SelectColumns})
                VALUES ($id, $name, $version, $buildId, $status, $importedAt, $concepts, $relationships,
                        $duplicates, $validationFailures, $elapsedMs, $error);
                """;
            command.Parameters.AddWithValue("$id", record.Id.ToString());
            command.Parameters.AddWithValue("$name", record.DatasetName);
            command.Parameters.AddWithValue("$version", record.DatasetVersion);
            command.Parameters.AddWithValue("$buildId", record.BuildId);
            command.Parameters.AddWithValue("$status", record.Status.ToString());
            command.Parameters.AddWithValue("$importedAt", record.ImportedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$concepts", record.ConceptCount);
            command.Parameters.AddWithValue("$relationships", record.RelationshipCount);
            command.Parameters.AddWithValue("$duplicates", record.DuplicateGroupCount);
            command.Parameters.AddWithValue("$validationFailures", record.ValidationFailureCount);
            command.Parameters.AddWithValue("$elapsedMs", record.ElapsedMilliseconds);
            command.Parameters.AddWithValue("$error", (object?)record.ErrorMessage ?? DBNull.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<DatasetImportRecord?> GetLatestSuccessfulAsync(string datasetName, CancellationToken cancellationToken = default)
        {
            using var connection = connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT {SelectColumns} FROM dataset_imports
                WHERE dataset_name = $name AND status IN ('Succeeded', 'SucceededWithWarnings')
                ORDER BY imported_at_utc DESC LIMIT 1;
                """;
            command.Parameters.AddWithValue("$name", datasetName);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? ReadRecord(reader) : null;
        }

        public async Task<IReadOnlyList<DatasetImportRecord>> GetHistoryAsync(string datasetName, CancellationToken cancellationToken = default)
        {
            using var connection = connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM dataset_imports WHERE dataset_name = $name ORDER BY imported_at_utc DESC;";
            command.Parameters.AddWithValue("$name", datasetName);

            var results = new List<DatasetImportRecord>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(ReadRecord(reader));
            }

            return results;
        }

        private static DatasetImportRecord ReadRecord(Microsoft.Data.Sqlite.SqliteDataReader reader) => new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            Enum.Parse<DatasetImportStatus>(reader.GetString(4)),
            DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetInt32(9),
            reader.GetInt64(10),
            reader.IsDBNull(11) ? null : reader.GetString(11));
    }
}
