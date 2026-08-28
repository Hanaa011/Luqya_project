using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Forge.Localization;
using Forge.MultiTenancy;
using System;
using Volo.Abp.Emailing.Smtp;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.SettingManagement;
using Volo.Abp.BlobStoring.Database;
using Volo.Abp.Caching;
using Volo.Abp.OpenIddict;
using Volo.Abp.PermissionManagement.OpenIddict;
using Volo.Abp.AuditLogging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Emailing;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.TenantManagement;

namespace Forge;

[DependsOn(
    typeof(ForgeDomainSharedModule),
    typeof(AbpAuditLoggingDomainModule),
    typeof(AbpCachingModule),
    typeof(AbpBackgroundJobsDomainModule),
    typeof(AbpFeatureManagementDomainModule),
    typeof(AbpPermissionManagementDomainIdentityModule),
    typeof(AbpPermissionManagementDomainOpenIddictModule),
    typeof(AbpSettingManagementDomainModule),
    typeof(AbpEmailingModule),
    typeof(AbpIdentityDomainModule),
    typeof(AbpOpenIddictDomainModule),
    typeof(AbpTenantManagementDomainModule),
    typeof(BlobStoringDatabaseDomainModule)
    )]
public class ForgeDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpMultiTenancyOptions>(options =>
        {
            options.IsEnabled = MultiTenancyConsts.IsEnabled;
        });


        // Real SMTP sending, reusing ABP's existing Volo.Abp.Emailing.Smtp
        // .SmtpEmailSender (already referenced, no new provider) - it reads
        // its Host/Port/UserName/Password/EnableSsl/DefaultFromAddress from
        // the existing Email Settings management API (Abp.Mailing.* setting
        // names), which the backend developer configures with the server's
        // real credentials. Previously this was NullEmailSender (Debug-only,
        // meaning it was already an effective no-op here since local runs
        // are Debug builds), which is why no email has ever actually gone
        // out from this app yet.
        context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, SmtpEmailSender>());
    }
}
