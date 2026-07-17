using CustomerService.Application.Interfaces;
using CustomerService.Application.Services;
using CustomerService.Infrastructure.Data;
using CustomerService.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CustomerService.Tests.Fixtures;

public sealed class ServiceFixture : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly ServiceProvider provider;
    private readonly IServiceScope scope;

    public ServiceFixture()
    {
        connection.Open();
        var services = new ServiceCollection();
        services.AddLogging(x => x.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<CustomerDbContext>(x => x.UseSqlite(connection));
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerAddressRepository, CustomerAddressRepository>();
        services.AddScoped<ICustomerService, CustomerApplicationService>();
        services.AddScoped<ICustomerAddressService, CustomerAddressApplicationService>();
        provider = services.BuildServiceProvider();
        scope = provider.CreateScope();
        Db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        Db.Database.EnsureCreated();
    }

    public CustomerDbContext Db { get; }
    public ICustomerService Customers => scope.ServiceProvider.GetRequiredService<ICustomerService>();
    public ICustomerAddressService Addresses => scope.ServiceProvider.GetRequiredService<ICustomerAddressService>();

    public async ValueTask DisposeAsync()
    {
        scope.Dispose();
        await provider.DisposeAsync();
        await connection.DisposeAsync();
    }
}
