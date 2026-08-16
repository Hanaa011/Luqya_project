using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Importers
{
    // Everything one dataset source produces after Download + Integrity
    // Validation + Parsing - the three pipeline stages the spec says "each
    // importer must implement only its own parsing logic" for. Every later
    // stage (schema validation onward) is IImportCoordinator's job, common
    // to all sources.
    public sealed record DatasetSnapshot(
        string DatasetName,
        string DatasetVersion,
        DateTime FetchedAtUtc,
        IReadOnlyList<RawConceptRecord> Concepts,
        IReadOnlyList<RawRelationshipRecord> Relationships);

    // One adapter per knowledge source (ConceptNet, Wikidata, a curated
    // seed set, ...). Implementations own ONLY source-specific concerns:
    // where to fetch from, how to parse that source's format, and what
    // DatasetVersion means for that source (a dump date, an API ETag, a
    // manually-bumped constant for a static fixture, etc.).
    public interface IDatasetImporter
    {
        string DatasetName { get; }

        Task<DatasetSnapshot> FetchAsync(CancellationToken cancellationToken = default);
    }
}
