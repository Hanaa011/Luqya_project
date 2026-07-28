using Xunit;

namespace Forge.EntityFrameworkCore;

[CollectionDefinition(ForgeTestConsts.CollectionDefinitionName)]
public class ForgeEntityFrameworkCoreCollection : ICollectionFixture<ForgeEntityFrameworkCoreFixture>
{

}
