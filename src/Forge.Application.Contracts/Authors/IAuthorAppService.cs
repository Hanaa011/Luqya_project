using System;
using System.Threading.Tasks;
using Forge.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Forge.Authors;

public interface IAuthorAppService :
    ICrudAppService<
        AuthorDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateAuthorDto>
{
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(AuthorExcelDownloadDto input);

    Task<DownloadTokenResultDto> GetDownloadTokenAsync();
}
