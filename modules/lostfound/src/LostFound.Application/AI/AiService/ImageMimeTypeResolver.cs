namespace LostFound.AI.AiService
{
    // Same magic-byte sniffing IImageValidator already validates image bytes
    // against (JPEG/PNG/WEBP) - just resolving which one, since ai_service's
    // multipart requests need a real image/* content type and neither the
    // search input (base64 bytes) nor the report image blob (raw bytes, no
    // stored content-type) carries one on their own. Kept here rather than
    // extending IImageValidator's contract, since that interface is outside
    // this feature's approved file scope.
    internal static class ImageMimeTypeResolver
    {
        public static string Resolve(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                return "image/jpeg";
            }

            if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                return "image/png";
            }

            return "image/webp";
        }
    }
}
