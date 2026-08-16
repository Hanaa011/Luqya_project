using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LostFound.AI.Models
{
    // Real HTTP downloader with checksum verification - downloads to a
    // ".download" temp file first and only moves it into place once the
    // SHA-256 matches, so a partial/corrupted/tampered download is never
    // mistaken for a valid installed model (Part 2 spec's "Security"
    // section: "Validate downloaded artifacts", "Reject unsigned or
    // corrupted models").
    internal sealed class HttpEmbeddingDownloader : IEmbeddingDownloader
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<HttpEmbeddingDownloader> _logger;

        public HttpEmbeddingDownloader(IHttpClientFactory httpClientFactory, ILogger<HttpEmbeddingDownloader> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<EmbeddingDownloadResult> DownloadAsync(
            Uri sourceUri, string destinationPath, string expectedSha256Checksum, CancellationToken cancellationToken = default)
        {
            var tempPath = destinationPath + ".download";

            try
            {
                var httpClient = _httpClientFactory.CreateClient();

                using (var response = await httpClient.GetAsync(sourceUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();

                    var directory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using var fileStream = File.Create(tempPath);
                    await responseStream.CopyToAsync(fileStream, cancellationToken);
                }

                var actualChecksum = await ComputeSha256Async(tempPath, cancellationToken);

                if (!string.Equals(actualChecksum, expectedSha256Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(tempPath);

                    return new EmbeddingDownloadResult(
                        false,
                        null,
                        actualChecksum,
                        $"Checksum mismatch: expected '{expectedSha256Checksum}', got '{actualChecksum}'. " +
                        "Rejecting the download - the file may be corrupted or tampered with.");
                }

                File.Move(tempPath, destinationPath, overwrite: true);

                return new EmbeddingDownloadResult(true, destinationPath, actualChecksum, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to download embedding model artifact from '{Uri}'.", sourceUri);

                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                return new EmbeddingDownloadResult(false, null, null, ex.Message);
            }
        }

        private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
        {
            await using var stream = File.OpenRead(filePath);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexString(hash);
        }
    }
}
