using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using LostFound.AI.Caching;
using LostFound.AI.Configuration;
using LostFound.AI.Importers;
using LostFound.AI.Runtime;

namespace LostFound.AI.Diagnostics
{
    internal sealed class AiPlatformDiagnostics(
        IEmbeddingRuntimeDiagnostics embeddingDiagnostics,
        IConceptCache conceptCache,
        IDatasetImportHistoryRepository importHistory,
        IEnumerable<IDatasetImporter> importers,
        IOptions<LocalAiRuntimeOptions> localAiOptions,
        IOptions<KnowledgeGraphOptions> knowledgeGraphOptions) : IAiPlatformDiagnostics
    {
        public async Task<AiPlatformDiagnosticsReport> GetReportAsync(CancellationToken cancellationToken = default)
        {
            var embeddingReport = await embeddingDiagnostics.GetReportAsync(cancellationToken);

            var storage = new List<StorageHealth>
            {
                DescribeFile("embeddings", localAiOptions.Value.DatabasePath),
                DescribeFile("knowledge", knowledgeGraphOptions.Value.DatabasePath)
            };

            var latestImports = new List<DatasetImportRecord>();
            foreach (var importer in importers)
            {
                var latest = await importHistory.GetLatestSuccessfulAsync(importer.DatasetName, cancellationToken);
                if (latest != null)
                {
                    latestImports.Add(latest);
                }
            }

            return new AiPlatformDiagnosticsReport(embeddingReport, conceptCache.Count, storage, latestImports);
        }

        private static StorageHealth DescribeFile(string storeName, string path)
        {
            var fileInfo = new FileInfo(path);
            return new StorageHealth(
                storeName,
                fileInfo.Exists,
                fileInfo.Exists ? fileInfo.Length : null,
                fileInfo.Exists ? fileInfo.LastWriteTimeUtc : null);
        }
    }
}
