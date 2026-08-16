using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace LostFound.Reporters
{
    public class ReporterManager : DomainService
    {
        private readonly IReporterRepository _reporterRepository;

        public ReporterManager(IReporterRepository reporterRepository)
        {
            _reporterRepository = reporterRepository;
        }

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

            var existingByPhone = !string.IsNullOrWhiteSpace(phone)
                ? await _reporterRepository.FindByPhoneAsync(phone)
                : null;

            if (existingByPhone != null)
            {
                existingByPhone.LinkToIdentityUser(identityUserId);
                return existingByPhone;
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
