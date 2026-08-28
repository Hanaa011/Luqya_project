using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.Uow;
using LostFound.Calls;
using LostFound.Conversations.Dtos;
using LostFound.Reports;
using LostFound.Reporters;

namespace LostFound.Conversations
{
    public class ConversationAppService : ApplicationService, IConversationAppService
    {
        // 1 hour - generous enough that a call never gets cut off mid-ring/
        // mid-conversation from token expiry alone within this Phase's scope.
        private const uint CallPrivilegeExpireSeconds = 3600;

        private readonly IConversationRepository _conversationRepository;
        private readonly IReportRepository _reportRepository;
        private readonly IReporterRepository _reporterRepository;
        private readonly IIdentityUserRepository _identityUserRepository;
        private readonly InMemoryCallStateStore _callStateStore;
        private readonly IConfiguration _configuration;
        private readonly ReporterManager _reporterManager;
        private readonly IEmailSender _emailSender;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public ConversationAppService(
            IConversationRepository conversationRepository,
            IReportRepository reportRepository,
            IReporterRepository reporterRepository,
            IIdentityUserRepository identityUserRepository,
            InMemoryCallStateStore callStateStore,
            IConfiguration configuration,
            ReporterManager reporterManager,
            IEmailSender emailSender,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _conversationRepository = conversationRepository;
            _reportRepository = reportRepository;
            _reporterRepository = reporterRepository;
            _identityUserRepository = identityUserRepository;
            _callStateStore = callStateStore;
            _configuration = configuration;
            _reporterManager = reporterManager;
            _emailSender = emailSender;
            _unitOfWorkManager = unitOfWorkManager;
        }

        public async Task<ConversationDto> OpenAsync(Guid reportId)
        {
            var currentUserId = RequireCurrentUserId();

            var report = await _reportRepository.GetAsync(reportId);
            var reporter = await _reporterRepository.GetAsync(report.ReporterId);

            if (reporter.IdentityUserId == null)
            {
                // Trigger (idempotently) the guest verification-claim email
                // instead of just failing - see ReporterManager
                // .IssueClaimTokenIfNeededAsync for the "don't resend while
                // a valid token is still pending" guarantee. Only fires when
                // we actually have somewhere to send it; a guest who only
                // gave a phone number gets no claim email today (in scope:
                // existing email infra only, no SMS), and the caller still
                // gets the same clear, distinct error below either way.
                if (!string.IsNullOrWhiteSpace(reporter.Email))
                {
                    // requiresNew: OpenAsync always throws right after this
                    // (below), which would otherwise roll back the ambient
                    // unit of work - and the token insert along with it. The
                    // token/email side effect must survive that rollback, so
                    // it gets its own independent transaction that commits
                    // before the throw, regardless of what happens after.
                    using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);

                    var rawToken = await _reporterManager.IssueClaimTokenIfNeededAsync(reporter.Id);
                    if (rawToken != null)
                    {
                        await SendClaimEmailAsync(reporter.Email, rawToken);
                    }

                    await uow.CompleteAsync();
                }

                throw new BusinessException(
                    ReporterErrorCodes.ReportOwnerNotClaimed,
                    "This report's owner hasn't registered an account. We've emailed them a link to verify " +
                    "and claim it - you'll be able to message them here once they do.");
            }

            var ownerId = reporter.IdentityUserId.Value;

            if (ownerId == currentUserId)
            {
                throw new UserFriendlyException("You can't start a conversation about your own report.");
            }

            var (participant1Id, participant2Id) = NormalizeParticipants(currentUserId, ownerId);

            var conversation = await _conversationRepository.FindByReportAndParticipantsAsync(
                reportId, participant1Id, participant2Id);

            if (conversation == null)
            {
                conversation = new Conversation(GuidGenerator.Create(), reportId, participant1Id, participant2Id);
                await _conversationRepository.InsertAsync(conversation);
            }

            return await MapToDtoAsync(conversation, currentUserId, report, includeAllMessages: false);
        }

        public async Task<List<ConversationDto>> GetListAsync()
        {
            var currentUserId = RequireCurrentUserId();

            var conversations = await _conversationRepository.GetListForUserWithMessagesAsync(currentUserId);
            if (conversations.Count == 0)
            {
                return new List<ConversationDto>();
            }

            var reportIds = conversations.Select(c => c.ReportId).Distinct().ToList();
            var reports = await _reportRepository.GetListAsync(r => reportIds.Contains(r.Id));
            var reportsById = reports.ToDictionary(r => r.Id);

            var ordered = conversations.OrderByDescending(c =>
                c.Messages.Count > 0 ? c.Messages.Max(m => m.CreationTime) : c.CreationTime);

            var dtos = new List<ConversationDto>();
            foreach (var conversation in ordered)
            {
                reportsById.TryGetValue(conversation.ReportId, out var report);
                dtos.Add(await MapToDtoAsync(conversation, currentUserId, report, includeAllMessages: false));
            }

            return dtos;
        }

