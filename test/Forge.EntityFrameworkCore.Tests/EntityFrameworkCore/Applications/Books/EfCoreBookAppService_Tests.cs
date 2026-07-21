using Forge.Books;
using Xunit;

namespace Forge.EntityFrameworkCore.Applications.Books;

[Collection(ForgeTestConsts.CollectionDefinitionName)]
public class EfCoreBookAppService_Tests : BookAppService_Tests<ForgeEntityFrameworkCoreTestModule>
{

}