using Volo.Abp.BlobStoring;

namespace LostFound.Reports
{
    // Blob container marker for report images - adjust to whatever
    // container/provider (Azure/S3/FileSystem) is already configured for
    // this module's image uploads. Lives in Contracts (not Application)
    // so LostFound.HttpApi can reference IBlobContainer<ReportImageContainer>
    // directly for the image-retrieval endpoint (ReportImagesController)
    // without taking a dependency on the whole Application layer.
    //
    // [BlobContainerName] pins the container's stable storage name to its
    // ORIGINAL value (this type previously lived in the LostFound.BackgroundJobs
    // namespace, which ABP's default convention uses to derive the container
    // name it already persisted into AbpBlobContainers/AbpBlobs for every
    // report image uploaded before this move). Without this attribute, moving
    // the type would silently orphan every already-uploaded image - confirmed
    // live during Task 6 (Phase-3-Part-2-Real-World-Validation-Report.md): a
    // freshly-added retrieval endpoint 404'd on real, existing blobs because
    // the container name had drifted with the namespace change.
    [BlobContainerName("LostFound.BackgroundJobs.ReportImageContainer")]
    public class ReportImageContainer
    {
    }
}
