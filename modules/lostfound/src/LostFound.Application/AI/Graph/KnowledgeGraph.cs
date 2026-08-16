using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LostFound.AI.Concepts;
using LostFound.AI.Storage;

namespace LostFound.AI.Graph
{
    // Also declared as IKnowledgeStore - see Storage/StorageAbstractions.cs
    // for why that's a marker interface rather than a second implementation.
    internal sealed class KnowledgeGraph(
        IConceptRepository conceptRepository,
        IRelationshipRepository relationshipRepository) : IKnowledgeGraph, IKnowledgeStore
    {
        public async Task<Concept?> GetConceptAsync(Guid conceptId, CancellationToken cancellationToken = default)
        {
            var concept = await conceptRepository.GetByIdAsync(conceptId, cancellationToken);
            if (concept == null)
            {
                return null;
            }

            // Enrich the denormalized parent/child/related lists from the
            // relationship graph - see Concept.ParentConceptIds's doc
            // comment ("not a second source of truth").
            var outgoing = await relationshipRepository.GetBySourceAsync(conceptId, cancellationToken: cancellationToken);

            return concept with
            {
                ParentConceptIds = outgoing.Where(r => r.RelationshipType == RelationshipType.Parent).Select(r => r.TargetConceptId).ToList(),
                ChildConceptIds = outgoing.Where(r => r.RelationshipType == RelationshipType.Child).Select(r => r.TargetConceptId).ToList(),
                RelatedConceptIds = outgoing.Where(r => r.RelationshipType is RelationshipType.RelatedTo or RelationshipType.SimilarTo)
                    .Select(r => r.TargetConceptId).ToList()
            };
        }

        public async Task<Guid> AddConceptAsync(Concept concept, CancellationToken cancellationToken = default)
        {
            await conceptRepository.UpsertAsync(concept, cancellationToken);
            return concept.Id;
        }

        public Task AddRelationshipAsync(
            Guid sourceConceptId,
            Guid targetConceptId,
            RelationshipType relationshipType,
            double weight = 1.0,
            string? sourceDataset = null,
            CancellationToken cancellationToken = default) =>
            relationshipRepository.AddAsync(
                new ConceptRelationship
                {
                    Id = Guid.NewGuid(),
                    SourceConceptId = sourceConceptId,
                    TargetConceptId = targetConceptId,
                    RelationshipType = relationshipType,
                    Weight = weight,
                    SourceDataset = sourceDataset
                },
                cancellationToken);

        public Task<IReadOnlyList<ConceptRelationship>> GetRelationshipsAsync(
            Guid conceptId, RelationshipType? relationshipType = null, CancellationToken cancellationToken = default) =>
            relationshipRepository.GetBySourceAsync(conceptId, relationshipType, cancellationToken);

        public async Task<IReadOnlyList<Concept>> GetRelatedConceptsAsync(
            Guid conceptId,
            IReadOnlyCollection<RelationshipType>? relationshipTypes = null,
            int maxDepth = 1,
            CancellationToken cancellationToken = default)
        {
            if (maxDepth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "maxDepth must be at least 1.");
            }

            var visited = new HashSet<Guid> { conceptId };
            var frontier = new List<Guid> { conceptId };
            var resultIds = new List<Guid>();

            for (var depth = 0; depth < maxDepth && frontier.Count > 0; depth++)
            {
                var nextFrontier = new List<Guid>();

                foreach (var currentId in frontier)
                {
                    var edges = relationshipTypes == null
                        ? await relationshipRepository.GetBySourceAsync(currentId, cancellationToken: cancellationToken)
                        : (await Task.WhenAll(relationshipTypes.Select(t =>
                            relationshipRepository.GetBySourceAsync(currentId, t, cancellationToken))))
                          .SelectMany(r => r)
                          .ToList();

                    foreach (var edge in edges)
                    {
                        if (visited.Add(edge.TargetConceptId))
                        {
                            resultIds.Add(edge.TargetConceptId);
                            nextFrontier.Add(edge.TargetConceptId);
                        }
                    }
                }

                frontier = nextFrontier;
            }

            var concepts = await Task.WhenAll(resultIds.Select(id => conceptRepository.GetByIdAsync(id, cancellationToken)));
            return concepts.Where(c => c != null).Select(c => c!).ToList();
        }
    }
}
