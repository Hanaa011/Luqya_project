using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LostFound.AI.Configuration;
using LostFound.AI.Models;

namespace LostFound.AI.Runtime
{
    // Default IEmbeddingRuntime: lazily loads the active installed model
    // (per IEmbeddingModelManager) on first use and keeps it loaded (warm)
    // for the lifetime of the singleton. Never throws out of IsAvailable/
    // GetStatusAsync - a missing/corrupt/not-yet-installed model is a
    // normal, expected state (see EmbeddingRuntimeHealth.NotInstalled),
    // reported so LostFound.AI.Embeddings.LocalFirstEmbeddingEngine can fall
    // back to the external provider without this ever bubbling up as an
    // exception to a search request.
    internal sealed class OnnxEmbeddingRuntime : IEmbeddingRuntime, IDisposable
    {
        private readonly IEmbeddingModelManager _modelManager;
        private readonly LocalAiRuntimeOptions _options;
        private readonly ILogger<OnnxEmbeddingRuntime> _logger;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        private IEmbeddingModel? _loadedModel;
        private EmbeddingRuntimeHealth _health = EmbeddingRuntimeHealth.NotInstalled;
        private string? _lastError = "Local embedding model has not been loaded yet.";

        public OnnxEmbeddingRuntime(
            IEmbeddingModelManager modelManager,
            IOptions<LocalAiRuntimeOptions> options,
            ILogger<OnnxEmbeddingRuntime> logger)
        {
            _modelManager = modelManager;
            _options = options.Value;
            _logger = logger;
        }

        public bool IsAvailable => _loadedModel != null && _health == EmbeddingRuntimeHealth.Healthy;

        public string? ActiveModelVersion => _loadedModel?.Version;

        public async Task<EmbeddingRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            await EnsureModelLoadedAsync(cancellationToken);
            return new EmbeddingRuntimeStatus(_health, _loadedModel?.Name, _loadedModel?.Version, _lastError);
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            await EnsureModelLoadedAsync(cancellationToken);

            if (_loadedModel == null)
            {
                throw new InvalidOperationException($"Local embedding runtime is unavailable: {_lastError}");
            }

            return await _loadedModel.EmbedAsync(text, cancellationToken);
        }

        private async Task EnsureModelLoadedAsync(CancellationToken cancellationToken)
        {
            if (_loadedModel != null)
            {
                return;
            }

            if (!_options.Enabled)
            {
                _health = EmbeddingRuntimeHealth.NotInstalled;
                _lastError = "Local AI runtime is disabled via configuration (\"LostFound:AI:LocalRuntime:Enabled\").";
                return;
            }

            await _loadLock.WaitAsync(cancellationToken);
            try
            {
                if (_loadedModel != null)
                {
                    return;
                }

                var active = await _modelManager.GetActiveModelAsync(cancellationToken);
                if (active is not { Status: EmbeddingModelStatus.Installed, InstallPath: not null })
                {
                    _health = EmbeddingRuntimeHealth.NotInstalled;
                    _lastError = "No local embedding model is installed yet. Search uses the configured " +
                                  "external provider until a model is installed via IEmbeddingModelManager.InstallAsync.";
                    return;
                }

                _health = EmbeddingRuntimeHealth.Loading;
                _loadedModel = OnnxEmbeddingModel.Load(active, _options);
                _health = EmbeddingRuntimeHealth.Healthy;
                _lastError = null;

                _logger.LogInformation(
                    "Local embedding model '{Name}' v{Version} loaded successfully.", active.Name, active.Version);
            }
            catch (Exception ex)
            {
                _health = EmbeddingRuntimeHealth.Faulted;
                _lastError = ex.Message;
                _logger.LogWarning(ex, "Failed to load local embedding model; falling back to the external provider.");
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public void Dispose() => _loadedModel?.Dispose();
    }
}
