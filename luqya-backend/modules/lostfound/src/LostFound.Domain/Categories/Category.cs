using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace LostFound.Categories
{
    public class Category : FullAuditedAggregateRoot<Guid>
    {
        public virtual string Name { get; private set; }

        public virtual string? Icon { get; private set; }

        protected Category()
        {
            Name = string.Empty;
        }

        public Category(Guid id, string name, string? icon = null) : base(id)
        {
            Name = string.Empty;
            SetName(name);
            SetIcon(icon);
        }

        public Category SetName(string name)
        {
            Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: CategoryConsts.MaxNameLength);
            return this;
        }

        public Category SetIcon(string? icon)
        {
            Icon = Check.Length(icon, nameof(icon), maxLength: CategoryConsts.MaxIconLength);
            return this;
        }
    }
}
