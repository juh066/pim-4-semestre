using AuroraGaleria.Configuration;
using AuroraGaleria.Models;
using AuroraGaleria.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuroraGaleria.Tests;

public class BusinessRulesPhaseTests
{
    [Fact]
    public async Task VisitorCancellationIsAllowedBeforeButBlockedAtSessionStartAndIsIdempotent()
    {
        await using var store = new TestDatabase(); var ids = await store.SeedAsync();
        await using var db = store.CreateContext();
        var session = await db.VisitSessions.FindAsync(ids.Session);
        session!.VisitDate = new DateTime(2030, 1, 10); session.StartTime = new TimeSpan(10, 0, 0); await db.SaveChangesAsync();
        var exposure = await db.Exposicoes.FindAsync(ids.Exposure);
        exposure!.Inicio = new DateTime(2030, 1, 1); exposure.Fim = new DateTime(2030, 1, 31); await db.SaveChangesAsync();
        var clock = new MutableClock(new DateTimeOffset(2030, 1, 10, 9, 59, 0, TimeSpan.FromHours(-3)));
        var service = new ReservationService(db, clock);
        var reservation = await service.ReserveAsync(ids.User1, ids.Session, 2, ReservationTypes.Ticket, "before-start");
        Assert.True(reservation.Success);
        clock.Now = new DateTimeOffset(2030, 1, 10, 10, 0, 0, TimeSpan.FromHours(-3));
        Assert.Equal("started", (await service.CancelAsync(ids.User1, reservation.Value!.Id)).Code);
        clock.Now = clock.Now.AddMinutes(-1);
        Assert.True((await service.CancelAsync(ids.User1, reservation.Value.Id)).Success);
        Assert.True((await service.CancelAsync(ids.User1, reservation.Value.Id)).Success);
        db.ChangeTracker.Clear();
        Assert.Equal(0, (await db.VisitSessions.FindAsync(ids.Session))!.ReservedQuantity);
    }

    [Fact]
    public async Task AdministrationProtectsCapacityAndIdentityAndCancelsWithReason()
    {
        await using var store = new TestDatabase(); var ids = await store.SeedAsync();
        await using var db = store.CreateContext(); var clock = TestDatabase.CreateClock();
        var reservations = new ReservationService(db, clock);
        Assert.True((await reservations.ReserveAsync(ids.User1, ids.Session, 3, ReservationTypes.Appointment, "reserved")).Success);
        var service = new AdminCatalogService(db, clock);
        var session = await db.VisitSessions.AsNoTracking().SingleAsync(item => item.Id == ids.Session);
        var below = await service.UpdateSessionAsync(ids.User2, ids.Session, new UpdateSessionAdminRequest { ExposureId = ids.Exposure, VisitDate = session.VisitDate, StartTime = session.StartTime, Capacity = 2 });
        Assert.Equal("capacity", below.Code);
        var moved = await service.UpdateSessionAsync(ids.User2, ids.Session, new UpdateSessionAdminRequest { ExposureId = ids.Exposure, VisitDate = session.VisitDate.AddDays(1), StartTime = session.StartTime, Capacity = 5 });
        Assert.Equal("reserved", moved.Code);
        Assert.True((await service.ToggleSessionAsync(ids.User2, ids.Session)).Success);
        Assert.Equal(3, (await db.VisitSessions.FindAsync(ids.Session))!.ReservedQuantity);
        Assert.True((await service.CancelSessionAsync(ids.User2, ids.Session, "Manutenção necessária")).Success);
        db.ChangeTracker.Clear();
        var cancelled = await db.VisitSessions.FindAsync(ids.Session);
        Assert.Equal(SessionStatuses.Cancelled, cancelled!.Status); Assert.Equal(0, cancelled.ReservedQuantity);
        var reservation = await db.VisitReservations.SingleAsync();
        Assert.Equal(CancellationSources.Gallery, reservation.CancellationSource);
        Assert.Equal("Manutenção necessária", reservation.CancellationReason);
    }

