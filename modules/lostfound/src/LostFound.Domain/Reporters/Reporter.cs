using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace LostFound.Reporters
{
    // Reporter is a business aggregate representing the owner of a Report,
    // regardless of whether they are an authenticated user or a guest.
    // It is NOT a replacement for IdentityUser - it optionally links to one
    // via IdentityUserId (a plain Guid, no cross-module FK - see
    // ReporterConfiguration for why).
    public class Reporter : FullAuditedAggregateRoot<Guid>
    {
        public virtual Guid? IdentityUserId { get; private set; }

        public virtual string? Name { get; private set; }

        public virtual string Phone { get; private set; }

        public virtual string? Email { get; private set; }

        public virtual PreferredContactType PreferredContact { get; private set; }

        protected Reporter()
        {
            Phone = string.Empty;
        }

        internal Reporter(
            Guid id,
            string phone,
            string? name = null,
            string? email = null,
            PreferredContactType preferredContact = PreferredContactType.Phone,
            Guid? identityUserId = null) : base(id)
        {
            Phone = string.Empty;
            SetPhone(phone);
            SetName(name);
            SetEmail(email);
            SetPreferredContact(preferredContact);
            IdentityUserId = identityUserId;
        }

        internal Reporter SetName(string? name)
        {
            Name = Check.Length(name, nameof(name), maxLength: ReporterConsts.MaxNameLength);
            return this;
        }

        internal Reporter SetPhone(string phone)
        {
            Phone = Check.NotNullOrWhiteSpace(
                phone,
                nameof(phone),
                maxLength: ReporterConsts.MaxPhoneLength
            );
            return this;
        }

        internal Reporter SetEmail(string? email)
        {
            Email = Check.Length(email, nameof(email), maxLength: ReporterConsts.MaxEmailLength);
            return this;
        }

        internal Reporter SetPreferredContact(PreferredContactType preferredContact)
        {
            PreferredContact = preferredContact;
            return this;
        }

        internal Reporter LinkToIdentityUser(Guid identityUserId)
        {
            IdentityUserId = identityUserId;
            return this;
        }
    }
}
