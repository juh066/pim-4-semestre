using AuroraGaleria.Data;
using AuroraGaleria.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AuroraGaleria.Configuration;
using AuroraGaleria.Services;

namespace AuroraGaleria.Tests;

internal sealed class TestDatabase : IAsyncDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aurora-tests-{Guid.NewGuid():N}.db");
    public string ConnectionString => $"Data Source={Path};Default Timeout=10;Foreign Keys=True";

    public AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(ConnectionString).Options);

    public async Task<(int User1, int User2, int Product, int Exposure, int Session)> SeedAsync(int capacity = 5)
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
        var users = new[]
        {
            new User { Name = "Visitante Um", Email = "um@example.test", PasswordHash = "hash", Role = UserRoles.Visitor },
            new User { Name = "Visitante Dois", Email = "dois@example.test", PasswordHash = "hash", Role = UserRoles.Visitor }
        };
        var exposure = new Exposicao { Titulo = "Exposição Teste", Descricao = "", ImagemUrl = "/test.jpg", Periodo = "Teste", Inicio = DateTime.Today.AddDays(1), Fim = DateTime.Today.AddDays(30), Ativa = true };
        var product = new Produto { Nome = "Produto Teste", Preco = 50m, ImagemUrl = "/test.jpg", Ativo = true };
        db.AddRange(users); db.Add(exposure); db.Add(product); await db.SaveChangesAsync();
        var session = new VisitSession { ExposureId = exposure.Id, VisitDate = DateTime.Today.AddDays(2), StartTime = new TimeSpan(10, 0, 0), Capacity = capacity, IsActive = true };
        db.Add(session); await db.SaveChangesAsync();
        return (users[0].Id, users[1].Id, product.Id, exposure.Id, session.Id);
    }

    public static IGalleryClock CreateClock() => new GalleryClock(TimeProvider.System, Options.Create(new GalleryOptions()));

    public async ValueTask DisposeAsync()
    {
        await Task.Yield();
        SqliteConnection.ClearAllPools();
        if (File.Exists(Path)) File.Delete(Path);
    }
}
