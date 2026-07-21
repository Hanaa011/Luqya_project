using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI
{
    // Lives in Application.Contracts (NOT Domain) - the Domain layer stays
    // 100% free of any AI-related type. Implementations live directly in
    // LostFound.Application/AI/Providers (merged, no separate AI project).
    public interface IEmbeddingProvider
    {
        string ProviderName { get; }

        Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

        // Practical approach: rather than requiring a dedicated multimodal/
        // CLIP-style embedding model, the image is captioned by the same
        // provider's vision model, then the caption text is embedded with
        // the method above (caption-then-embed).
        Task<float[]> GenerateImageEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
    }
}
