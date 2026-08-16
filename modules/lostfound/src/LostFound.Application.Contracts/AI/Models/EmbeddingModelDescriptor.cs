using System;

namespace LostFound.AI.Models
{
    // Describes a model an operator/administrator wants installed - the
    // input to IEmbeddingModelManager.InstallAsync. Never invented/guessed
    // by application code; this is meant to come from configuration or an
    // explicit admin action, since installing a model is a deliberate
    // operational decision, not something search requests trigger.
    public sealed record EmbeddingModelDescriptor(
        string Name,
        string Version,
        Uri DownloadUri,
        string Sha256Checksum,
        string ModelFileName,
        string TokenizerFileName);

    public enum EmbeddingModelStatus
    {
        Downloading,
        Failed,
        Installed,
        Corrupted
    }

    // A row of installation history / current install state, as tracked by
    // IEmbeddingVersionManager. IsActive marks the single version
    // IEmbeddingRuntime should load - see IEmbeddingModelManager.ActivateAsync.
    public sealed record EmbeddingModelInfo(
        string Name,
        string Version,
        EmbeddingModelStatus Status,
        DateTime? InstalledAtUtc,
        string? InstallPath,
        string? Sha256Checksum,
        bool IsActive);
}
