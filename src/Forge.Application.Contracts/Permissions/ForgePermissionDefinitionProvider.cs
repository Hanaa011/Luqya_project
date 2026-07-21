using Forge.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace Forge.Permissions;

public class ForgePermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(ForgePermissions.GroupName);

        var booksPermission = myGroup.AddPermission(ForgePermissions.Books.Default, L("Permission:Books"));
        booksPermission.AddChild(ForgePermissions.Books.Create, L("Permission:Books.Create"));
        booksPermission.AddChild(ForgePermissions.Books.Edit, L("Permission:Books.Edit"));
        booksPermission.AddChild(ForgePermissions.Books.Delete, L("Permission:Books.Delete"));

        var authorsPermission = myGroup.AddPermission(ForgePermissions.Authors.Default, L("Permission:Authors"));
        authorsPermission.AddChild(ForgePermissions.Authors.Create, L("Permission:Authors.Create"));
        authorsPermission.AddChild(ForgePermissions.Authors.Edit, L("Permission:Authors.Edit"));
        authorsPermission.AddChild(ForgePermissions.Authors.Delete, L("Permission:Authors.Delete"));
        //Define your own permissions here. Example:
        //myGroup.AddPermission(ForgePermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<ForgeResource>(name);
    }
}
