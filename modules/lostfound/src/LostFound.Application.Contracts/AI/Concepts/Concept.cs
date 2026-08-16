using System;
using System.Collections.Generic;

namespace LostFound.AI.Concepts
{
    // A real-world object represented as a semantic concept rather than a
    // keyword - the Part 3 spec's core principle ("Do NOT build a simple
    // dictionary. Do NOT hardcode synonyms."). LocalizedNames/Synonyms/
    // Aliases/DialectWords/CommonMisspellings are all keyed by ISO language
    // code (e.g. "ar", "en", "ur") so new languages plug in as data, not
    // code - see docs/Semantic-AI/Phase-2A/PHASE-2A-PART-3-Semantic-Knowledge-Platform.md
    // ("allow adding new languages without redesign").
    public sealed record Concept
    {
        public required Guid Id { get; init; }

        public required string CanonicalName { get; init; }

        public IReadOnlyDictionary<string, string> LocalizedNames { get; init; } = new Dictionary<string, string>();

        public IReadOnlyDictionary<string, IReadOnlyList<string>> Synonyms { get; init; } =
            new Dictionary<string, IReadOnlyList<string>>();

        public IReadOnlyDictionary<string, IReadOnlyList<string>> Aliases { get; init; } =
            new Dictionary<string, IReadOnlyList<string>>();

        public IReadOnlyDictionary<string, IReadOnlyList<string>> DialectWords { get; init; } =
            new Dictionary<string, IReadOnlyList<string>>();

        public IReadOnlyDictionary<string, IReadOnlyList<string>> CommonMisspellings { get; init; } =
            new Dictionary<string, IReadOnlyList<string>>();

        public IReadOnlyDictionary<string, string> SingularForms { get; init; } = new Dictionary<string, string>();

        public IReadOnlyDictionary<string, string> PluralForms { get; init; } = new Dictionary<string, string>();

        // Structural (IsA/Parent/Child) relationships live in
        // IRelationshipRepository, not duplicated here - these lists are a
        // denormalized read convenience populated by IKnowledgeGraph, not a
        // second source of truth.
        public IReadOnlyList<Guid> ParentConceptIds { get; init; } = Array.Empty<Guid>();
        public IReadOnlyList<Guid> ChildConceptIds { get; init; } = Array.Empty<Guid>();
        public IReadOnlyList<Guid> RelatedConceptIds { get; init; } = Array.Empty<Guid>();

        public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Brands { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Materials { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Colors { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> TypicalLocations { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> TypicalUses { get; init; } = Array.Empty<string>();

        public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

        // Pointer to a vector in the Phase 2A Part 2 embedding store (cache
        // key), not the vector itself - concepts and embeddings are separate
        // concerns with separate lifecycles/versioning.
        public string? EmbeddingReference { get; init; }

        public double PopularityScore { get; init; }
        public double ConfidenceScore { get; init; } = 1.0;

        public int Version { get; init; } = 1;

        public string? SourceDataset { get; init; }
        public DateTime ImportedAtUtc { get; init; } = DateTime.UtcNow;
        public IReadOnlyList<string> LanguageAvailability { get; init; } = Array.Empty<string>();
        public string? EmbeddingVersion { get; init; }

        public bool IsActive { get; init; } = true;
    }
}
