using Dorosak.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Dorosak.Application.IntegrationTests.Identity;

[Collection(InfrastructureTestGroup.Name)]
public sealed class AdminBootstrapTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Bootstrap_CreatesConfirmedMfaAdminOnlyOnce()
    {
        await using AsyncServiceScope firstScope = fixture.Services.CreateAsyncScope();
        IAdminBootstrapService bootstrap = firstScope.ServiceProvider
            .GetRequiredService<IAdminBootstrapService>();
        AdminBootstrapResult first = await bootstrap.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(first.Created);
        Assert.False(first.AlreadyExists);

        await using AsyncServiceScope secondScope = fixture.Services.CreateAsyncScope();
        IAdminBootstrapService secondBootstrap = secondScope.ServiceProvider
            .GetRequiredService<IAdminBootstrapService>();
        AdminBootstrapResult second = await secondBootstrap.ExecuteAsync(TestContext.Current.CancellationToken);
        Assert.False(second.Created);
        Assert.True(second.AlreadyExists);

        UserManager<ApplicationUser> userManager = secondScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = Assert.IsType<ApplicationUser>(
            await userManager.FindByEmailAsync("bootstrap-admin@example.test"));
        Assert.True(user.EmailConfirmed);
        Assert.True(user.TwoFactorEnabled);
        Assert.NotEqual("JBSWY3DPEHPK3PXP", user.ProtectedMfaSecret);
        Assert.True(await userManager.IsInRoleAsync(user, Dorosak.Infrastructure.Identity.IdentityConstants.AdminRole));
        Assert.True(await userManager.CheckPasswordAsync(user, "temporary bootstrap password"));
    }
}
