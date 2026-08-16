using System;
using System.Collections.Generic;
using System.Linq;
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
        // EmbeddingJson is the DESCRIPTION embedding (the user's own words -
        // Description + LocationDetails only). MetadataEmbeddingJson is a
        // second, independent embedding of the AI-classified attributes
        // (ObjectType/Color/Brand/Category/Tags), kept in its own vector so
        // a wrong or hallucinated classification value can no longer corrupt
        // the primary semantic-relevance signal - see
        // Multi-Representation-Embedding-Architecture-Analysis.md for the
        // real, twice-reproduced evidence this addresses (a hallucinated
        // color value shifting the WHOLE combined vector away from the true
        // semantic content). Every existing consumer of EmbeddingJson
        // (DuplicateResolver, MatchManager's resilience fallback, the legacy
        // AiMatchingService path) is therefore automatically and correctly
        // re-pointed at the Description-only embedding with no changes of
        // its own required - it was already reading this exact field.
        public virtual string? EmbeddingJson { get; private set; }

        public virtual string? MetadataEmbeddingJson { get; private set; }

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
            AiTagsJson = SerializeJson(tags?.ToList());
            IsAiClassified = true;
            return this;
        }

        /// <summary>
        /// Returns the raw (non-normalized) AI-generated tags for this report,
        /// deserialized from <see cref="AiTagsJson"/>. Returns an empty list
        /// instead of null when no tags are set.
        /// </summary>
        public virtual List<string> GetAiTags()
        {
            return DeserializeJson<List<string>>(AiTagsJson) ?? new List<string>();
        }

        // Semantic text for the DESCRIPTION embedding - the user's own
        // words only (Description + LocationDetails), never touched by any
        // AI-generated classification output. This is deliberately the ONLY
        // text this method returns now - see the EmbeddingJson field remarks
        // for why splitting it this way was worth doing.
        public virtual string BuildDescriptionEmbeddingText()
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

            return parts.Count > 0 ? string.Join(". ", parts) : Type.ToString();
        }

        // Semantic text for the METADATA embedding - AI classification
        // output only (object type / color / brand / category / tags),
        // isolated from the user's own Description text. Returns null (not
        // an empty/fallback string) when there is genuinely no classified
        // metadata yet, so the caller can skip generating this embedding
        // entirely rather than embedding meaningless placeholder text - the
        // same "optional signal, gracefully absent" pattern HasEmbedding/
        // HasImageEmbedding already use.
        public virtual string? BuildMetadataEmbeddingText(string? categoryName = null)
        {
            var parts = new List<string>();

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

            return parts.Count > 0 ? string.Join(". ", parts) : null;
        }

        public virtual bool HasEmbedding => !string.IsNullOrWhiteSpace(EmbeddingJson);

        public virtual bool HasMetadataEmbedding => !string.IsNullOrWhiteSpace(MetadataEmbeddingJson);

        public virtual bool HasImageEmbedding => !string.IsNullOrWhiteSpace(ImageEmbeddingJson);

        public virtual Report SetEmbedding(float[] embedding)
        {
            EmbeddingJson = SerializeJson(embedding);
            return this;
        }

        public virtual float[]? GetEmbeddingVector()
        {
            return DeserializeJson<float[]>(EmbeddingJson);
        }

        public virtual Report SetMetadataEmbedding(float[] embedding)
        {
            MetadataEmbeddingJson = SerializeJson(embedding);
            return this;
        }

        public virtual float[]? GetMetadataEmbeddingVector()
        {
            return DeserializeJson<float[]>(MetadataEmbeddingJson);
        }

        public virtual Report SetImageEmbedding(float[] embedding)
        {
            ImageEmbeddingJson = SerializeJson(embedding);
            return this;
        }

        public virtual float[]? GetImageEmbeddingVector()
        {
            return DeserializeJson<float[]>(ImageEmbeddingJson);
        }

        // =====================================================================
        // ---- Hybrid AI matching support -------------------------------------
        //
        // These members expose normalized, comparable views of the AI-generated
        // metadata (object type / color / brand / tags) so that an external
        // scoring engine can combine structured-attribute equality with
        // semantic/image embedding similarity. No similarity scoring, weighting,
        // or matching-decision logic lives here - the entity only exposes clean
        // domain information; the scoring itself belongs to an application/
        // domain service.
        // =====================================================================

        /// <summary>
        /// True if this report has an AI-resolved object type.
        /// </summary>
        public virtual bool HasObjectType() => !string.IsNullOrWhiteSpace(AiObjectType);

        /// <summary>
        /// True if this report has an AI-resolved color.
        /// </summary>
        public virtual bool HasColor() => !string.IsNullOrWhiteSpace(Color);

        /// <summary>
        /// True if this report has an AI-resolved brand.
        /// </summary>
        public virtual bool HasBrand() => !string.IsNullOrWhiteSpace(AiBrand);

        /// <summary>
        /// True if this report has at least one AI-generated tag.
        /// </summary>
        public virtual bool HasTags() => GetAiTags().Any(tag => !string.IsNullOrWhiteSpace(tag));

        /// <summary>
        /// Returns the object type trimmed and lower-cased for case-insensitive
        /// comparison, or <see cref="string.Empty"/> when not set.
        /// </summary>
        public virtual string GetNormalizedObjectType() => NormalizeAttribute(AiObjectType);

        /// <summary>
        /// Returns the color trimmed and lower-cased for case-insensitive
        /// comparison, or <see cref="string.Empty"/> when not set.
        /// </summary>
        public virtual string GetNormalizedColor() => NormalizeAttribute(Color);

        /// <summary>
        /// Returns the brand trimmed and lower-cased for case-insensitive
        /// comparison, or <see cref="string.Empty"/> when not set.
        /// </summary>
        public virtual string GetNormalizedBrand() => NormalizeAttribute(AiBrand);

        /// <summary>
        /// Returns the AI-generated tags trimmed, lower-cased, de-duplicated,
        /// and stripped of blank entries. Returns an empty list instead of
        /// null when no tags are set.
        /// </summary>
        public virtual List<string> GetNormalizedTags()
        {
            return NormalizeTagList(GetAiTags());
        }

        /// <summary>
        /// True if both reports have an object type set and it matches
        /// case-insensitively after trimming. Returns false if either report
        /// has no object type.
        /// </summary>
        public virtual bool IsSameObjectType(Report other)
        {
            Check.NotNull(other, nameof(other));

            return HasObjectType() && other.HasObjectType() &&
                   GetNormalizedObjectType() == other.GetNormalizedObjectType();
        }

        /// <summary>
        /// True if both reports have a color set and it matches
        /// case-insensitively after trimming. Returns false if either report
        /// has no color.
        /// </summary>
        public virtual bool IsSameColor(Report other)
        {
            Check.NotNull(other, nameof(other));

            return HasColor() && other.HasColor() &&
                   GetNormalizedColor() == other.GetNormalizedColor();
        }

        /// <summary>
        /// True if both reports have a brand set and it matches
        /// case-insensitively after trimming. Returns false if either report
        /// has no brand.
        /// </summary>
        public virtual bool IsSameBrand(Report other)
        {
            Check.NotNull(other, nameof(other));

            return HasBrand() && other.HasBrand() &&
                   GetNormalizedBrand() == other.GetNormalizedBrand();
        }

        /// <summary>
        /// Returns the set of normalized tags shared between this report and
        /// <paramref name="other"/>. Returns an empty list when either report
        /// has no tags or there is no overlap.
        /// </summary>
        public virtual List<string> GetCommonTags(Report other)
        {
            Check.NotNull(other, nameof(other));

            return GetNormalizedTags()
                .Intersect(other.GetNormalizedTags())
                .ToList();
        }

        /// <summary>
        /// Trims whitespace and lower-invariants a raw AI-generated value,
        /// returning <see cref="string.Empty"/> instead of null/blank.
        /// Public so callers outside the entity (e.g. a matching engine
        /// normalizing an unsaved search query) can normalize values the
        /// same way this entity normalizes its own attributes, without
        /// duplicating the rule.
        /// </summary>
        public static string NormalizeAttribute(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Trims, lower-invariants, de-duplicates, and strips blank entries
        /// from a raw tag list, returning an empty list instead of null.
        /// Public for the same reason as <see cref="NormalizeAttribute"/>.
        /// </summary>
        public static List<string> NormalizeTagList(IEnumerable<string>? tags)
        {
            return tags == null
                ? new List<string>()
                : tags.Select(NormalizeAttribute).Where(tag => tag.Length > 0).Distinct().ToList();
        }

        /// <summary>
        /// Central JSON serialization helper used to persist AI metadata
        /// (tags, embeddings) so that serialization logic is not duplicated
        /// across setters. Returns null for a null value.
        /// </summary>
        private static string? SerializeJson<T>(T? value)
        {
            return value == null ? null : JsonSerializer.Serialize(value);
        }

        /// <summary>
        /// Central JSON deserialization helper used to read AI metadata
        /// (tags, embeddings) so that deserialization logic is not duplicated
        /// across getters. Returns null for a null/blank value.
        /// </summary>
        private static T? DeserializeJson<T>(string? json)
        {
            return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json);
        }
    }
}