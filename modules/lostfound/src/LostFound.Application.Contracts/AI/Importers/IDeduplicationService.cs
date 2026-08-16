using System.Collections.Generic;

namespace LostFound.AI.Importers
{
    public enum DuplicateMatchKind
    {
        Exact,
        Alias,
        Semantic
    }

    // A traceable merge decision - the spec: "Every merge decision should
    // be traceable." MatchKind + a short Reason together are what an
    // administrator (or this report) would need to audit why two records
    // were considered the same real-world object.
    public sealed record DuplicateGroup(
        IReadOnlyList<RawConceptRecord> Records,
        DuplicateMatchKind MatchKind,
        string Reason);

    // Groups raw records (usually from different datasets/languages) that
    // refer to the same real-world concept, WITHOUT deciding how to merge
    // them - that's ICanonicalizer's job. Kept separate so "are these the
    // same thing" (a matching/similarity concern) and "how do we combine
    // them into one Concept" (a data-modeling concern) can evolve
    // independently - e.g. semantic matching upgrading from the text-overlap
    // heuristic below to real embedding similarity once Phase 2A Part 2's
    // local model is installed, with zero change to ICanonicalizer.
    public interface IDeduplicationService
    {
        IReadOnlyList<DuplicateGroup> GroupDuplicates(IReadOnlyList<RawConceptRecord> records);
    }
}
