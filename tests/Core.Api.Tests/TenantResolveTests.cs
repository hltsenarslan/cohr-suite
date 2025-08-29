using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Core.Api.Tests;

public class TenantResolveTests : IClassFixture<CoreWebAppFactory>
{
    private readonly CoreWebAppFactory _factory;

    public TenantResolveTests(CoreWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Resolve_Firm1_Returns_Id()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/internal/tenants/resolve/firm1");
        var body = await res.Content.ReadAsStringAsync();

        Assert.True(res.IsSuccessStatusCode, body);
        Assert.Contains("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac", body);
    }
}