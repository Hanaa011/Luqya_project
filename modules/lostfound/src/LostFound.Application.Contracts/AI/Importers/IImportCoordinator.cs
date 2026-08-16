using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Importers
{
    public enum ImportMode
    {
        // Re-import even if the latest recorded DatasetVersion already
        // matches what was just fetched.
        Full,

        // Skip (record DatasetImportStatus.Skipped) when the fetched
        // DatasetVersion already matches the last successful import -
        // "avoid rebuilding unchanged knowledge" (spec).
        Incremental
    }

    // Full diagnostics/reporting output for one coordinator run across
    // however many importers it was given - the spec's "Diagnostics"
    // section list, verbatim.
    public sealed record ImportReport(IReadOnlyList<DatasetImportRecord> DatasetResults)
    {
        public int TotalConceptsImported => Sum(r => r.ConceptCount);
        public int TotalRelationshipsImported => Sum(r => r.RelationshipCount);
        public int TotalDuplicateGroups => Sum(r => r.DuplicateGroupCount);
        public int TotalValidationFailures => Sum(r => r.ValidationFailureCount);
        public long TotalElapsedMilliseconds => Sum(r => (int)r.ElapsedMilliseconds);

        private int Sum(System.Func<DatasetImportRecord, int> selector)
        {
            var total = 0;
            foreach (var result in DatasetResults)
            {
                total += selector(result);
            }
            return total;
        }
    }

    // Orchestrates the full pipeline (schema validation onward - see
    // IDatasetImporter) for one or more sources, running them in parallel
    // and isolating failures per-source ("Retry failed datasets" / a
    // failing source must never abort the others).
    public interface IImportCoordinator
    {
        Task<DatasetImportRecord> ImportAsync(IDatasetImporter importer, ImportMode mode, CancellationToken cancellationToken = default);

        Task<ImportReport> ImportAllAsync(IEnumerable<IDatasetImporter> importers, ImportMode mode, CancellationToken cancellationToken = default);
    }
}
