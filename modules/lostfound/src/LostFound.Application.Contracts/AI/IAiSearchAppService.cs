using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using LostFound.AI.Dtos;

namespace LostFound.AI
{
    // Real-time semantic search ONLY - separate from IReportAppService and
    // IMatchAppService by design.
    public interface IAiSearchAppService : IApplicationService
    {
        Task<AiSearchResponseDto> SearchAsync(AiSearchInputDto input);
    }
}
