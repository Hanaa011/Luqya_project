using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace LostFound.Reports.Dtos
{
    public class ReportDto : AuditedEntityDto<Guid>
    {
        public Guid ReporterId { get; set; }

        // Nullable - null until AI classification runs.
        public Guid? CategoryId { get; set; }

        public Guid LocationId { get; set; }

        public string? LocationDetails { get; set; }

        public ReportType Type { get; set; }
        public string? Description { get; set; }
        public DateTime? LostFoundDate { get; set; }
        public string? ImagePath { get; set; }
        public bool IsItemWithFinder { get; set; }
        public string? PickupLocation { get; set; }
        public ReportStatus Status { get; set; }
        public bool HasEmbedding { get; set; }

        // ---- AI-generated metadata (read-only, never set by the user) ----
        public string? Color { get; set; }
        public string? AiObjectType { get; set; }
        public string? AiBrand { get; set; }
        public List<string> AiTags { get; set; } = new();
        public bool IsAiClassified { get; set; }
    }
}
