using System;

namespace LostFound.AI.Graph
{
    public enum RelationshipType
    {
        IsA,
        PartOf,
        RelatedTo,
        SimilarTo,
        BrandOf,
        CategoryOf,
        Parent,
        Child
    }

    public sealed record ConceptRelationship
    {
        public required Guid Id { get; init; }
        public required Guid SourceConceptId { get; init; }
        public required Guid TargetConceptId { get; init; }
        public required RelationshipType RelationshipType { get; init; }

        // Strength/confidence of this edge, 0-1 - lets ranking (Phase 2B)
        // weight a strong IsA edge (e.g. Smartphone IsA Phone) differently
        // from a weak RelatedTo one, without a separate scoring table.
        public double Weight { get; init; } = 1.0;

        public int Version { get; init; } = 1;
        public string? SourceDataset { get; init; }
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    }
}
