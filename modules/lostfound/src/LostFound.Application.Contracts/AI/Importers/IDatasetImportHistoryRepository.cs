using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Importers
{
    public interface IDatasetImportHistoryRepository
    {
        Task RecordAsync(DatasetImportRecord record, CancellationToken cancellationToken = default);

        // Used by IImportCoordinator to implement "avoid rebuilding
        // unchanged knowledge": if the latest successful import already
        // matches the freshly-fetched DatasetVersion, the import is skipped
        // - see ImportCoordinator.ImportAsync.
        Task<DatasetImportRecord?> GetLatestSuccessfulAsync(string datasetName, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<DatasetImportRecord>> GetHistoryAsync(string datasetName, CancellationToken cancellationToken = default);
    }
}
