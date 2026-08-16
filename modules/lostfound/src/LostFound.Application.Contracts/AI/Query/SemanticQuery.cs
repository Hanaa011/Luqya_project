using System;
using System.Collections.Generic;

namespace LostFound.AI.Query
{
    public enum QueryIntent
    {
        Unknown,
        LostItem,
        FoundItem,
        SearchRequest,
        GeneralQuestion
    }

    public enum EntityType
    {
        Object,
        Color,
        Brand,
        Material,
        Category,
        Location,
        DateTime,
        Quantity
    }

    public sealed record RecognizedEntity(EntityType Type, string Value, string RawText, double Confidence);

    public sealed record SpellCorrection(string Original, string Corrected, double Confidence);

    // A term added to the query by ISemanticExpander - Reason names exactly
    // which Concept field it came from (Synonym/Alias/DialectWord/Misspelling/
    // Related concept name), so the expansion is traceable, not a black box.
    public sealed record ExpandedTerm(string Term, string Reason, double Weight);

    public sealed record QueryDiagnostics(
        long ProcessingTimeMs,
        string? DetectedLanguage,
        IReadOnlyList<SpellCorrection> Corrections,
        IReadOnlyList<RecognizedEntity> Entities,
        IReadOnlyList<ExpandedTerm> ExpandedConcepts);

    // The rich output of IQueryPipeline - everything Phase 2B Part 2's
    // hybrid retrieval engine needs, computed once per query and cacheable
    // (see IQueryCache). FinalSemanticText is the single string meant to be
    // handed to IEmbeddingEngine - the pipeline itself never calls it
    // (spec: "This phase does NOT implement retrieval").
    public sealed record SemanticQuery(
        string RawText,
        string LanguageCode,
        string NormalizedText,
        string CorrectedText,
        IReadOnlyList<string> Tokens,
        IReadOnlyList<string> Lemmas,
        QueryIntent Intent,
        IReadOnlyList<RecognizedEntity> Entities,
        Guid? ResolvedConceptId,
        IReadOnlyList<ExpandedTerm> ExpandedTerms,
        string FinalSemanticText,
        QueryDiagnostics Diagnostics);
}
