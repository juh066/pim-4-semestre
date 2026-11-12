using System.Text.RegularExpressions;
using AuroraGaleria.Models;
using AuroraGaleria.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AuroraGaleria.Tests;

public class SqlServerDeliveryScriptTests
{
    [Fact]
    public async Task DeliveryScriptsCreateSchemaSeedCatalogAndSupportReservationFlow()
    {
        await using var database = new SqlServerTestDatabase();
        await CreateEmptyDatabaseAsync(database.DatabaseName);

        var scriptsDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "database", "sql-server"));
        await ExecuteScriptAsync(database.ConnectionString, Path.Combine(scriptsDirectory, "01-estrutura.sql"));
        await ExecuteScriptAsync(database.ConnectionString, Path.Combine(scriptsDirectory, "02-dados-demonstracao.sql"));
        await ExecuteScriptAsync(database.ConnectionString, Path.Combine(scriptsDirectory, "02-dados-demonstracao.sql"));

        await using var db = database.CreateContext();
        Assert.Equal(1, await db.Exposicoes.CountAsync(item => item.Titulo == "Horizontes da Aurora"));
        Assert.Equal(3, await db.Produtos.CountAsync());
        Assert.Equal(2, await db.VisitSessions.CountAsync());

        Assert.True(await DatabaseObjectExistsAsync(database.ConnectionString, "dbo.sp_ReserveVisitSession", "P"));
        Assert.True(await DatabaseObjectExistsAsync(database.ConnectionString, "dbo.sp_CancelVisitReservation", "P"));
        Assert.True(await DatabaseObjectExistsAsync(database.ConnectionString, "dbo.trg_visit_sessions_audit", "TR"));

        var user = new User
        {
            Name = "Visitante fictício", Email = "visitante-script@example.test", PasswordHash = "hash-de-teste",
            Role = UserRoles.Visitor, IsActive = true, SessionVersion = Guid.NewGuid().ToString("N")
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var session = await db.VisitSessions.OrderBy(item => item.VisitDate).FirstAsync();
        var service = new ReservationService(db, new CurrentTestClock());
        var reservation = await service.ReserveAsync(user.Id, session.Id, 2, ReservationTypes.Ticket, "script-reservation");
        Assert.True(reservation.Success, reservation.Message);
        Assert.True((await service.CancelAsync(user.Id, reservation.Value!.Id)).Success);

        db.ChangeTracker.Clear();
        var persistedSession = (await db.VisitSessions.FindAsync(session.Id))!;
        Assert.Equal(0, persistedSession.ReservedQuantity);
        Assert.Equal(ReservationStatuses.Cancelled, (await db.VisitReservations.FindAsync(reservation.Value.Id))!.Status);
        persistedSession.Capacity++;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.True(await db.AdminAuditLogs.AnyAsync(item => item.EntityId == session.Id));
    }

    private static async Task CreateEmptyDatabaseAsync(string databaseName)
    {
        Assert.Matches("^AuroraGaleriaTest_[a-f0-9]+$", databaseName);
        await using var connection = new SqlConnection(
            "Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteScriptAsync(string connectionString, string path)
    {
        var script = await File.ReadAllTextAsync(path);
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var batch in batches.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 60;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<bool> DatabaseObjectExistsAsync(string connectionString, string objectName, string type)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID(@name) AND type = @type";
        command.Parameters.AddWithValue("@name", objectName);
        command.Parameters.AddWithValue("@type", type);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private sealed class CurrentTestClock : IGalleryClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateTimeOffset GalleryNow => DateTimeOffset.Now;
        public DateTimeOffset SessionStart(DateTime date, TimeSpan time) =>
            new(DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified), GalleryNow.Offset);
    }
}
