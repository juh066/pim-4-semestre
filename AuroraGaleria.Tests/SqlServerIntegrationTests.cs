using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuroraGaleria.Models;
using AuroraGaleria.Services;
using Microsoft.EntityFrameworkCore;

namespace AuroraGaleria.Tests;

public class SqlServerIntegrationTests
{
    [Fact]
    public async Task ProceduresPreventOversellingAndCancellationIsAuthorizedAndIdempotent()
    {
        await using var store = new SqlServerTestDatabase();
        await store.InitializeAsync();
        var ids = await SeedAsync(store);
        var clock = new FixedClock();

        await using var firstDb = store.CreateContext();
        await using var secondDb = store.CreateContext();
        var first = new ReservationService(firstDb, clock);
        var second = new ReservationService(secondDb, clock);
        var results = await Task.WhenAll(
            first.ReserveAsync(ids.User1, ids.Session1, 1, ReservationTypes.Ticket, "last-seat-a"),
            second.ReserveAsync(ids.User2, ids.Session1, 1, ReservationTypes.Appointment, "last-seat-b"));

        Assert.Single(results, item => item.Success);
        Assert.Single(results, item => item.Code == "capacity");
        var winner = results.Single(item => item.Success).Value!;
        var owner = winner.UserId;
        var other = owner == ids.User1 ? ids.User2 : ids.User1;

        await using var cancellationDb = store.CreateContext();
        var cancellation = new ReservationService(cancellationDb, clock);
        Assert.Equal("not_found", (await cancellation.CancelAsync(other, winner.Id)).Code);
        Assert.True((await cancellation.CancelAsync(owner, winner.Id)).Success);
        Assert.True((await cancellation.CancelAsync(owner, winner.Id)).Success);
        cancellationDb.ChangeTracker.Clear();
        Assert.Equal(0, (await cancellationDb.VisitSessions.FindAsync(ids.Session1))!.ReservedQuantity);
    }

    [Fact]
    public async Task TriggerAuditsApplicationUpdateAndMultiRowUpdate()
    {
        await using var store = new SqlServerTestDatabase();
        await store.InitializeAsync();
        var ids = await SeedAsync(store);
        await using var db = store.CreateContext();
        var clock = new FixedClock();
        var service = new AdminCatalogService(db, clock);
        var session = await db.VisitSessions.AsNoTracking().SingleAsync(item => item.Id == ids.Session1);

        var updated = await service.UpdateSessionAsync(ids.Administrator, session.Id, new UpdateSessionAdminRequest
        {
            ExposureId = session.ExposureId, VisitDate = session.VisitDate,
            StartTime = session.StartTime, Capacity = 2
        });
        Assert.True(updated.Success);
        Assert.Contains(await db.AdminAuditLogs.AsNoTracking().ToListAsync(),
            item => item.EntityId == session.Id && item.AdministratorId == ids.Administrator && item.Action == "ChangeCapacity");

        await db.Database.OpenConnectionAsync();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'AuroraAdminId', @value={ids.Administrator}");
            await db.Database.ExecuteSqlRawAsync("UPDATE visit_sessions SET Capacity = Capacity + 3 WHERE Id IN ({0}, {1})", ids.Session1, ids.Session2);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'AuroraAdminId', @value=NULL");
            await db.Database.CloseConnectionAsync();
        }

