using System;
using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI.Models
{
    public sealed record EmbeddingDownloadResult(
        bool Success,
        string? FilePath,
        string? ActualChecksum,
        string? ErrorMessage);

    // Fetches a model artifact and verifies it against an expected SHA-256
    // checksum before it's ever considered installed - see the Part 2 spec's
    // "Security" section ("Validate downloaded artifacts", "Reject unsigned
    // or corrupted models"). A checksum mismatch is treated as a rejected
    // download, not a warning.
    public interface IEmbeddingDownloader
    {
        Task<EmbeddingDownloadResult> DownloadAsync(
            Uri sourceUri,
            string destinationPath,
            string expectedSha256Checksum,
            CancellationToken cancellationToken = default);
    }
}
