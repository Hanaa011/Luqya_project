using System;
using System.Collections.Generic;
using System.Linq;

namespace LostFound.AI.Importers
{
    internal sealed class DataValidator : IDataValidator
    {
        public ValidationResult ValidateConcept(RawConceptRecord record, IReadOnlyCollection<string> supportedLanguages)
        {
            var issues = new List<ValidationIssue>();

            if (string.IsNullOrWhiteSpace(record.SourceId))
            {
                issues.Add(new ValidationIssue("MISSING_ID", "Concept record has no SourceId.", record.CanonicalName));
            }

            if (string.IsNullOrWhiteSpace(record.CanonicalName))
            {
                issues.Add(new ValidationIssue("MISSING_NAME", "Concept record has no CanonicalName.", record.SourceId));
            }
            else if (!HasOnlyPairedSurrogates(record.CanonicalName))
            {
                issues.Add(new ValidationIssue(
                    "INVALID_UTF8", $"CanonicalName '{record.CanonicalName}' contains an unpaired surrogate character.", record.SourceId));
            }

            if (!supportedLanguages.Contains(record.LanguageCode, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    "UNSUPPORTED_LANGUAGE", $"Language '{record.LanguageCode}' is not supported.", record.SourceId));
            }

            return issues.Count == 0 ? ValidationResult.Valid : new ValidationResult(false, issues);
        }

        public ValidationResult ValidateRelationship(RawRelationshipRecord record, IReadOnlyCollection<string> knownConceptNames)
        {
            var issues = new List<ValidationIssue>();

            if (string.Equals(record.SourceConceptName, record.TargetConceptName, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    "CIRCULAR_REFERENCE", $"Relationship '{record.SourceConceptName}' -> itself is not allowed.", record.SourceConceptName));
            }

            if (!knownConceptNames.Contains(record.SourceConceptName, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    "BROKEN_REFERENCE", $"Relationship source '{record.SourceConceptName}' matches no concept in this batch.", record.SourceConceptName));
            }

            if (!knownConceptNames.Contains(record.TargetConceptName, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    "BROKEN_REFERENCE", $"Relationship target '{record.TargetConceptName}' matches no concept in this batch.", record.TargetConceptName));
            }

            return issues.Count == 0 ? ValidationResult.Valid : new ValidationResult(false, issues);
        }

        // A .NET string that survived parsing is already valid UTF-16; the
        // practical "invalid UTF-8" failure mode an importer actually hits
        // is mis-decoded source bytes leaving behind unpaired surrogates.
        private static bool HasOnlyPairedSurrogates(string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]))
                {
                    if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                    {
                        return false;
                    }
                    i++;
                }
                else if (char.IsLowSurrogate(text[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
