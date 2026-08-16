using System;
using System.Collections.Generic;
using LostFound.Reports;

namespace LostFound.AI.Retrieval
{
    // Contracts-safe projection of the Domain-layer LostFound.Reports.Report -
    // retrieval strategies operate on this instead of the entity directly,
    // so this subsystem's interfaces never need a Domain-layer reference
    // (LostFound.Application.Contracts references Domain.Shared only, the
    // same boundary IEmbeddingProvider/IAiMatchingService already respect).
    // ReportType itself lives in Domain.Shared, so it's safe to reference
    // directly rather than duplicating it as a string code.
    public sealed record SearchableReport(
        Guid ReportId,
        ReportType Type,
        string? Description,
        string? LocationDetails,
        DateTime? LostFoundDate,
        string? ObjectType,
        string? Color,
        string? Brand,
        IReadOnlyList<string> Tags,
        Guid? CategoryId,
        // Resolved once, up front, by whatever builds the SearchableReport
        // projection (IHybridSearchEngine's implementation) - not by each
        // retrieval strategy individually. Report itself only stores
        // CategoryId; keeping name resolution out of CategoryRetriever
        // means that strategy stays a pure, dependency-free class ("every
        // strategy must be independently testable" - no repository/DB
        // mocking needed to unit test it).
        string? CategoryName,
        float[]? EmbeddingVector,
        float[]? ImageEmbeddingVector,
        // The second, independent embedding of AI-classified attributes
        // only (ObjectType/Color/Brand/Category/Tags) - see
        // Report.MetadataEmbeddingJson's remarks. Null for a report with no
        // classified metadata yet, exactly like EmbeddingVector/
        // ImageEmbeddingVector already model absence.
        float[]? MetadataEmbeddingVector);
}
