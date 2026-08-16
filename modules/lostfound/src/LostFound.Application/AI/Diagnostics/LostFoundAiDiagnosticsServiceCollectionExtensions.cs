using Microsoft.Extensions.DependencyInjection;

namespace LostFound.AI
{
    // Phase 2A Part 5 (Infrastructure, Storage, Caching) DI wiring for the
    // cross-cutting IAiPlatformDiagnostics aggregator. Must be called AFTER
    // AddLostFoundLocalAiRuntime, AddLostFoundKnowledgeGraph, and
    // AddLostFoundDatasetImporters - it depends on services each of those
    // registers (IEmbeddingRuntimeDiagnostics, IConceptCache,
    // IDatasetImportHistoryRepository, IEnumerable&lt;IDatasetImporter&gt;).
    public static class LostFoundAiDiagnosticsServiceCollectionExtensions
    {
        public static IServiceCollection AddLostFoundAiDiagnostics(this IServiceCollection services)
        {
            services.AddSingleton<Diagnostics.IAiPlatformDiagnostics, Diagnostics.AiPlatformDiagnostics>();

            return services;
        }
    }
}
