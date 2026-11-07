using System.Security.Claims;
using AuroraGaleria.Data;
using AuroraGaleria.Models;
using AuroraGaleria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuroraGaleria.Controllers;

[ApiController, Authorize, Route("user-actions")]
public class UserActionsController(AppDbContext db, IReservationService reservations, ICartService cart, IGalleryClock clock) : ControllerBase
{
    [HttpGet("tickets")]
    public Task<IActionResult> Tickets() => ListReservations(ReservationTypes.Ticket);

    [HttpPost("tickets")]
    public async Task<IActionResult> BuyTicket(BuyTicketRequest request)
    {
        var result = await reservations.ReserveAsync(UserId, request.SessionId, request.Quantity, ReservationTypes.Ticket, request.RequestId);
        return ReservationResponse(result, "Ingresso reservado com sucesso.");
    }

    [HttpGet("appointments")]
    public Task<IActionResult> Appointments() => ListReservations(ReservationTypes.Appointment);

    [HttpPost("appointments")]
    public async Task<IActionResult> Schedule(ScheduleAppointmentRequest request)
    {
        var result = await reservations.ReserveAsync(UserId, request.SessionId, 1, ReservationTypes.Appointment, request.RequestId);
        return ReservationResponse(result, "Visita agendada com sucesso.");
    }

    [HttpDelete("appointments/{id:int}"), HttpDelete("reservations/{id:int}")]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await reservations.CancelAsync(UserId, id);
        if (result.Success) return Ok(new { message = "Agendamento cancelado com sucesso." });
        if (result.Code == "not_found")
        {
            var legacy = await db.UserAppointments.SingleOrDefaultAsync(item => item.Id == id && item.UserId == UserId);
            if (legacy is not null && string.Equals(legacy.Status, "Cancelado", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { message = "Agendamento já estava cancelado." });
            }
            if (legacy is not null && legacy.AppointmentDate.Date <= clock.GalleryNow.Date)
            {
                return Conflict(new { message = "O horário do agendamento já começou ou não pode ser verificado com segurança." });
            }
            if (legacy is not null)
            {
                legacy.Status = "Cancelado";
                await db.SaveChangesAsync();
                return Ok(new { message = "Agendamento cancelado com sucesso." });
            }
        }
        return result.Code == "not_found" ? NotFound(new { message = result.Message }) : Conflict(new { message = result.Message });
    }

    [HttpGet("purchases")]
    public async Task<IActionResult> Purchases()
    {
        var orders = await db.PurchaseOrders.AsNoTracking().Where(order => order.UserId == UserId)
            .Include(order => order.Items).OrderByDescending(order => order.CreatedAt).ToListAsync();
        var current = orders.SelectMany(order => order.Items.Select(item => new
        {
            id = item.Id, orderId = order.Id, item.ProductName, item.Quantity, item.UnitPrice, item.TotalPrice,
            status = order.Status == PurchaseOrderStatuses.Cancelled ? "Cancelada" : "Confirmada",
            situation = order.Status == PurchaseOrderStatuses.Cancelled ? "Compra simulada cancelada" : "Compra registrada",
            canCancel = order.Status == PurchaseOrderStatuses.Confirmed, createdAt = order.CreatedAt.ToString("yyyy-MM-dd")
        })).Cast<object>().ToList();
        var legacy = await db.UserPurchases.AsNoTracking().Where(item => item.UserId == UserId).OrderByDescending(item => item.CreatedAt).ToListAsync();
        current.AddRange(legacy.Select(item => (object)new
        {
            id = item.Id, orderId = (int?)null, item.ProductName, item.Quantity,
            unitPrice = item.Quantity > 0 ? item.TotalPrice / item.Quantity : 0,
            item.TotalPrice, item.Status, situation = "Registro histórico legado", createdAt = item.CreatedAt.ToString("yyyy-MM-dd")
        }));
        return Ok(current);
    }

    [HttpDelete("purchases/{id:int}")]
    public async Task<IActionResult> CancelPurchase(int id)
    {
        var result = await cart.CancelOrderAsync(UserId, id);
        if (result.Success) return Ok(new { message = result.Message });
        return result.Code == "not_found" ? NotFound(new { message = result.Message }) : Conflict(new { message = result.Message });
    }

    [Authorize(Roles = UserRoles.Administrator), HttpGet("analytics")]
    public async Task<IActionResult> Analytics()
    {
        var sales = await db.PurchaseItems.AsNoTracking().GroupBy(item => item.ProductName)
            .Select(group => new { Produto = group.Key, Quantidade = group.Sum(item => item.Quantity), Receita = group.Sum(item => item.TotalPrice) })
            .OrderByDescending(item => item.Quantidade).ToListAsync();
        var tickets = await db.VisitReservations.AsNoTracking()
            .Where(item => item.Type == ReservationTypes.Ticket && item.Status == ReservationStatuses.Confirmed)
            .GroupBy(item => item.ExposureName)
            .Select(group => new { Exposicao = group.Key, Quantidade = group.Sum(item => item.Quantity) })
            .OrderByDescending(item => item.Quantidade).ToListAsync();
        return Ok(new { VendasPorProduto = sales, IngressosPorExposicao = tickets });
    }

    private async Task<IActionResult> ListReservations(string type)
    {
        var today = clock.GalleryNow.Date;
        var items = await db.VisitReservations.AsNoTracking().Where(item => item.UserId == UserId && item.Type == type)
            .OrderByDescending(item => item.VisitDate).ToListAsync();
        items = items.OrderByDescending(item => item.VisitDate).ThenBy(item => item.StartTime).ToList();
        var result = items.Select(item => new
        {
            item.Id, eventName = item.ExposureName, date = item.VisitDate.ToString("yyyy-MM-dd"),
            time = item.StartTime.ToString(@"hh\:mm"), item.Quantity,
            status = item.Status == ReservationStatuses.Cancelled ? "cancelado" : clock.SessionStart(item.VisitDate, item.StartTime) <= clock.GalleryNow ? "past" : "confirmado",
            cancellationSource = item.CancellationSource, cancellationReason = item.CancellationReason
        }).Cast<object>().ToList();
        if (type == ReservationTypes.Ticket)
        {
            var legacy = await db.UserTickets.AsNoTracking().Where(item => item.UserId == UserId).ToListAsync();
            result.AddRange(legacy.Select(item => (object)new { item.Id, eventName = item.EventName, date = item.EventDate.ToString("yyyy-MM-dd"), time = "--:--", item.Quantity, status = item.EventDate.Date < today ? "past" : item.Status }));
        }
        else
        {
            var legacy = await db.UserAppointments.AsNoTracking().Where(item => item.UserId == UserId).ToListAsync();
            result.AddRange(legacy.Select(item => (object)new { item.Id, eventName = item.EventName, date = item.AppointmentDate.ToString("yyyy-MM-dd"), time = "--:--", item.Quantity, status = item.Status.ToLowerInvariant() }));
        }
        return Ok(result);
    }

    private IActionResult ReservationResponse(ServiceResult<VisitReservation> result, string successMessage)
    {
        if (result.Success) return Ok(new { message = successMessage, id = result.Value!.Id });
        return result.Code switch
        {
            "not_found" => NotFound(new { message = result.Message }),
            "capacity" => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
