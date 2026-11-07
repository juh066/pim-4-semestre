using AuroraGaleria.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AuroraGaleria.Tests;

public sealed class SqlServerTestDatabase : IAsyncDisposable
{
    private const string Server = "(localdb)\\MSSQLLocalDB";
    public string DatabaseName { get; } = $"AuroraGaleriaTest_{Guid.NewGuid():N}";
    public string ConnectionString => $"Server={Server};Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True";

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
        return new AppDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection($"Server={Server};Database=master;Trusted_Connection=True;TrustServerCertificate=True");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]; END";
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class SqlServerWebFactory : WebApplicationFactory<Program>
{
    private readonly SqlServerTestDatabase _database = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DatabaseProvider"] = "SqlServer",
            ["ConnectionStrings:DefaultConnection"] = _database.ConnectionString,
            ["AcademicDemo:EnableAdminRegistration"] = "true",
            ["AcademicDemo:AdminRegistrationCode"] = "123"
        }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
