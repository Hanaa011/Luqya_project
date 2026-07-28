using System.Threading;
using System.Threading.Tasks;

namespace LostFound.AI
{
    // The AI-first replacement for manual Category selection. Given a
    // description and an optional image, returns everything the UI used to
    // ask the user for, generated automatically instead.
    public interface IItemClassificationProvider
    {
        string ProviderName { get; }

        Task<ItemClassificationResult> ClassifyAsync(
            string? description,
            byte[]? imageBytes,
            CancellationToken cancellationToken = default);
    }

    public class ItemClassificationResult
    {
        // Free-text category NAME (resolved/created via CategoryManager,
        // never a Guid the AI could not possibly know).
        public string? CategoryName { get; set; }

        public string? ObjectType { get; set; }

        public string? Color { get; set; }

        public string? Brand { get; set; }

        public System.Collections.Generic.List<string> Tags { get; set; } = new();

        // Used to build the image embedding (caption-then-embed approach).
        public string? ImageCaption { get; set; }
    }
}
