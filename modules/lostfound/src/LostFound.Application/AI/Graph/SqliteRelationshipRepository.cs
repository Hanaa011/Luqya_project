using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using LostFound.AI.Storage;

namespace LostFound.AI.Graph
{
    internal sealed class SqliteRelationshipRepository(KnowledgeSqliteConnectionFactory connectionFactory) : IRelationshipRepository
    {
        private const string SelectColumns =
            "id, source_concept_id, target_concept_id, relationship_type, weight, version, source_dataset, created_at_utc";

        public async Task AddAsync(ConceptRelationship relationship, CancellationToken cancellationToken = default)
        {
            using var connection = connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                INSERT INTO concept_relationships ({SelectColumns})
                VALUES ($id, $source, $target, $type, $weight, $version, $sourceDataset, $createdAt);
                """;
            command.Parameters.AddWithValue("$id", relationship.Id.ToString());
            command.Parameters.AddWithValue("$source", relationship.SourceConceptId.ToString());
            command.Parameters.AddWithValue("$target", relationship.TargetConceptId.ToString());
            command.Parameters.AddWithValue("$type", relationship.RelationshipType.ToString());
            command.Parameters.AddWithValue("$weight", relationship.Weight);
            command.Parameters.AddWithValue("$version", relationship.Version);
            command.Parameters.AddWithValue("$sourceDataset", (object?)relationship.SourceDataset ?? DBNull.Value);
            command.Parameters.AddWithValue("$createdAt", relationship.CreatedAtUtc.ToString("O"));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public Task<IReadOnlyList<ConceptRelationship>> GetBySourceAsync(
            Guid sourceConceptId, RelationshipType? relationshipType = null, CancellationToken cancellationToken = default) =>
            QueryAsync("source_concept_id", sourceConceptId, relationshipType, cancellationToken);

        public Task<IReadOnlyList<ConceptRelationship>> GetByTargetAsync(
            Guid targetConceptId, RelationshipType? relationshipType = null, CancellationToken cancellationToken = default) =>
            QueryAsync("target_concept_id", targetConceptId, relationshipType, cancellationToken);

        public async Task RemoveAsync(Guid relationshipId, CancellationToken cancellationToken = default)
        {
            using var connection = connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM concept_relationships WHERE id = $id;";
            command.Parameters.AddWithValue("$id", relationshipId.ToString());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<IReadOnlyList<ConceptRelationship>> QueryAsync(
            string column, Guid conceptId, RelationshipType? relationshipType, CancellationToken cancellationToken)
        {
            using var connection = connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();

            command.CommandText = relationshipType == null
                ? $"SELECT {SelectColumns} FROM concept_relationships WHERE {column} = $conceptId;"
                : $"SELECT {SelectColumns} FROM concept_relationships WHERE {column} = $conceptId AND relationship_type = $type;";

            command.Parameters.AddWithValue("$conceptId", conceptId.ToString());
            if (relationshipType != null)
            {
                command.Parameters.AddWithValue("$type", relationshipType.Value.ToString());
            }

            var results = new List<ConceptRelationship>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new ConceptRelationship
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    SourceConceptId = Guid.Parse(reader.GetString(1)),
                    TargetConceptId = Guid.Parse(reader.GetString(2)),
                    RelationshipType = Enum.Parse<RelationshipType>(reader.GetString(3)),
                    Weight = reader.GetDouble(4),
                    Version = reader.GetInt32(5),
                    SourceDataset = reader.IsDBNull(6) ? null : reader.GetString(6),
                    CreatedAtUtc = DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                });
            }

            return results;
        }
    }
}
