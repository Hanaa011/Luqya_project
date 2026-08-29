using System;

namespace LostFound.Reporters.Dtos
{
    public class ConfirmReporterClaimResultDto
    {
        public Guid ReporterId { get; set; }

        // Set only when someone has already claimed "this is my item"
        // against one of this reporter's reports - null means nothing to
        // auto-open yet (see ReporterAppService.TryOpenClaimedConversationAsync).
        public Guid? ConversationId { get; set; }
    }
}
