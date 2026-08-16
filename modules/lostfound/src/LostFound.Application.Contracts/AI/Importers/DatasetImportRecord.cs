using System;

namespace LostFound.AI.Importers
{
    public enum DatasetImportStatus
    {
        Succeeded,
        SucceededWithWarnings,
        Failed,
        Skipped
    }

    // One row of import history - "Every import must generate: Dataset
    // version, Import timestamp, Import source, Build identifier..." (spec).
    // BuildId lets two imports of the identical DatasetVersion be told apart
    // (e.g. a re-run after a canonicalization bug fix, same source data).
    public sealed record DatasetImportRecord(
        Guid Id,
        string DatasetName,
        string DatasetVersion,
        string BuildId,
        DatasetImportStatus Status,
        DateTime ImportedAtUtc,
        int ConceptCount,
        int RelationshipCount,
        int DuplicateGroupCount,
        int ValidationFailureCount,
        long ElapsedMilliseconds,
        string? ErrorMessage);
}
