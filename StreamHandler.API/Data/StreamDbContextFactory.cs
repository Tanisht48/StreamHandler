using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StreamHandler.API.Data;

/// <summary>
/// Used only by dotnet-ef CLI (migrations). Always targets PostgreSQL so that
/// migration files are generated against the relational schema, independent of
/// the runtime InMemory fallback.
/// </summary>
public class StreamDbContextFactory : IDesignTimeDbContextFactory<StreamDbContext>
{
    public StreamDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connStr = config.GetConnectionString("StreamHandlerDb");

        // Fall back to a localhost default so `dotnet ef migrations add` works
        // without a running Postgres instance (schema generation only).
        if (string.IsNullOrWhiteSpace(connStr))
            connStr = "Host=localhost;Port=5432;Database=streamhandler;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<StreamDbContext>();
        optionsBuilder.UseNpgsql(connStr);

        return new StreamDbContext(optionsBuilder.Options);
    }
}
