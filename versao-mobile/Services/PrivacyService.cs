using System.Data;
using System.Text.Json;
using AuroraGaleria.Configuration;
using AuroraGaleria.Data;
using AuroraGaleria.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuroraGaleria.Services;

public interface IPrivacyService
{
    Task<ServiceResult<int>> RequestDeletionAsync(int userId);
    Task<IReadOnlyList<AccountDeletionRequest>> ListPendingAsync();
    Task<ServiceResult<bool>> CompleteDeletionAsync(int requestId, int? administratorId);
    Task<ServiceResult<bool>> AnonymizeUserAsync(int userId, int? administratorId, string reason);
}

public sealed class PrivacyService(AppDbContext db, IGalleryClock clock) : IPrivacyService
{
    public async Task<ServiceResult<int>> RequestDeletionAsync(int userId)
    {
        var existing = await db.AccountDeletionRequests.SingleOrDefaultAsync(item => item.UserId == userId && item.Status == DeletionRequestStatuses.Pending);
        if (existing is not null) return ServiceResult<int>.Ok(existing.Id, "A solicitação já está pendente.");
        if (!await db.Users.AnyAsync(item => item.Id == userId && item.IsActive)) return ServiceResult<int>.Fail("not_found", "Conta não encontrada.");
        var request = new AccountDeletionRequest { UserId = userId, RequestedAtUtc = clock.UtcNow.UtcDateTime };
        db.AccountDeletionRequests.Add(request);
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(request.Id, "Solicitação de exclusão enviada.");
    }

    public async Task<IReadOnlyList<AccountDeletionRequest>> ListPendingAsync() =>
        await db.AccountDeletionRequests.AsNoTracking().Include(item => item.User)
            .Where(item => item.Status == DeletionRequestStatuses.Pending).OrderBy(item => item.RequestedAtUtc).ToListAsync();

    public async Task<ServiceResult<bool>> CompleteDeletionAsync(int requestId, int? administratorId)
    {
        var request = await db.AccountDeletionRequests.SingleOrDefaultAsync(item => item.Id == requestId);
        if (request is null) return ServiceResult<bool>.Fail("not_found", "Solicitação não encontrada.");
        if (request.Status == DeletionRequestStatuses.Completed) return ServiceResult<bool>.Ok(true, "Solicitação já concluída.");
        var result = await AnonymizeUserAsync(request.UserId, administratorId, "Solicitação de exclusão da conta");
        if (!result.Success) return result;
        request.Status = DeletionRequestStatuses.Completed;
        request.CompletedAtUtc = clock.UtcNow.UtcDateTime;
        request.CompletedByAdministratorId = administratorId;
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true, "Conta excluída e históricos anonimizados.");
    }

    public async Task<ServiceResult<bool>> AnonymizeUserAsync(int userId, int? administratorId, string reason)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId);
        if (user is null) return ServiceResult<bool>.Fail("not_found", "Conta não encontrada.");
        if (!user.IsActive) return ServiceResult<bool>.Ok(true, "A conta já foi anonimizada.");

        var future = await db.VisitReservations.Where(item => item.UserId == userId && item.Status == ReservationStatuses.Confirmed).ToListAsync();
        foreach (var reservation in future)
        {
            var start = clock.SessionStart(reservation.VisitDate, reservation.StartTime);
            if (start <= clock.GalleryNow) continue;
            var released = await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE visit_sessions SET ReservedQuantity = ReservedQuantity - {reservation.Quantity} WHERE Id = {reservation.VisitSessionId} AND ReservedQuantity >= {reservation.Quantity}");
            if (released != 1) return ServiceResult<bool>.Fail("consistency", "Não foi possível liberar as vagas futuras da conta.");
            reservation.Status = ReservationStatuses.Cancelled;
            reservation.CancelledAtUtc = clock.UtcNow.UtcDateTime;
            reservation.CancellationSource = CancellationSources.Gallery;
            reservation.CancellationReason = "Conta excluída";
        }

        db.CartItems.RemoveRange(db.CartItems.Where(item => item.UserId == userId));
        db.IdempotencyRecords.RemoveRange(db.IdempotencyRecords.Where(item => item.UserId == userId));
        user.Name = "Conta removida";
        user.Email = $"removed-{Guid.NewGuid():N}@invalid.local";
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"));
        user.IsActive = false;
        user.SessionVersion = Guid.NewGuid().ToString("N");
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdministratorId = administratorId, Action = "AnonymizeAccount", EntityType = "User", EntityId = userId,
            OccurredAtUtc = clock.UtcNow.UtcDateTime, Reason = reason,
            NewValues = JsonSerializer.Serialize(new { IsActive = false, PersonalDataRemoved = true })
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ServiceResult<bool>.Ok(true);
    }
}

public sealed record RetentionPreview(bool Configured, bool Due, DateOnly? EvaluationDate, DateOnly? DueDate, int ActiveAccounts, string Message);

public interface IRetentionService
{
    Task<RetentionPreview> PreviewAsync();
    Task<ServiceResult<int>> ExecuteAsync(bool confirmed);
}

public sealed class RetentionService(AppDbContext db, IPrivacyService privacy, IOptions<RetentionOptions> options, IGalleryClock clock) : IRetentionService
{
    public async Task<RetentionPreview> PreviewAsync()
    {
        if (!DateOnly.TryParse(options.Value.FinalEvaluationDate, out var evaluation))
            return new(false, false, null, null, 0, "A data da avaliação final não está configurada; nenhuma limpeza será executada.");
        var dueDate = evaluation.AddDays(options.Value.PersonalDataRetentionDays);
        var count = await db.Users.CountAsync(item => item.IsActive);
        var due = DateOnly.FromDateTime(clock.GalleryNow.DateTime) >= dueDate;
        return new(true, due, evaluation, dueDate, count, due ? $"{count} conta(s) ativa(s) serão anonimizadas." : $"A limpeza somente poderá ocorrer em {dueDate:dd/MM/yyyy}.");
    }

    public async Task<ServiceResult<int>> ExecuteAsync(bool confirmed)
    {
        var preview = await PreviewAsync();
        if (!preview.Configured) return ServiceResult<int>.Fail("not_configured", preview.Message);
        if (!preview.Due) return ServiceResult<int>.Fail("not_due", preview.Message);
        if (!confirmed) return ServiceResult<int>.Fail("confirmation_required", "Use a confirmação explícita para executar a limpeza.");
        var ids = await db.Users.AsNoTracking().Where(item => item.IsActive).Select(item => item.Id).ToListAsync();
        var completed = 0;
        foreach (var id in ids)
        {
            var result = await privacy.AnonymizeUserAsync(id, null, "Rotina de retenção após avaliação");
            if (result.Success) completed++;
        }
        return ServiceResult<int>.Ok(completed, $"{completed} conta(s) anonimizadas.");
    }
}