    [Fact]
    public async Task SimulatedOrderCancellationPreservesItemsAndIsIdempotent()
    {
        await using var store = new TestDatabase(); var ids = await store.SeedAsync();
        await using var db = store.CreateContext(); var cart = new CartService(db);
        await cart.AddAsync(ids.User1, new() { ProductId = ids.Product, Quantity = 2, RequestId = "add-order" });
        var checkout = await cart.CheckoutAsync(ids.User1, new() { RequestId = "checkout-order" });
        var orderId = checkout.Value!.OrderId!.Value;
        Assert.True((await cart.CancelOrderAsync(ids.User1, orderId)).Success);
        Assert.True((await cart.CancelOrderAsync(ids.User1, orderId)).Success);
        Assert.Equal(PurchaseOrderStatuses.Cancelled, (await db.PurchaseOrders.FindAsync(orderId))!.Status);
        Assert.Equal(1, await db.PurchaseItems.CountAsync());
        Assert.Equal("not_found", (await cart.CancelOrderAsync(ids.User2, orderId)).Code);
    }

    [Fact]
    public async Task AccountAnonymizationRevokesAccessAndReleasesFutureCapacity()
    {
        await using var store = new TestDatabase(); var ids = await store.SeedAsync();
        await using var db = store.CreateContext(); var clock = TestDatabase.CreateClock();
        var reservations = new ReservationService(db, clock);
        await reservations.ReserveAsync(ids.User1, ids.Session, 2, ReservationTypes.Ticket, "future-delete");
        var before = (await db.Users.FindAsync(ids.User1))!.SessionVersion;
        var privacy = new PrivacyService(db, clock);
        Assert.True((await privacy.AnonymizeUserAsync(ids.User1, ids.User2, "Teste de exclusão")).Success);
        db.ChangeTracker.Clear();
        var user = await db.Users.FindAsync(ids.User1);
        Assert.False(user!.IsActive); Assert.NotEqual(before, user.SessionVersion); Assert.DoesNotContain("um@example", user.Email);
        Assert.Equal(0, (await db.VisitSessions.FindAsync(ids.Session))!.ReservedQuantity);
    }

    [Fact]
    public async Task RetentionDoesNotRunWithoutEvaluationDate()
    {
        await using var store = new TestDatabase(); await store.SeedAsync();
        await using var db = store.CreateContext(); var clock = TestDatabase.CreateClock();
        var privacy = new PrivacyService(db, clock);
        var retention = new RetentionService(db, privacy, Options.Create(new RetentionOptions()), clock);
        var preview = await retention.PreviewAsync();
        Assert.False(preview.Configured);
        Assert.Equal("not_configured", (await retention.ExecuteAsync(true)).Code);
        Assert.Equal(2, await db.Users.CountAsync(item => item.IsActive));
    }

    [Fact]
    public void GalleryClockUsesSaoPauloRegardlessOfDeviceTimeZone()
    {
        var provider = new FixedTimeProvider(new DateTimeOffset(2026, 11, 7, 13, 0, 0, TimeSpan.Zero));
        var clock = new GalleryClock(provider, Options.Create(new GalleryOptions { TimeZoneId = "America/Sao_Paulo" }));
        Assert.Equal(10, clock.GalleryNow.Hour);
        Assert.Equal(TimeSpan.FromHours(-3), clock.GalleryNow.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.FromHours(-3)), clock.SessionStart(new DateTime(2026, 11, 7), new TimeSpan(10, 0, 0)));
    }

    private sealed class MutableClock(DateTimeOffset now) : IGalleryClock
    {
        public DateTimeOffset Now { get; set; } = now;
        public DateTimeOffset UtcNow => Now.ToUniversalTime();
        public DateTimeOffset GalleryNow => Now;
        public DateTimeOffset SessionStart(DateTime date, TimeSpan time) => new(DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified), Now.Offset);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
