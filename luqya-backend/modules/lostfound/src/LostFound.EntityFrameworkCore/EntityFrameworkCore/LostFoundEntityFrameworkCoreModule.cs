using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace LostFound.EntityFrameworkCore;

[DependsOn(
    typeof(LostFoundDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class LostFoundEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<LostFoundDbContext>(options =>
        {
            options.AddDefaultRepositories<ILostFoundDbContext>(includeAllEntities: true);
            
            /* Add custom repositories here. Example:
            * options.AddRepository<Question, EfCoreQuestionRepository>();
            */
        });
    }
}
