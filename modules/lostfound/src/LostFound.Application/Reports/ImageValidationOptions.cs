namespace LostFound.Reports
{
    // Bound from configuration section "LostFound:ImageValidation". Shared
    // by ReportAppService.UploadImageAsync and AiSearchAppService.SearchAsync
    // (see IImageValidator) - the audit's §20/§38 Issue #15 finding was that
    // no size/format limit existed anywhere in the module, frontend or
    // backend; this is the single, configurable source of truth for it.
    public class ImageValidationOptions
    {
        public int MaxSizeBytes { get; set; } = 8 * 1024 * 1024; // 8 MB
    }
}
