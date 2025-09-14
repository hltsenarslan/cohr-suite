using Core.Api.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Api.Services;

public interface ILicenseCache
{
    LicenseSnapshot Snapshot { get; }
    DateTime LoadedAt { get; }
    string Mode { get; } // convenience
    string Fingerprint { get; } // convenience

    Task ReloadAsync(); // admin endpoint için
}

public sealed class LicenseCache : ILicenseCache
{
    private readonly ILogger<LicenseCache> _logger;
    private readonly IHostEnvironment _env;
    private readonly IConfiguration _cfg;

    private LicenseSnapshot _snapshot;

    public LicenseCache(ILogger<LicenseCache> logger, IHostEnvironment env, IConfiguration cfg)
    {
        _logger = logger;
        _env = env;
        _cfg = cfg;

        _snapshot = LicenseLoader.LoadAndValidate(_env.ContentRootPath, _cfg, _logger);
    }

    public LicenseSnapshot Snapshot => _snapshot;
    public DateTime LoadedAt => _snapshot.LoadedAt;
    public string Mode => _snapshot.Mode;
    public string Fingerprint => _snapshot.Fingerprint;

    public Task ReloadAsync()
    {
        _snapshot = LicenseLoader.LoadAndValidate(_env.ContentRootPath, _cfg, _logger);
        return Task.CompletedTask;
    }
}