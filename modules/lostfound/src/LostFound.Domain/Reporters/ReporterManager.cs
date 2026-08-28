using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace LostFound.Reporters
{
    public class ReporterManager : DomainService
    {
        // How long a claim-verification email link stays redeemable.
        private static readonly TimeSpan ClaimTokenLifetime = TimeSpan.FromMinutes(60);

        private readonly IReporterRepository _reporterRepository;
        private readonly IReporterClaimTokenRepository _reporterClaimTokenRepository;

        public ReporterManager(
            IReporterRepository reporterRepository,
            IReporterClaimTokenRepository reporterClaimTokenRepository)
        {
            _reporterRepository = reporterRepository;
            _reporterClaimTokenRepository = reporterClaimTokenRepository;
        }

        // Security note: this deliberately does NOT auto-link an existing
        // guest Reporter by phone match anymore. It used to - any existing
        // Reporter whose Phone matched the (caller-suppliable, via
        // CreateReportDto.ReporterPhone) phone value got silently linked to
        // whichever IdentityUser happened to submit a report with that
        // phone typed in, with zero proof the caller actually controls that
        // phone number. That let one authenticated user take over another
        // (possibly guest) reporter's identity - including their past
        // reports - just by typing the victim's phone number into an
        // unrelated new report. The only supported way to link a guest
        // Reporter to an account now is the verified email claim flow
        // (IssueClaimTokenIfNeededAsync / ClaimGuestReportAsync below).
        public async Task<Reporter> FindOrCreateForIdentityUserAsync(
            Guid identityUserId,
            string? name,
            string phone,
            string? email,
            PreferredContactType preferredContact = PreferredContactType.Phone)
        {
            var existing = await _reporterRepository.FindByIdentityUserIdAsync(identityUserId);
            if (existing != null)
            {
                return existing;
            }

            var reporter = new Reporter(
                GuidGenerator.Create(),
                phone,
                name,
                email,
                preferredContact,
                identityUserId
            );

            await _reporterRepository.InsertAsync(reporter);

            return reporter;
        }

        // Idempotent: returns null (and issues nothing) when a still-valid
        // (unused, unexpired) token already exists for this reporter - the
        // caller must treat null as "don't send another email," since the
        // raw value of that still-pending token isn't recoverable from its
        // stored hash. Returns the raw token (only ever held in memory here
        // and by the caller for exactly one outgoing email) when a new one
        // was actually created.
        public async Task<string?> IssueClaimTokenIfNeededAsync(Guid reporterId)
        {
            var now = Clock.Now;

            var existing = await _reporterClaimTokenRepository.FindValidForReporterAsync(reporterId, now);
            if (existing != null)
            {
                return null;
            }

            var rawToken = GenerateRawToken();
            var token = new ReporterClaimToken(
                GuidGenerator.Create(), reporterId, HashToken(rawToken), now.Add(ClaimTokenLifetime));

            await _reporterClaimTokenRepository.InsertAsync(token);

            return rawToken;
        }

        public async Task<Reporter> ClaimGuestReportAsync(string rawToken, Guid identityUserId)
        {
            var token = await _reporterClaimTokenRepository.FindByTokenHashAsync(HashToken(rawToken))
                ?? throw new BusinessException(ReporterErrorCodes.ClaimTokenInvalid);

            var now = Clock.Now;
            if (!token.IsValid(now))
            {
                throw new BusinessException(ReporterErrorCodes.ClaimTokenInvalid);
            }

            var reporter = await _reporterRepository.GetAsync(token.ReporterId);

            // Fail-safe: a valid, unexpired, unused token must still never
            // override a Reporter that's already linked to someone by the
            // time it's redeemed (e.g. two claim attempts racing, or the
            // reporter already claimed via a different token). Left
            // unmarked-used deliberately - it didn't actually consume
            // anything, and any later redemption attempt hits this exact
            // same, deterministic rejection regardless of this token's own
            // used/unused state, so there's no replay risk either way.
            if (reporter.IdentityUserId != null)
            {
                throw new BusinessException(ReporterErrorCodes.ReporterAlreadyLinked);
            }

            reporter.LinkToIdentityUser(identityUserId);
            await _reporterRepository.UpdateAsync(reporter);

            token.MarkUsed(now);
            await _reporterClaimTokenRepository.UpdateAsync(token);

            return reporter;
        }

        private static string GenerateRawToken() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        private static string HashToken(string rawToken) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        public async Task<Reporter> FindOrCreateForGuestAsync(
            string? name,
            string phone,
            string? email,
            PreferredContactType preferredContact = PreferredContactType.Phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                throw new BusinessException(ReporterErrorCodes.PhoneIsRequiredForGuests);
            }

            var existing = await _reporterRepository.FindByPhoneAsync(phone);
            if (existing != null)
            {
                return existing;
            }

            var reporter = new Reporter(
                GuidGenerator.Create(),
                phone,
                name,
                email,
                preferredContact
            );

            await _reporterRepository.InsertAsync(reporter);

            return reporter;
        }

        public Task UpdateContactInfoAsync(
            Reporter reporter,
            string? name,
            string? email,
            PreferredContactType preferredContact)
        {
            Check.NotNull(reporter, nameof(reporter));

            reporter.SetName(name);
            reporter.SetEmail(email);
            reporter.SetPreferredContact(preferredContact);

            return Task.CompletedTask;
        }
    }
}
