using System.Collections.Generic;

namespace LostFound.AI.Importers
{
    public sealed record ValidationIssue(string Code, string Message, string? RecordIdentifier);

    public sealed record ValidationResult(bool IsValid, IReadOnlyList<ValidationIssue> Issues)
    {
        public static readonly ValidationResult Valid = new(true, System.Array.Empty<ValidationIssue>());
    }

    // Schema/data-quality validation - the spec's explicit checklist:
    // missing identifiers, invalid UTF-8, invalid relationships, broken
    // references, circular references (where prohibited), unsupported
    // languages, duplicate concepts (structural duplicates within a single
    // batch, NOT cross-dataset semantic duplicates - see IDeduplicationService
    // for that).
    public interface IDataValidator
    {
        ValidationResult ValidateConcept(RawConceptRecord record, IReadOnlyCollection<string> supportedLanguages);

        ValidationResult ValidateRelationship(
            RawRelationshipRecord record, IReadOnlyCollection<string> knownConceptNames);
    }
}
