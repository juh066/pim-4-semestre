using AuroraGaleria.Models;
using AuroraGaleria.Services;
using Microsoft.EntityFrameworkCore;

namespace AuroraGaleria.Tests;

public class BusinessRulesTests
{
    [Fact]
    public async Task CartRejectsMissingInactiveAndInvalidQuantity()
    {
        await using var store = new TestDatabase();
        var ids = await store.SeedAsync();
        await using var db = store.CreateContext();
        var service = new CartService(db);

        Assert.Equal("not_found", (await service.AddAsync(ids.User1, new() { ProductId = 999, Quantity = 1, RequestId = "missing" })).Code);
        Assert.Equal("invalid_quantity", (await service.AddAsync(ids.User1, new() { ProductId = ids.Product, Quantity = 21, RequestId = "quantity" })).Code);
        (await db.Produtos.FindAsync(ids.Product))!.Ativo = false; await db.SaveChangesAsync();
        Assert.Equal("inactive", (await service.AddAsync(ids.User1, new() { ProductId = ids.Product, Quantity = 1, RequestId = "inactive" })).Code);
    }

    [Fact]
    public async Task PriceChangeRequiresNewConfirmationAndHistoryKeepsConfirmedPrice()
    {
        await using var store = new TestDatabase(); var ids = await store.SeedAsync();
        await using var db = store.CreateContext(); var service = new CartService(db);
        Assert.True((await service.AddAsync(ids.User1, new() { ProductId = ids.Product, Quantity = 2, RequestId = "add" })).Success);
        (await db.Produtos.FindAsync(ids.Product))!.Preco = 75m; await db.SaveChangesAsync();

        var changed = await service.CheckoutAsync(ids.User1, new() { RequestId = "checkout-1" });
        Assert.False(changed.Success); Assert.Equal("price_changed", changed.Code); Assert.Empty(await db.PurchaseOrders.ToListAsync());
        var confirmed = await service.CheckoutAsync(ids.User1, new() { RequestId = "checkout-2" });
        Assert.True(confirmed.Success);
        var item = await db.PurchaseItems.SingleAsync();
        Assert.Equal(75m, item.UnitPrice); Assert.Equal(150m, item.TotalPrice);
    }

    [Fact]
    public async Task PastAndOutOfExposureDatesAreRejected()
    {
        await using var store = new TestDatabase(); var ids = await store.SeedAsync();
        await using var db = store.CreateContext();
        var session = await db.VisitSessions.FindAsync(ids.Session); var exposure = await db.Exposicoes.FindAsync(ids.Exposure);
        var service = new ReservationService(db, TestDatabase.CreateClock());
        session!.VisitDate = DateTime.Today.AddDays(-1); await db.SaveChangesAsync();
        Assert.Equal("invalid_date", (await service.ReserveAsync(ids.User1, ids.Session, 1, ReservationTypes.Ticket, "past")).Code);
        session.VisitDate = exposure!.Fim.AddDays(1); await db.SaveChangesAsync();
        Assert.Equal("invalid_date", (await service.ReserveAsync(ids.User1, ids.Session, 1, ReservationTypes.Ticket, "outside")).Code);
    }

    [Fact]
    public async Task OwnershipCancellationAndAvailabilityAreConsistent()
    {
        await using var store = new TestDatabase(); var ids = await store.SeedAsync();
        await using var db = store.CreateContext(); var service = new ReservationService(db, TestDatabase.CreateClock());
        var reservation = await service.ReserveAsync(ids.User1, ids.Session, 3, ReservationTypes.Ticket, "reserve");
        Assert.True(reservation.Success);
        Assert.Equal("not_found", (await service.CancelAsync(ids.User2, reservation.Value!.Id)).Code);
        Assert.True((await service.CancelAsync(ids.User1, reservation.Value.Id)).Success);
        db.ChangeTracker.Clear(); Assert.Equal(0, (await db.VisitSessions.FindAsync(ids.Session))!.ReservedQuantity);
    }

    [Fact]
    public async Task ConcurrentReservationsNeverExceedCapacity()
    {
        await using var store = new TestDatabase(); var ids = await store.SeedAsync(capacity: 5);
        await using var db1 = store.CreateContext(); await using var db2 = store.CreateContext();
        var first = new ReservationService(db1, TestDatabase.CreateClock()).ReserveAsync(ids.User1, ids.Session, 4, ReservationTypes.Ticket, "one");
        var second = new ReservationService(db2, TestDatabase.CreateClock()).ReserveAsync(ids.User2, ids.Session, 4, ReservationTypes.Ticket, "two");
        var results = await Task.WhenAll(first, second);
        Assert.Single(results, item => item.Success);
        await using var check = store.CreateContext();
        Assert.True((await check.VisitSessions.FindAsync(ids.Session))!.ReservedQuantity <= 5);
    }

    [Fact]
    public async Task RepeatedRequestsAreIdempotent()
    {
        await using var store = new TestDatabase(); var ids = await store.SeedAsync();
        await using var db = store.CreateContext(); var cart = new CartService(db); var reservations = new ReservationService(db, TestDatabase.CreateClock());
        var request = new AddCartItemRequest { ProductId = ids.Product, Quantity = 1, RequestId = "same-add" };
        await cart.AddAsync(ids.User1, request); await cart.AddAsync(ids.User1, request);
        Assert.Equal(1, (await cart.GetAsync(ids.User1)).Count);
        var checkout = await cart.CheckoutAsync(ids.User1, new() { RequestId = "same-checkout" });
        var repeatedCheckout = await cart.CheckoutAsync(ids.User1, new() { RequestId = "same-checkout" });
        Assert.True(repeatedCheckout.Value!.AlreadyProcessed);
        Assert.Equal(checkout.Value!.OrderId, repeatedCheckout.Value.OrderId);
        Assert.Equal(1, await db.PurchaseOrders.CountAsync());
        var first = await reservations.ReserveAsync(ids.User1, ids.Session, 1, ReservationTypes.Appointment, "same-reservation");
        var repeated = await reservations.ReserveAsync(ids.User1, ids.Session, 1, ReservationTypes.Appointment, "same-reservation");
        Assert.Equal(first.Value!.Id, repeated.Value!.Id);
        Assert.Equal(1, await db.VisitReservations.CountAsync());
    }

    [Fact]
    public async Task ExposurePeriodCannotOrphanSessionsAndSessionsCanBeDisabled()
    {
        await using var store = new TestDatabase(); var ids = await store.SeedAsync();
        await using var db = store.CreateContext(); var service = new AdminCatalogService(db, TestDatabase.CreateClock());
        var exposure = await db.Exposicoes.FindAsync(ids.Exposure);
        var update = new ExposureAdminRequest
        {
            Title = exposure!.Titulo, Description = exposure.Descricao, ImageUrl = exposure.ImagemUrl, Period = exposure.Periodo,
            StartDate = DateTime.Today.AddDays(10), EndDate = DateTime.Today.AddDays(20)
        };
        Assert.Equal("conflict", (await service.UpdateExposureAsync(ids.Exposure, update)).Code);
        var existingSession = await db.VisitSessions.FindAsync(ids.Session);
        var duplicate = await service.CreateSessionAsync(ids.User1, new SessionAdminRequest
        {
            ExposureId = ids.Exposure, VisitDate = existingSession!.VisitDate,
            StartTime = existingSession.StartTime, Capacity = 10
        });
        Assert.Equal("conflict", duplicate.Code);
        Assert.True((await service.ToggleSessionAsync(ids.User1, ids.Session)).Success);
        Assert.False((await db.VisitSessions.FindAsync(ids.Session))!.IsActive);
    }
}
