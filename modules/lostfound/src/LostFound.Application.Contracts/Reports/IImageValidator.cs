namespace LostFound.Reports
{
    // Shared image validation used by both the report-image-upload path
    // (ReportAppService.UploadImageAsync) and the AI image-search path
    // (AiSearchAppService.SearchAsync) - see Luqya-System-Reference.md
    // §20/§38 Issue #15 ("no size or format validation exists anywhere in
    // the module, frontend or backend"). One implementation, two call
    // sites, so the rule can never drift between them.
    public interface IImageValidator
    {
        ImageValidationResult Validate(byte[]? imageBytes);
    }

    public sealed class ImageValidationResult
    {
        private ImageValidationResult(bool isValid, string? errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        public bool IsValid { get; }

        public string? ErrorMessage { get; }

        public static ImageValidationResult Valid() => new(true, null);

        public static ImageValidationResult Invalid(string errorMessage) => new(false, errorMessage);
    }
}
