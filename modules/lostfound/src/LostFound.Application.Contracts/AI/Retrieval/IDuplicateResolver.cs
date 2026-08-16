using System;
using System.Collections.Generic;

namespace LostFound.AI.Retrieval
{
    // "Duplicate Resolution" stage. Report ID is already unique after
    // ICandidateMerger (that's the merge key), so the real work here is
    // SEMANTIC duplicate collapsing: two distinct reports whose embeddings
    // are near-identical (very likely the same real-world item reported
    // twice) get merged into one candidate, keeping the higher-scored
    // report's identity and combining both candidates' contributions -
    // "Preserve provenance from all retrieval sources" (spec). Concept-ID-
    // based dedup is not implemented: Report has no Concept linkage yet
    // (that's Phase 2B Part 4's integration territory), so there is nothing
    // real to dedup on for that dimension today.
    public interface IDuplicateResolver
    {
        IReadOnlyList<RetrievedCandidate> Resolve(
            IReadOnlyList<RetrievedCandidate> candidates, IReadOnlyDictionary<Guid, SearchableReport> reportsById);
    }
}
