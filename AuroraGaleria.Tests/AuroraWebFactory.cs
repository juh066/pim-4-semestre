using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AuroraGaleria.Data;

namespace AuroraGaleria.Tests;

public sealed class AuroraWebFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"aurora-web-{Guid.NewGuid():N}.db");
    private readonly bool _enableAdminRegistration;
    private readonly string _adminRegistrationCode;

    public AuroraWebFactory() : this(false, string.Empty)
    {
    }

    internal AuroraWebFactory(bool enableAdminRegistration, string adminRegistrationCode)
    {
        _enableAdminRegistration = enableAdminRegistration;
        _adminRegistrationCode = adminRegistrationCode;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath};Default Timeout=10;Foreign Keys=True",
            ["DatabaseProvider"] = "Sqlite",
            ["AcademicDemo:EnableAdminRegistration"] = _enableAdminRegistration.ToString(),
            ["AcademicDemo:AdminRegistrationCode"] = _adminRegistrationCode
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(
                $"Data Source={_databasePath};Default Timeout=10;Foreign Keys=True"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(_databasePath)) File.Delete(_databasePath);
        }
    }
}
