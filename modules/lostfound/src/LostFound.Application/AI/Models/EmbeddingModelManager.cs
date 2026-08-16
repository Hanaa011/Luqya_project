using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LostFound.AI.Configuration;
using LostFound.AI.Storage;

namespace LostFound.AI.Models
{
    // Orchestrates install/activate/rollback (IEmbeddingModelManager) on top
    // of a persisted version history (IEmbeddingVersionManager), both
    // implemented by this one class since they share the same SQLite-backed
    // state and are only ever meaningfully used together - see
    // IEmbeddingVersionManager's doc comment for why the interfaces
    // themselves stay separate.
    // Also declared as IModelStore/IMetadataStore - see
    // Storage/StorageAbstractions.cs for why these are marker interfaces
    // rather than second implementations.
    internal sealed class EmbeddingModelManager : IEmbeddingModelManager, IEmbeddingVersionManager, IModelStore, IMetadataStore
    {
        private readonly EmbeddingSqliteConnectionFactory _connectionFactory;
        private readonly IEmbeddingDownloader _downloader;
        private readonly LocalAiRuntimeOptions _options;
        private readonly ILogger<EmbeddingModelManager> _logger;

        public EmbeddingModelManager(
            EmbeddingSqliteConnectionFactory connectionFactory,
            IEmbeddingDownloader downloader,
            IOptions<LocalAiRuntimeOptions> options,
            ILogger<EmbeddingModelManager> logger)
        {
            _connectionFactory = connectionFactory;
            _downloader = downloader;
            _options = options.Value;
            _logger = logger;
        }

        // ---- IEmbeddingModelManager ----------------------------------------

        public async Task<EmbeddingModelInfo> InstallAsync(EmbeddingModelDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            var installPath = Path.Combine(_options.ModelDirectory, descriptor.Name, descriptor.Version);
            Directory.CreateDirectory(installPath);

            var modelFilePath = Path.Combine(installPath, descriptor.ModelFileName);
            var downloadResult = await _downloader.DownloadAsync(
                descriptor.DownloadUri, modelFilePath, descriptor.Sha256Checksum, cancellationToken);

            if (!downloadResult.Success)
            {
                var failed = new EmbeddingModelInfo(
                    descriptor.Name, descriptor.Version, EmbeddingModelStatus.Failed, null, installPath, downloadResult.ActualChecksum, false);
                await RecordInstalledAsync(failed, cancellationToken);

                throw new InvalidOperationException(
                    $"Failed to install embedding model '{descriptor.Name}' v{descriptor.Version}: {downloadResult.ErrorMessage}");
            }

            var installed = new EmbeddingModelInfo(
                descriptor.Name, descriptor.Version, EmbeddingModelStatus.Installed, DateTime.UtcNow, installPath, downloadResult.ActualChecksum, false);
            await RecordInstalledAsync(installed, cancellationToken);

            _logger.LogInformation(
                "Embedding model '{Name}' v{Version} installed at '{Path}'.", descriptor.Name, descriptor.Version, installPath);

            return installed;
        }

        public Task<EmbeddingModelInfo?> GetActiveModelAsync(CancellationToken cancellationToken = default) =>
            GetActiveAsync(cancellationToken);

        public async Task<IReadOnlyList<EmbeddingModelInfo>> GetInstalledModelsAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT name, version, status, installed_at_utc, install_path, sha256_checksum, is_active " +
                "FROM embedding_models ORDER BY installed_at_utc DESC;";

            var results = new List<EmbeddingModelInfo>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(ReadModelInfo(reader));
            }

