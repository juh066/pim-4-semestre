using System.Data;
using System.Data.Common;
using AuroraGaleria.Data;
using AuroraGaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace AuroraGaleria.Services;

public interface IReservationService
{
    Task<ServiceResult<VisitReservation>> ReserveAsync(int userId, int sessionId, int quantity, string type, string requestId);
    Task<ServiceResult<bool>> CancelAsync(int userId, int reservationId);
}

public sealed class ReservationService(AppDbContext db, IGalleryClock clock) : IReservationService
{
    public async Task<ServiceResult<VisitReservation>> ReserveAsync(int userId, int sessionId, int quantity, string type, string requestId)
    {
        if (quantity < 1 || quantity > 20)
        {
            return ServiceResult<VisitReservation>.Fail("invalid_quantity", "Informe uma quantidade entre 1 e 20.");
        }
        if (type is not ReservationTypes.Ticket and not ReservationTypes.Appointment || string.IsNullOrWhiteSpace(requestId))
            return ServiceResult<VisitReservation>.Fail("invalid", "A solicitação de reserva é inválida.");

        if (db.Database.IsSqlServer())
            return await ReserveWithSqlServerAsync(userId, sessionId, quantity, type, requestId);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var previous = await db.VisitReservations.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId && item.RequestId == requestId);
        if (previous is not null)
        {
            return ServiceResult<VisitReservation>.Ok(previous, "Solicitação já processada.");
        }

        var session = await db.VisitSessions.Include(item => item.Exposure).SingleOrDefaultAsync(item => item.Id == sessionId);
        var today = clock.GalleryNow.Date;
        if (session?.Exposure is null || !session.IsActive || session.Status != SessionStatuses.Open || !session.Exposure.Ativa)
        {
            return ServiceResult<VisitReservation>.Fail("closed", "As reservas para esta sessão estão encerradas.");
        }
        if (session.VisitDate.Date < today || session.VisitDate.Date < session.Exposure.Inicio.Date || session.VisitDate.Date > session.Exposure.Fim.Date)
        {
            return ServiceResult<VisitReservation>.Fail("invalid_date", "Não é possível reservar uma data passada ou fora do período da exposição.");
        }
        if (clock.SessionStart(session.VisitDate, session.StartTime) <= clock.GalleryNow)
            return ServiceResult<VisitReservation>.Fail("started", "Esta sessão já começou e não aceita novas reservas.");

        var affected = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE visit_sessions SET ReservedQuantity = ReservedQuantity + {quantity} WHERE Id = {sessionId} AND IsActive = 1 AND ReservedQuantity + {quantity} <= Capacity");
        if (affected != 1)
        {
            return ServiceResult<VisitReservation>.Fail("capacity", "Não há vagas suficientes nesta sessão.");
        }

        var reservation = new VisitReservation
        {
            UserId = userId, VisitSessionId = session.Id, Type = type, Quantity = quantity,
            Status = ReservationStatuses.Confirmed, RequestId = requestId,
            ExposureName = session.Exposure.Titulo, VisitDate = session.VisitDate,
            StartTime = session.StartTime, CreatedAt = clock.UtcNow.UtcDateTime
        };
        db.VisitReservations.Add(reservation);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ServiceResult<VisitReservation>.Ok(reservation);
    }

    public async Task<ServiceResult<bool>> CancelAsync(int userId, int reservationId)
    {
        if (db.Database.IsSqlServer())
            return await CancelWithSqlServerAsync(userId, reservationId);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var reservation = await db.VisitReservations.SingleOrDefaultAsync(item => item.Id == reservationId && item.UserId == userId);
        if (reservation is null)
        {
            return ServiceResult<bool>.Fail("not_found", "Reserva não encontrada.");
        }
        if (reservation.Status == ReservationStatuses.Cancelled)
        {
            return ServiceResult<bool>.Ok(true, "Reserva já estava cancelada.");
        }
        if (clock.SessionStart(reservation.VisitDate, reservation.StartTime) <= clock.GalleryNow)
            return ServiceResult<bool>.Fail("started", "Esta sessão já começou e não pode mais ser cancelada.");

        var affected = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE visit_sessions SET ReservedQuantity = ReservedQuantity - {reservation.Quantity} WHERE Id = {reservation.VisitSessionId} AND ReservedQuantity >= {reservation.Quantity}");
        if (affected != 1)
        {
            return ServiceResult<bool>.Fail("consistency", "Não foi possível atualizar a disponibilidade.");
        }

        reservation.Status = ReservationStatuses.Cancelled;
        reservation.CancelledAtUtc = clock.UtcNow.UtcDateTime;
        reservation.CancellationSource = CancellationSources.Visitor;
        reservation.CancellationReason = null;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ServiceResult<bool>.Ok(true);
    }

    private async Task<ServiceResult<VisitReservation>> ReserveWithSqlServerAsync(int userId, int sessionId, int quantity, string type, string requestId)
    {
        var result = await ExecuteProcedureAsync("dbo.sp_ReserveVisitSession", command =>
        {
            Add(command, "@UserId", DbType.Int32, userId);
            Add(command, "@SessionId", DbType.Int32, sessionId);
            Add(command, "@Quantity", DbType.Int32, quantity);
            Add(command, "@Type", DbType.String, type);
            Add(command, "@RequestId", DbType.String, requestId);
            Add(command, "@CreatedAtUtc", DbType.DateTime2, clock.UtcNow.UtcDateTime);
            Add(command, "@GalleryNow", DbType.DateTime2, clock.GalleryNow.DateTime);
        });
        if (result.Id is int reservationId && result.Code is "ok" or "duplicate")
        {
            var reservation = await db.VisitReservations.AsNoTracking().SingleAsync(item => item.Id == reservationId);
            return ServiceResult<VisitReservation>.Ok(reservation, result.Message);
        }
        return ServiceResult<VisitReservation>.Fail(result.Code, result.Message);
    }

    private async Task<ServiceResult<bool>> CancelWithSqlServerAsync(int userId, int reservationId)
    {
        var result = await ExecuteProcedureAsync("dbo.sp_CancelVisitReservation", command =>
        {
            Add(command, "@UserId", DbType.Int32, userId);
            Add(command, "@ReservationId", DbType.Int32, reservationId);
            Add(command, "@CancelledAtUtc", DbType.DateTime2, clock.UtcNow.UtcDateTime);
            Add(command, "@GalleryNow", DbType.DateTime2, clock.GalleryNow.DateTime);
        });
        return result.Code is "ok" or "already_cancelled"
            ? ServiceResult<bool>.Ok(true, result.Message)
            : ServiceResult<bool>.Fail(result.Code, result.Message);
    }

    private async Task<ProcedureResult> ExecuteProcedureAsync(string procedure, Action<DbCommand> configure)
    {
        var connection = db.Database.GetDbConnection();
        var closeAfter = connection.State != ConnectionState.Open;
        if (closeAfter) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = procedure;
            command.CommandType = CommandType.StoredProcedure;
            configure(command);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return new ProcedureResult("error", "A operação não retornou um resultado.", null);
            return new ProcedureResult(
                reader.GetString(reader.GetOrdinal("ResultCode")),
                reader.GetString(reader.GetOrdinal("Message")),
                reader.IsDBNull(reader.GetOrdinal("ReservationId")) ? null : reader.GetInt32(reader.GetOrdinal("ReservationId")));
        }
        finally
        {
            if (closeAfter) await connection.CloseAsync();
        }
    }

    private static void Add(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record ProcedureResult(string Code, string Message, int? Id);
}
