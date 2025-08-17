using System.Threading.Tasks;
using Testcontainers.PostgreSql;
using Xunit;

namespace Core.Api.Tests;

public sealed class TestDatabaseFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } =
        new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithDatabase("core")
            .Build();

    public string ConnectionString => Container.GetConnectionString(); 
    // örn: Host=localhost;Port=5432?;Database=core;Username=postgres;Password=postgres

    public Task InitializeAsync() => Container.StartAsync();
    public Task DisposeAsync()    => Container.DisposeAsync().AsTask();
}