using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class GatewayFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Core’a giden HttpClient’ı fake handler ile değiştir
            services.AddHttpClient("core").ConfigurePrimaryHttpMessageHandler(() => new FakeCoreHandler());
        });

        return base.CreateHost(builder);
    }
}