using System.Collections.Generic;

namespace LostFound.AI.Importers
{
    // What an IDatasetImporter produces BEFORE the pipeline's validation/
    // normalization/dedup/canonicalization stages run - deliberately not a
    // Concept yet, since one real-world object may arrive as several raw
    // records (different datasets, different languages) that only become
    // one Concept after ICanonicalizer merges them - see the spec's
    // "شنطة/شنطه/حقيبة/Bag/Backpack/Handbag" example.
    public sealed record RawConceptRecord(
        string SourceDataset,
        string SourceId,
        string CanonicalName,
        string LanguageCode,
        IReadOnlyList<string> Synonyms,
        IReadOnlyList<string> Aliases,
        // PHASE-VALIDATION-08: previously structurally unavailable anywhere
        // in the import pipeline - every importer, including the original
        // seed one, could only ever produce Synonyms/Aliases, even though
        // Concept.DialectWords/CommonMisspellings existed since Phase 2A
        // Part 3 and EntityRecognizer/InMemoryAliasResolver already index
        // them correctly. Added so lexical richness (dialect variants,
        // common misspellings/typing mistakes/OCR errors) can actually be
        // authored as ontology data, not just as unused Concept fields.
        IReadOnlyList<string> DialectWords,
        IReadOnlyList<string> CommonMisspellings,
        IReadOnlyList<string> Categories,
        // Names (not IDs - not resolvable until the whole batch is loaded)
        // of parent concepts this record claims, resolved into real
        // ConceptRelationship rows by IRelationshipBuilder after canonicalization.
        IReadOnlyList<string> ParentNames,
        double Confidence = 1.0);
}
