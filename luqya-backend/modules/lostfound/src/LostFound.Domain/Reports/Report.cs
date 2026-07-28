using System;
using System.Collections.Generic;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace LostFound.Reports
{
    public class Report : FullAuditedAggregateRoot<Guid>
    {
        public virtual Guid ReporterId { get; private set; }

        // AI-resolved, optional - filled in later by the background job.
        public virtual Guid? CategoryId { get; private set; }

        // Required structured location reference (used for analytics/filtering).
        public virtual Guid LocationId { get; private set; }

        // Optional free-text exact-place description, e.g. "Under the stairs".
        public virtual string? LocationDetails { get; private set; }

        public virtual ReportType Type { get; private set; }

        public virtual string? Description { get; private set; }

        // AI-generated, not user input.
        public virtual string? Color { get; private set; }

        public virtual DateTime? LostFoundDate { get; private set; }

        public virtual string? ImagePath { get; private set; }

        public virtual bool IsItemWithFinder { get; private set; }

        public virtual string? PickupLocation { get; private set; }

        public virtual ReportStatus Status { get; private set; }

        // ---- AI-generated metadata ----
        public virtual string? AiObjectType { get; private set; }

        public virtual string? AiBrand { get; private set; }

        public virtual string? AiTagsJson { get; private set; }

        public virtual bool IsAiClassified { get; private set; }

        // ---- AI semantic search vectors ----
        public virtual string? EmbeddingJson { get; private set; }

        public virtual string? ImageEmbeddingJson { get; private set; }

        protected Report()
        {
        }

        public Report(
            Guid id,
            Guid reporterId,
            Guid locationId,
            ReportType type,
            string? description = null,
            string? locationDetails = null,
            DateTime? lostFoundDate = null,
            string? imagePath = null,
            bool isItemWithFinder = false,
            string? pickupLocation = null,
            ReportStatus status = ReportStatus.Open) : base(id)
        {
            ReporterId = reporterId;
            SetLocation(locationId);
            Type = type;
            Description = description;
            SetLocationDetails(locationDetails);
            LostFoundDate = lostFoundDate;
            ImagePath = imagePath;
            IsItemWithFinder = isItemWithFinder;
            PickupLocation = pickupLocation;
            Status = status;
        }

        public Report SetStatus(ReportStatus status)
        {
            Status = status;
            return this;
        }

        public Report SetLocation(Guid locationId)
        {
            Check.NotNull(locationId, nameof(locationId));
            LocationId = locationId;
            return this;
        }

        public Report SetLocationDetails(string? locationDetails)
        {
            LocationDetails = Check.Length(locationDetails, nameof(locationDetails), maxLength: ReportConsts.MaxLocationDetailsLength);
            return this;
        }

        public Report UpdateDetails(
            string? description,
            string? locationDetails,
            DateTime? lostFoundDate,
            string? imagePath,
            bool isItemWithFinder,
            string? pickupLocation)
        {
            Description = description;
            SetLocationDetails(locationDetails);
            LostFoundDate = lostFoundDate;
            ImagePath = imagePath;
            IsItemWithFinder = isItemWithFinder;
            PickupLocation = pickupLocation;
            return this;
        }

        // Called once by the background job after AI classification. Safe to
        // call again on re-classification - always overwrites with the
        // latest AI output.
        public virtual Report ApplyAiClassification(
            Guid? categoryId,
            string? objectType,
            string? color,
            string? brand,
            IEnumerable<string>? tags)
        {
            CategoryId = categoryId;
            AiObjectType = objectType;
            Color = color;
            AiBrand = brand;
            AiTagsJson = tags != null ? JsonSerializer.Serialize(tags) : null;
            IsAiClassified = true;
            return this;
        }

        public virtual List<string> GetAiTags()
        {
            return string.IsNullOrWhiteSpace(AiTagsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(AiTagsJson) ?? new List<string>();
        }

        // Semantic text for the TEXT embedding - Description + LocationDetails
        // + AI classification output (object type / color / brand / tags /
        // category), enriched once classification has run.
        public virtual string BuildEmbeddingText(string? categoryName = null)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Description))
            {
                parts.Add(Description!);
            }

            if (!string.IsNullOrWhiteSpace(LocationDetails))
            {
                parts.Add(LocationDetails!);
            }

            if (!string.IsNullOrWhiteSpace(AiObjectType))
            {
                parts.Add(AiObjectType!);
            }

            if (!string.IsNullOrWhiteSpace(Color))
            {
                parts.Add(Color!);
            }

            if (!string.IsNullOrWhiteSpace(AiBrand))
            {
                parts.Add(AiBrand!);
            }

            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                parts.Add(categoryName!);
            }

            foreach (var tag in GetAiTags())
            {
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    parts.Add(tag);
                }
            }

            return parts.Count > 0 ? string.Join(". ", parts) : Type.ToString();
        }

        public virtual bool HasEmbedding => !string.IsNullOrWhiteSpace(EmbeddingJson);

        public virtual bool HasImageEmbedding => !string.IsNullOrWhiteSpace(ImageEmbeddingJson);

        public virtual Report SetEmbedding(float[] embedding)
        {
            EmbeddingJson = JsonSerializer.Serialize(embedding);
            return this;
        }

        public virtual float[]? GetEmbeddingVector()
        {
            return string.IsNullOrWhiteSpace(EmbeddingJson) ? null : JsonSerializer.Deserialize<float[]>(EmbeddingJson);
        }

        public virtual Report SetImageEmbedding(float[] embedding)
        {
            ImageEmbeddingJson = JsonSerializer.Serialize(embedding);
            return this;
        }

        public virtual float[]? GetImageEmbeddingVector()
        {
            return string.IsNullOrWhiteSpace(ImageEmbeddingJson) ? null : JsonSerializer.Deserialize<float[]>(ImageEmbeddingJson);
        }
    }
}