            return results;
        }

        public Task ActivateAsync(string name, string version, CancellationToken cancellationToken = default) =>
            SetActiveAsync(name, version, cancellationToken);

        public async Task RollbackToPreviousAsync(CancellationToken cancellationToken = default)
        {
            var history = await GetInstalledModelsAsync(cancellationToken);
            var active = history.FirstOrDefault(m => m.IsActive);

            var previous = history
                .Where(m => m.Status == EmbeddingModelStatus.Installed
                            && (active == null || m.Name != active.Name || m.Version != active.Version))
                .OrderByDescending(m => m.InstalledAtUtc)
                .FirstOrDefault();

            if (previous == null)
            {
                throw new InvalidOperationException("No previous installed embedding model version is available to roll back to.");
            }

            await SetActiveAsync(previous.Name, previous.Version, cancellationToken);

            _logger.LogWarning(
                "Rolled back active embedding model to '{Name}' v{Version}.", previous.Name, previous.Version);
        }

        public async Task<bool> VerifyIntegrityAsync(string name, string version, CancellationToken cancellationToken = default)
        {
            var models = await GetInstalledModelsAsync(cancellationToken);
            var model = models.FirstOrDefault(m => m.Name == name && m.Version == version);

            if (model?.InstallPath == null || model.Sha256Checksum == null)
            {
                return false;
            }

            var modelFilePath = Path.Combine(model.InstallPath, _options.ModelFileName);
            if (!File.Exists(modelFilePath))
            {
                return false;
            }

            var actualChecksum = await ComputeSha256Async(modelFilePath, cancellationToken);
            return string.Equals(actualChecksum, model.Sha256Checksum, StringComparison.OrdinalIgnoreCase);
        }

        // ---- IEmbeddingVersionManager ---------------------------------------

        public async Task RecordInstalledAsync(EmbeddingModelInfo info, CancellationToken cancellationToken = default)
        {
            using var connection = _connectionFactory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO embedding_models (name, version, status, installed_at_utc, install_path, sha256_checksum, is_active)
                VALUES ($name, $version, $status, $installedAt, $installPath, $checksum, 0)
                ON CONFLICT(name, version) DO UPDATE SET
                    status = excluded.status,
                    installed_at_utc = excluded.installed_at_utc,
                    install_path = excluded.install_path,
                    sha256_checksum = excluded.sha256_checksum;
                """;
            command.Parameters.AddWithValue("$name", info.Name);
            command.Parameters.AddWithValue("$version", info.Version);
            command.Parameters.AddWithValue("$status", info.Status.ToString());
            command.Parameters.AddWithValue("$installedAt", (object?)info.InstalledAtUtc?.ToString("O") ?? DBNull.Value);
            command.Parameters.AddWithValue("$installPath", (object?)info.InstallPath ?? DBNull.Value);
            command.Parameters.AddWithValue("$checksum", (object?)info.Sha256Checksum ?? DBNull.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<EmbeddingModelInfo>> GetHistoryAsync(string name, CancellationToken cancellationToken = default)
        {
            var all = await GetInstalledModelsAsync(cancellationToken);
            return all.Where(m => m.Name == name).ToList();
        }

        public async Task<EmbeddingModelInfo?> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            using (var connection = _connectionFactory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT name, version, status, installed_at_utc, install_path, sha256_checksum, is_active " +
                    "FROM embedding_models WHERE is_active = 1 LIMIT 1;";

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    return ReadModelInfo(reader);
                }
            }

            // No active row in the version history yet. This is the normal
            // state for a model that was copied into ModelDirectory by hand
            // (an operator installing a model out-of-band, e.g. because
            // automatic download is unavailable/undesired) rather than
            // through InstallAsync - nothing else in the system ever
            // discovers that case on its own, so without this the runtime
            // reports NotInstalled forever even though the files are right
            // there. Register + activate it using the same persisted-history
            // mechanism InstallAsync uses, so it behaves identically to a
            // normal install from this point on (visible in
            // GetInstalledModelsAsync/GetHistoryAsync, rollback-able, etc.).
            return await TryAutoRegisterManuallyInstalledModelAsync(cancellationToken);
        }

        private async Task<EmbeddingModelInfo?> TryAutoRegisterManuallyInstalledModelAsync(CancellationToken cancellationToken)
        {
            var modelPath = Path.Combine(_options.ModelDirectory, _options.ModelFileName);
            var tokenizerPath = Path.Combine(_options.ModelDirectory, _options.TokenizerFileName);

            if (!File.Exists(modelPath) || !File.Exists(tokenizerPath))
            {
                return null;
            }

            var name = ReadModelNameFromConfig(_options.ModelDirectory) ?? "local-manual";
            var checksum = await ComputeSha256Async(modelPath, cancellationToken);
            // Content-derived version: a manual replacement of model.onnx
            // (e.g. an operator swapping in a different export) is picked up
            // as a distinct version on the next resolution automatically,
            // without requiring an explicit version number from anywhere.
            var version = checksum[..12];

            var info = new EmbeddingModelInfo(
                name, version, EmbeddingModelStatus.Installed, DateTime.UtcNow, _options.ModelDirectory, checksum, false);

            await RecordInstalledAsync(info, cancellationToken);
            await SetActiveAsync(name, version, cancellationToken);

            _logger.LogInformation(
                "Auto-registered manually installed embedding model '{Name}' v{Version} found at '{Path}' " +
                "(no prior install record existed in the version history).", name, version, _options.ModelDirectory);

            return info with { IsActive = true };
        }

        // Best-effort: the HuggingFace "_name_or_path" field, when present,
        // gives a human-readable model identity for diagnostics/history
        // instead of the generic "local-manual" fallback. Never required -
        // a missing/unreadable config.json just means less descriptive
        // naming, not a failure to register the model.
        private static string? ReadModelNameFromConfig(string modelDirectory)
        {
            var configPath = Path.Combine(modelDirectory, "config.json");
            if (!File.Exists(configPath))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(configPath));
                return document.RootElement.TryGetProperty("_name_or_path", out var nameElement)
                    ? nameElement.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public async Task SetActiveAsync(string name, string version, CancellationToken cancellationToken = default)
        {
            using var connection = _connectionFactory.CreateOpenConnection();
            using var transaction = connection.BeginTransaction();

            using (var clear = connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = "UPDATE embedding_models SET is_active = 0;";
                await clear.ExecuteNonQueryAsync(cancellationToken);
            }

            using (var set = connection.CreateCommand())
            {
                set.Transaction = transaction;
                set.CommandText = "UPDATE embedding_models SET is_active = 1 WHERE name = $name AND version = $version;";
                set.Parameters.AddWithValue("$name", name);
                set.Parameters.AddWithValue("$version", version);

                var rows = await set.ExecuteNonQueryAsync(cancellationToken);
                if (rows == 0)
                {
                    throw new InvalidOperationException($"No installed embedding model '{name}' v{version} was found to activate.");
                }
            }

            transaction.Commit();
        }

        // ---- shared helpers --------------------------------------------------

        private static EmbeddingModelInfo ReadModelInfo(SqliteDataReader reader) => new(
            reader.GetString(0),
            reader.GetString(1),
            Enum.Parse<EmbeddingModelStatus>(reader.GetString(2)),
            reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetInt32(6) == 1);

        private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
        {
            await using var stream = File.OpenRead(filePath);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexString(hash);
        }
    }
}
