using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.BlobStoring;

namespace LostFound.Reports
{
    // Task 6 (Phase 3 Part 2 real-world validation): IReportAppService.UploadImageAsync
    // has existed since PHASE-VALIDATION-08 and genuinely persists uploaded image
    // bytes (proven live - see Phase-3-Part-2-Real-World-Validation-Report.md §1),
    // but nothing ever exposed a way to read them back. Match.jsx's
    // `<img src={report.imagePath}>` has therefore always pointed at a bare blob
    // name (e.g. "af92694fc89..."), never a real URL - the browser could only ever
    // resolve that as a broken relative link. A plain MVC controller (not an
    // ApplicationService) is used here, not because of any specific ABP requirement,
    // but because this needs to return the image's own bytes with a real image
    // Content-Type, which is what an <img> tag needs - an ApplicationService method
    // returning byte[] would instead be JSON/base64-wrapped by the conventional
    // controller, unusable directly as an <img src>. Anonymous access matches
    // ReportAppService.GetAsync/GetListAsync (Match.jsx itself is not an
    // auth-guarded route - see the system audit's frontend routing table).
    [Route("api/app/report/image")]
    public class ReportImagesController : LostFoundController
    {
        private readonly IBlobContainer<ReportImageContainer> _imageContainer;

        public ReportImagesController(IBlobContainer<ReportImageContainer> imageContainer)
        {
            _imageContainer = imageContainer;
        }

        [HttpGet("{blobName}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAsync(string blobName)
        {
            if (!await _imageContainer.ExistsAsync(blobName))
            {
                return NotFound();
            }

            var bytes = await _imageContainer.GetAllBytesAsync(blobName);
            return File(bytes, ResolveContentType(bytes));
        }

        // Magic-byte sniffing only, same signatures as ImageValidator - kept as
        // its own small local check rather than sharing that class's contract,
        // since validation ("is this acceptable to store?") and this
        // ("what Content-Type header does this already-stored blob need?") are
        // different questions asked at different times, with IImageValidator's
        // JPEG/PNG/WEBP-or-reject shape not naturally extending to "return the
        // specific type" without changing its return contract for every caller.
        private static string ResolveContentType(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                return "image/jpeg";
            }

            if (bytes.Length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            {
                return "image/png";
            }

            if (bytes.Length >= 12 &&
                bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
                bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
            {
                return "image/webp";
            }

            return "application/octet-stream";
        }
    }
}
