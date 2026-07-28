using System;
using Volo.Abp.Application.Dtos;

namespace Forge.Authors;

public class AuthorDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; }

    public DateTime BirthDate { get; set; }

    public string? ShortBio { get; set; }
}