        public async Task<ConversationDto> GetAsync(Guid id)
        {
            var currentUserId = RequireCurrentUserId();

            var conversation = await _conversationRepository.GetWithMessagesAsync(id)
                ?? throw new EntityNotFoundException(typeof(Conversation), id);

            EnsureParticipant(conversation, currentUserId);

            // "mark incoming messages read when the recipient opens the
            // conversation" - GetAsync is exactly that moment (initial open
            // AND every subsequent poll while the page stays open, which is
            // fine: MarkMessagesReadFor is a no-op once nothing is unread).
            if (conversation.MarkMessagesReadFor(currentUserId))
            {
                await _conversationRepository.UpdateAsync(conversation);
            }

            var report = await _reportRepository.FindAsync(conversation.ReportId);

            return await MapToDtoAsync(conversation, currentUserId, report, includeAllMessages: true);
        }

        public async Task<ConversationMessageDto> SendMessageAsync(Guid id, SendMessageInputDto input)
        {
            var currentUserId = RequireCurrentUserId();

            var text = input.Text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                throw new UserFriendlyException("Message text is required.");
            }

            var conversation = await _conversationRepository.GetWithMessagesAsync(id)
                ?? throw new EntityNotFoundException(typeof(Conversation), id);

            EnsureParticipant(conversation, currentUserId);

            var report = await _reportRepository.FindAsync(conversation.ReportId);
            if (report != null && report.Status == ReportStatus.Closed)
            {
                throw new UserFriendlyException("This report is closed - new messages can't be sent.");
            }

            var message = conversation.AddMessage(GuidGenerator.Create(), currentUserId, text);
            await _conversationRepository.UpdateAsync(conversation);

