using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace UniversalPOS.Infrastructure.Data;

public class AppDbContextFactory
    : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddUserSecrets("5657daf5-a757-491c-a7cb-7eb086f7109a")
            .Build();

        string connectionString =
            configuration.GetConnectionString("NexusPOS")
            ?? throw new InvalidOperationException(
                "NexusPOS connection string was not found.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}