        var multiRowLogs = await db.AdminAuditLogs.AsNoTracking()
            .Where(item => (item.EntityId == ids.Session1 || item.EntityId == ids.Session2) && item.Action == "ChangeCapacity")
            .ToListAsync();
        Assert.True(multiRowLogs.Count >= 3);
        Assert.All(multiRowLogs, item => Assert.Equal(ids.Administrator, item.AdministratorId));
    }

    [Fact]
    public async Task AdministrativeCatalogCrudWorksOnSqlServer()
    {
        await using var store = new SqlServerTestDatabase();
        await store.InitializeAsync();
        var ids = await SeedAsync(store);
        await using var db = store.CreateContext();
        var service = new AdminCatalogService(db, new FixedClock());

        var exposure = await service.CreateExposureAsync(new ExposureAdminRequest
        {
            Title = "Nova exposição", Description = "Cadastro no SQL Server", ImageUrl = "/nova.jpg",
            Period = "2031", StartDate = new DateTime(2031, 1, 1), EndDate = new DateTime(2031, 12, 31)
        });
        Assert.True(exposure.Success);
        Assert.True((await service.UpdateExposureAsync(exposure.Value, new ExposureAdminRequest
        {
            Title = "Exposição atualizada", Description = "Edição no SQL Server", ImageUrl = "/atualizada.jpg",
            Period = "2031", StartDate = new DateTime(2031, 1, 1), EndDate = new DateTime(2031, 12, 31)
        })).Success);

        var product = await service.CreateProductAsync(new ProductAdminRequest
        {
            Name = "Catálogo SQL", Price = 49.90m, ImageUrl = "/catalogo.jpg"
        });
        Assert.True(product.Success);
        Assert.True((await service.UpdateProductAsync(product.Value, new ProductAdminRequest
        {
            Name = "Catálogo SQL atualizado", Price = 59.90m, ImageUrl = "/catalogo-atualizado.jpg"
        })).Success);

        var session = await service.CreateSessionAsync(ids.Administrator, new SessionAdminRequest
        {
            ExposureId = exposure.Value, VisitDate = new DateTime(2031, 6, 1),
            StartTime = new TimeSpan(11, 0, 0), Capacity = 20
        });
        Assert.True(session.Success);
        Assert.True((await service.UpdateSessionAsync(ids.Administrator, session.Value, new UpdateSessionAdminRequest
        {
            ExposureId = exposure.Value, VisitDate = new DateTime(2031, 6, 1),
            StartTime = new TimeSpan(12, 0, 0), Capacity = 25
        })).Success);

        db.ChangeTracker.Clear();
        Assert.Equal("Exposição atualizada", (await db.Exposicoes.FindAsync(exposure.Value))!.Titulo);
        Assert.Equal(59.90m, (await db.Produtos.FindAsync(product.Value))!.Preco);
        var savedSession = await db.VisitSessions.FindAsync(session.Value);
        Assert.Equal(new TimeSpan(12, 0, 0), savedSession!.StartTime);
        Assert.Equal(25, savedSession.Capacity);
    }

    [Fact]
    public async Task HttpFlowsUseSqlServerForAuthenticationReservationsCartAndAdministration()
    {
        using var factory = new SqlServerWebFactory();
        using var visitor = factory.CreateClient(new() { AllowAutoRedirect = false });
        var visitorEmail = $"sql-visitor-{Guid.NewGuid():N}@example.test";

        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendJson(visitor, HttpMethod.Post, "/auth/register", new
        {
            name = "Visitante SQL", email = visitorEmail, password = "senha-ficticia-123"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendJson(visitor, HttpMethod.Post, "/auth/login", new
        {
            email = visitorEmail, password = "senha-ficticia-123"
        })).StatusCode);

        var reservation = await SendJson(visitor, HttpMethod.Post, "/user-actions/tickets", new
        {
            sessionId = 1, quantity = 1, requestId = Guid.NewGuid().ToString("N")
        });
        Assert.Equal(HttpStatusCode.OK, reservation.StatusCode);
        var reservationId = (await reservation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await SendJson(visitor, HttpMethod.Delete, $"/user-actions/reservations/{reservationId}", new { })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await SendJson(visitor, HttpMethod.Post, "/cart/items", new
        {
            productId = 1, quantity = 1, requestId = Guid.NewGuid().ToString("N")
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendJson(visitor, HttpMethod.Post, "/cart/checkout", new
        {
            requestId = Guid.NewGuid().ToString("N")
        })).StatusCode);
        var history = await visitor.GetFromJsonAsync<JsonElement>("/user-actions/purchases");
        var orderId = history[0].GetProperty("orderId").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await SendJson(visitor, HttpMethod.Delete, $"/user-actions/purchases/{orderId}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendJson(visitor, HttpMethod.Post, "/auth/logout", new { })).StatusCode);

        using var administrator = factory.CreateClient(new() { AllowAutoRedirect = false });
        var adminEmail = $"sql-admin-{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.OK, (await SendJson(administrator, HttpMethod.Post, "/auth/register", new
        {
            name = "Admin SQL", email = adminEmail, password = "senha-ficticia-123",
            registerAsAdministrator = true, adminAccessCode = "123"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendJson(administrator, HttpMethod.Post, "/auth/login", new
        {
            email = adminEmail, password = "senha-ficticia-123"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await administrator.GetAsync("/admin")).StatusCode);
    }

    private static async Task<(int User1, int User2, int Administrator, int Session1, int Session2)> SeedAsync(SqlServerTestDatabase store)
    {
        await using var db = store.CreateContext();
        var users = new[]
        {
            new User { Name = "Um", Email = $"um-{Guid.NewGuid():N}@example.test", PasswordHash = "hash", Role = UserRoles.Visitor },
            new User { Name = "Dois", Email = $"dois-{Guid.NewGuid():N}@example.test", PasswordHash = "hash", Role = UserRoles.Visitor },
            new User { Name = "Admin", Email = $"admin-{Guid.NewGuid():N}@example.test", PasswordHash = "hash", Role = UserRoles.Administrator }
        };
        db.Users.AddRange(users);
        var exposure = new Exposicao
        {
            Titulo = "Exposição SQL", Descricao = "Teste", ImagemUrl = "/test.jpg", Periodo = "2030",
            Inicio = new DateTime(2030, 1, 1), Fim = new DateTime(2030, 12, 31), Ativa = true
        };
        db.Exposicoes.Add(exposure);
        await db.SaveChangesAsync();
        var sessions = new[]
        {
            new VisitSession { ExposureId = exposure.Id, VisitDate = new DateTime(2030, 6, 1), StartTime = new TimeSpan(10, 0, 0), Capacity = 1 },
            new VisitSession { ExposureId = exposure.Id, VisitDate = new DateTime(2030, 6, 1), StartTime = new TimeSpan(14, 0, 0), Capacity = 4 }
        };
        db.VisitSessions.AddRange(sessions);
        await db.SaveChangesAsync();
        return (users[0].Id, users[1].Id, users[2].Id, sessions[0].Id, sessions[1].Id);
    }

    private static async Task<HttpResponseMessage> SendJson(HttpClient client, HttpMethod method, string url, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/auth/csrf");
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    private sealed class FixedClock : IGalleryClock
    {
        public DateTimeOffset UtcNow => new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset GalleryNow => new(2030, 1, 1, 9, 0, 0, TimeSpan.FromHours(-3));
        public DateTimeOffset SessionStart(DateTime date, TimeSpan time) =>
            new(DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified), TimeSpan.FromHours(-3));
    }
}