            return new ConversationMessageDto
            {
                Id = message.Id,
                SenderId = message.SenderId,
                Text = message.Text,
                CreationTime = message.CreationTime,
                IsRead = message.IsRead,
                IsMine = true
            };
        }

        public async Task<CallCredentialsDto> StartCallAsync(Guid id)
        {
            var currentUserId = RequireCurrentUserId();

            var conversation = await _conversationRepository.GetAsync(id);
            EnsureParticipant(conversation, currentUserId);
            await EnsureCallEligibleAsync(conversation);
            var (appId, appCertificate) = GetAgoraConfigOrThrow();

            // Idempotent - a repeated click just re-fetches the same
            // ringing/connected call's credentials, never starts a second
            // concurrent call for this conversation. The config check above
            // runs first specifically so a misconfigured server never
            // creates a "Ringing" entry it can't actually issue tokens for.
            var call = _callStateStore.StartOrGetExisting(id, currentUserId);

            return BuildCallCredentials(appId, appCertificate, id, currentUserId, call.CallId);
        }

        public async Task<CallCredentialsDto> JoinCallAsync(Guid id)
        {
            var currentUserId = RequireCurrentUserId();

            var conversation = await _conversationRepository.GetAsync(id);
            EnsureParticipant(conversation, currentUserId);
            await EnsureCallEligibleAsync(conversation);
            var (appId, appCertificate) = GetAgoraConfigOrThrow();

            var call = _callStateStore.Get(id)
                ?? throw new UserFriendlyException("There is no active call to join.");

            var credentials = BuildCallCredentials(appId, appCertificate, id, currentUserId, call.CallId);

            // Only flip to Connected once real credentials were actually
            // produced - a failed join must never leave the caller's side
            // showing "Connected" for a callee who has no working token.
            _callStateStore.MarkConnected(id);

            return credentials;
        }

        public async Task EndCallAsync(Guid id)
        {
            var currentUserId = RequireCurrentUserId();

            var conversation = await _conversationRepository.GetAsync(id);
            EnsureParticipant(conversation, currentUserId);

            _callStateStore.End(id);
        }

        private async Task EnsureCallEligibleAsync(Conversation conversation)
        {
            var report = await _reportRepository.FindAsync(conversation.ReportId);
            if (report != null && report.Status == ReportStatus.Closed)
            {
                throw new UserFriendlyException("This report is closed - calling isn't available.");
            }
        }

        // Reads from .NET User Secrets in Development (see appsettings/
        // launch config) - the App Certificate never leaves this method's
        // local variable and is never placed on any DTO.
        private (string AppId, string AppCertificate) GetAgoraConfigOrThrow()
        {
            var appId = _configuration["LostFound:Agora:AppId"];
            var appCertificate = _configuration["LostFound:Agora:AppCertificate"];

            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appCertificate))
            {
                throw new UserFriendlyException("Voice calling isn't configured on this server yet.");
            }

            return (appId, appCertificate);
        }

        // Never returns the App Certificate - only what the Agora Web SDK
        // needs to join this one channel as this one user.
        private static CallCredentialsDto BuildCallCredentials(
            string appId, string appCertificate, Guid conversationId, Guid userId, Guid callId)
        {
            var channelName = "luqya-" + conversationId.ToString("N");
            var uid = DeriveUid(conversationId, userId);

            var token = AgoraTokenBuilder.BuildVoiceToken(
                appId, appCertificate, channelName, uid, CallPrivilegeExpireSeconds, CallPrivilegeExpireSeconds);

            return new CallCredentialsDto
            {
                CallId = callId,
                AppId = appId,
                ChannelName = channelName,
                Token = token,
                Uid = uid,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(CallPrivilegeExpireSeconds),
            };
        }

        // Deterministic per (conversation, user) so the same person always
        // gets the same numeric Agora uid within this channel; with only
        // two participants per conversation, a hash collision between them
        // is not a practical concern.
        private static uint DeriveUid(Guid conversationId, Guid userId)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(conversationId.ToString("N") + ":" + userId.ToString("N")));
            var value = BitConverter.ToUInt32(hash, 0);
            return value == 0 ? 1u : value; // 0 is reserved by Agora to mean "auto-assign"
        }

        // Best-effort: a delivery failure (e.g. SMTP not yet configured on
        // this server) must not break the "This is my item" flow itself -
        // the claim/token state above is already durable and idempotent
        // regardless of whether this particular send succeeds. Logged, not
        // swallowed silently.
        private async Task SendClaimEmailAsync(string toEmail, string rawToken)
        {
            var claimUrl = $"{_configuration["App:AngularUrl"]?.TrimEnd('/')}/claim/{rawToken}";

            try
            {
                await _emailSender.SendAsync(
                    toEmail,
                    "Someone found your lost/found report on Luqya",
                    "Someone on Luqya believes they're related to a report you submitted. " +
                    $"To let them message you, verify it's yours: {claimUrl}\n\n" +
                    "This link works once and expires in 60 minutes. If you didn't submit a report on Luqya, " +
                    "you can ignore this email.",
                    isBodyHtml: false);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to send reporter claim email.");
            }
        }

        private Guid RequireCurrentUserId()
        {
            if (!CurrentUser.IsAuthenticated || CurrentUser.Id == null)
            {
                throw new AbpAuthorizationException("You must be signed in to use messaging.");
            }

            return CurrentUser.Id.Value;
        }

        private static void EnsureParticipant(Conversation conversation, Guid userId)
        {
            if (!conversation.HasParticipant(userId))
            {
                throw new AbpAuthorizationException("You are not a participant in this conversation.");
            }
        }

        // Duplicate prevention (ReportId + same two users, either order).
        private static (Guid Participant1Id, Guid Participant2Id) NormalizeParticipants(Guid a, Guid b) =>
            a.CompareTo(b) <= 0 ? (a, b) : (b, a);

        private async Task<ConversationDto> MapToDtoAsync(
            Conversation conversation, Guid currentUserId, Report? report, bool includeAllMessages)
        {
            var otherUserId = conversation.Participant1Id == currentUserId
                ? conversation.Participant2Id
                : conversation.Participant1Id;

            var otherUser = await _identityUserRepository.FindAsync(otherUserId);

            var dto = new ConversationDto
            {
                Id = conversation.Id,
                ReportId = conversation.ReportId,
                CreationTime = conversation.CreationTime,
                OtherParticipantName = ResolveDisplayName(otherUser),
                ReportDescription = report?.Description,
                ReportType = report?.Type ?? default,
                ReportIsClosed = report?.Status == ReportStatus.Closed,
                ActiveCall = MapActiveCallToDto(_callStateStore.Get(conversation.Id)),
                UnreadCount = conversation.Messages.Count(m => m.SenderId != currentUserId && !m.IsRead),
            };

            if (includeAllMessages)
            {
                dto.Messages = conversation.Messages
                    .OrderBy(m => m.CreationTime)
                    .Select(m => MapMessageToDto(m, currentUserId))
                    .ToList();
            }
            else if (conversation.Messages.Count > 0)
            {
                var last = conversation.Messages.OrderByDescending(m => m.CreationTime).First();
                dto.Messages = new List<ConversationMessageDto> { MapMessageToDto(last, currentUserId) };
            }

            return dto;
        }

        private static ActiveCallDto? MapActiveCallToDto(ActiveCall? call) =>
            call == null
                ? null
                : new ActiveCallDto
                {
                    CallId = call.CallId,
                    CallerId = call.CallerId,
                    State = call.State.ToString(),
                    StartedAtUtc = call.StartedAtUtc,
                };

        private static ConversationMessageDto MapMessageToDto(ConversationMessage message, Guid currentUserId) =>
            new()
            {
                Id = message.Id,
                SenderId = message.SenderId,
                Text = message.Text,
                CreationTime = message.CreationTime,
                IsRead = message.IsRead,
                IsMine = message.SenderId == currentUserId
            };

        // Name/surname if we have them, otherwise the username - never
        // email/phone. Mirrors the frontend's own displayName() helper
        // (Nav.jsx) so both sides agree on what to call a user.
        private static string ResolveDisplayName(IdentityUser? user)
        {
            if (user == null)
            {
                return "Luqya user";
            }

            var full = string.Join(" ", new[] { user.Name, user.Surname }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return string.IsNullOrWhiteSpace(full) ? user.UserName : full;
        }
    }
}
