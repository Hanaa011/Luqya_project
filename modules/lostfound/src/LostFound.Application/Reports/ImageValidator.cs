using Microsoft.Extensions.Options;

namespace LostFound.Reports
{
    // See IImageValidator for why this is one shared implementation rather
    // than duplicated per call site.
    public class ImageValidator : IImageValidator
    {
        private readonly IOptions<ImageValidationOptions> _options;

        public ImageValidator(IOptions<ImageValidationOptions> options)
        {
            _options = options;
        }

        public ImageValidationResult Validate(byte[]? imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return ImageValidationResult.Invalid("Image must not be empty.");
            }

            var maxSizeBytes = _options.Value.MaxSizeBytes;
            if (imageBytes.Length > maxSizeBytes)
            {
                return ImageValidationResult.Invalid(
                    $"Image exceeds the maximum allowed size of {maxSizeBytes / (1024 * 1024)} MB.");
            }

            if (!IsRecognizedImageFormat(imageBytes))
            {
                return ImageValidationResult.Invalid(
                    "Image data is not a recognized JPEG, PNG, or WEBP file.");
            }

            return ImageValidationResult.Valid();
        }

        // Signature (magic-byte) sniffing only - deliberately not a full
        // decode (e.g. a new System.Drawing/ImageSharp dependency) to avoid
        // adding an image-processing library for what is purely a
        // reject-obvious-garbage-early check. Sufficient to reject non-image
        // payloads and truncated/corrupt headers before they ever reach an
        // AI provider, per Task A2's requirement.
        private static bool IsRecognizedImageFormat(byte[] bytes)
        {
            return IsJpeg(bytes) || IsPng(bytes) || IsWebp(bytes);
        }

        private static bool IsJpeg(byte[] b) =>
            b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;

        private static bool IsPng(byte[] b) =>
            b.Length >= 8 &&
            b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 &&
            b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A;

        private static bool IsWebp(byte[] b) =>
            b.Length >= 12 &&
            b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F' &&
            b[8] == (byte)'W' && b[9] == (byte)'E' && b[10] == (byte)'B' && b[11] == (byte)'P';
    }
}
