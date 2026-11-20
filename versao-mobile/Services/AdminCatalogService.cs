using AuroraGaleria.Data;
using AuroraGaleria.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AuroraGaleria.Services;

public interface IAdminCatalogService
{
    Task<AdminDashboardViewModel> GetDashboardAsync();
    Task<ServiceResult<int>> CreateExposureAsync(ExposureAdminRequest request);
    Task<ServiceResult<int>> UpdateExposureAsync(int id, ExposureAdminRequest request);
    Task<ServiceResult<int>> ToggleExposureAsync(int id);
    Task<ServiceResult<int>> CreateProductAsync(ProductAdminRequest request);
    Task<ServiceResult<int>> UpdateProductAsync(int id, ProductAdminRequest request);
    Task<ServiceResult<int>> ToggleProductAsync(int id);
    Task<ServiceResult<int>> CreateSessionAsync(int administratorId, SessionAdminRequest request);
    Task<ServiceResult<int>> UpdateSessionAsync(int administratorId, int id, UpdateSessionAdminRequest request);
    Task<ServiceResult<int>> ToggleSessionAsync(int administratorId, int id);
    Task<ServiceResult<int>> CancelSessionAsync(int administratorId, int id, string reason);
}

public sealed class AdminCatalogService(AppDbContext db, IGalleryClock clock) : IAdminCatalogService
{
    public async Task<AdminDashboardViewModel> GetDashboardAsync()
    {
        var candidates = await db.VisitSessions.Where(item => item.Status == SessionStatuses.Open || item.Status == SessionStatuses.Closed).ToListAsync();
        foreach (var candidate in candidates.Where(item => clock.SessionStart(item.VisitDate, item.StartTime) <= clock.GalleryNow))
        {
            candidate.Status = SessionStatuses.Completed;
            candidate.IsActive = false;
        }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
        var sessions = await db.VisitSessions.AsNoTracking().Include(item => item.Exposure)
            .OrderBy(item => item.VisitDate).ToListAsync();
        return new AdminDashboardViewModel
        {
            Exposures = await db.Exposicoes.AsNoTracking().OrderBy(item => item.Inicio).ToListAsync(),
            Products = await db.Produtos.AsNoTracking().OrderBy(item => item.Nome).ToListAsync(),
            Sessions = sessions.OrderBy(item => item.VisitDate).ThenBy(item => item.StartTime).ToList(),
            DeletionRequests = await db.AccountDeletionRequests.AsNoTracking().Include(item => item.User).Where(item => item.Status == DeletionRequestStatuses.Pending).OrderBy(item => item.RequestedAtUtc).ToListAsync(),
            AuditLogs = await db.AdminAuditLogs.AsNoTracking().OrderByDescending(item => item.OccurredAtUtc).Take(50).ToListAsync()
        };
    }

    public async Task<ServiceResult<int>> CreateExposureAsync(ExposureAdminRequest request)
    {
        var validation = ValidateExposure(request);
        if (validation is not null) return ServiceResult<int>.Fail("invalid", validation);
        var exposure = new Exposicao
        {
            Titulo = request.Title.Trim(), Descricao = request.Description.Trim(), ImagemUrl = request.ImageUrl.Trim(),
            Periodo = request.Period.Trim(), Inicio = request.StartDate.Date, Fim = request.EndDate.Date, Ativa = true
        };
        db.Exposicoes.Add(exposure);
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(exposure.Id);
    }

    public async Task<ServiceResult<int>> UpdateExposureAsync(int id, ExposureAdminRequest request)
    {
        var validation = ValidateExposure(request);
        if (validation is not null) return ServiceResult<int>.Fail("invalid", validation);
        var exposure = await db.Exposicoes.SingleOrDefaultAsync(item => item.Id == id);
        if (exposure is null) return ServiceResult<int>.Fail("not_found", "Exposição não encontrada.");
        if (await db.VisitSessions.AnyAsync(item => item.ExposureId == id && (item.VisitDate < request.StartDate.Date || item.VisitDate > request.EndDate.Date)))
            return ServiceResult<int>.Fail("conflict", "O novo período deixaria sessões cadastradas fora da exposição. Ajuste ou desative essas sessões primeiro.");

        exposure.Titulo = request.Title.Trim(); exposure.Descricao = request.Description.Trim(); exposure.ImagemUrl = request.ImageUrl.Trim();
        exposure.Periodo = request.Period.Trim(); exposure.Inicio = request.StartDate.Date; exposure.Fim = request.EndDate.Date;
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(exposure.Id);
    }

    public async Task<ServiceResult<int>> ToggleExposureAsync(int id)
    {
        var exposure = await db.Exposicoes.SingleOrDefaultAsync(item => item.Id == id);
        if (exposure is null) return ServiceResult<int>.Fail("not_found", "Exposição não encontrada.");
        exposure.Ativa = !exposure.Ativa;
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(id);
    }

    public async Task<ServiceResult<int>> CreateProductAsync(ProductAdminRequest request)
    {
        var validation = ValidateProduct(request);
        if (validation is not null) return ServiceResult<int>.Fail("invalid", validation);
        var product = new Produto { Nome = request.Name.Trim(), Preco = request.Price, ImagemUrl = request.ImageUrl.Trim(), Ativo = true };
        db.Produtos.Add(product);
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(product.Id);
    }

    public async Task<ServiceResult<int>> UpdateProductAsync(int id, ProductAdminRequest request)
    {
        var validation = ValidateProduct(request);
        if (validation is not null) return ServiceResult<int>.Fail("invalid", validation);
        var product = await db.Produtos.SingleOrDefaultAsync(item => item.Id == id);
        if (product is null) return ServiceResult<int>.Fail("not_found", "Produto não encontrado.");
        product.Nome = request.Name.Trim(); product.Preco = request.Price; product.ImagemUrl = request.ImageUrl.Trim();
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(id);
    }

    public async Task<ServiceResult<int>> ToggleProductAsync(int id)
    {
        var product = await db.Produtos.SingleOrDefaultAsync(item => item.Id == id);
        if (product is null) return ServiceResult<int>.Fail("not_found", "Produto não encontrado.");
        product.Ativo = !product.Ativo;
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(id);
    }

    public async Task<ServiceResult<int>> CreateSessionAsync(int administratorId, SessionAdminRequest request)
    {
        var exposure = await db.Exposicoes.SingleOrDefaultAsync(item => item.Id == request.ExposureId);
        var today = clock.GalleryNow.Date;
        if (request.Capacity < 1 || exposure is null || !exposure.Ativa || request.VisitDate.Date < today ||
            request.VisitDate.Date < exposure.Inicio.Date || request.VisitDate.Date > exposure.Fim.Date)
            return ServiceResult<int>.Fail("invalid", "A sessão deve ter capacidade positiva, pertencer a uma exposição ativa, estar no período dela e não estar no passado.");
        if (clock.SessionStart(request.VisitDate, request.StartTime) <= clock.GalleryNow)
            return ServiceResult<int>.Fail("invalid", "A sessão deve começar no futuro, no Horário de São Paulo.");
        if (await db.VisitSessions.AnyAsync(item => item.ExposureId == request.ExposureId && item.VisitDate == request.VisitDate.Date && item.StartTime == request.StartTime))
            return ServiceResult<int>.Fail("conflict", "Já existe uma sessão neste dia e horário.");

        var session = new VisitSession { ExposureId = request.ExposureId, VisitDate = request.VisitDate.Date, StartTime = request.StartTime, Capacity = request.Capacity, IsActive = true, Status = SessionStatuses.Open };
        db.VisitSessions.Add(session);
        await db.SaveChangesAsync();
        await AuditAsync(administratorId, "Create", session.Id, null, new { session.ExposureId, session.VisitDate, session.StartTime, session.Capacity });
        return ServiceResult<int>.Ok(session.Id);
    }

    public async Task<ServiceResult<int>> UpdateSessionAsync(int administratorId, int id, UpdateSessionAdminRequest request)
    {
        db.ChangeTracker.Clear();
        var session = await db.VisitSessions.SingleOrDefaultAsync(item => item.Id == id);
        if (session is null) return ServiceResult<int>.Fail("not_found", "Sessão não encontrada.");
        if (session.Status is SessionStatuses.Cancelled or SessionStatuses.Completed)
            return ServiceResult<int>.Fail("conflict", "Sessões canceladas ou concluídas não podem ser alteradas.");
        if (request.Capacity < session.ReservedQuantity)
            return ServiceResult<int>.Fail("capacity", "A capacidade não pode ser menor que a quantidade de vagas reservadas.");
        var identityChanged = session.ExposureId != request.ExposureId || session.VisitDate.Date != request.VisitDate.Date || session.StartTime != request.StartTime;
        if (identityChanged && session.ReservedQuantity > 0)
            return ServiceResult<int>.Fail("reserved", "Esta sessão possui reservas. Para alterar a data ou o horário, cancele-a e crie outra.");
        var exposure = await db.Exposicoes.SingleOrDefaultAsync(item => item.Id == request.ExposureId && item.Ativa);
        if (exposure is null || request.VisitDate.Date < exposure.Inicio.Date || request.VisitDate.Date > exposure.Fim.Date)
            return ServiceResult<int>.Fail("invalid", "A data deve estar dentro do período de uma exposição ativa.");
        if (clock.SessionStart(request.VisitDate, request.StartTime) <= clock.GalleryNow)
            return ServiceResult<int>.Fail("invalid", "A sessão deve começar no futuro, no Horário de São Paulo.");
        if (identityChanged && await db.VisitSessions.AnyAsync(item => item.Id != id && item.ExposureId == request.ExposureId && item.VisitDate == request.VisitDate.Date && item.StartTime == request.StartTime))
            return ServiceResult<int>.Fail("conflict", "Já existe uma sessão neste dia e horário.");
        var previous = new { session.ExposureId, session.VisitDate, session.StartTime, session.Capacity };
        session.ExposureId = request.ExposureId; session.VisitDate = request.VisitDate.Date; session.StartTime = request.StartTime; session.Capacity = request.Capacity;
        await SaveSessionChangesAsync(administratorId);
        if (!db.Database.IsSqlServer())
            await AuditAsync(administratorId, "Update", id, null, new { session.ExposureId, session.VisitDate, session.StartTime, session.Capacity }, previous);
        return ServiceResult<int>.Ok(id);
    }

    public async Task<ServiceResult<int>> ToggleSessionAsync(int administratorId, int id)
    {
        db.ChangeTracker.Clear();
        var session = await db.VisitSessions.SingleOrDefaultAsync(item => item.Id == id);
        if (session is null) return ServiceResult<int>.Fail("not_found", "Sessão não encontrada.");
        if (session.Status is SessionStatuses.Cancelled or SessionStatuses.Completed)
            return ServiceResult<int>.Fail("conflict", "Sessões canceladas ou concluídas não podem ser reabertas.");
        var previous = session.Status;
        session.Status = session.Status == SessionStatuses.Open ? SessionStatuses.Closed : SessionStatuses.Open;
        session.IsActive = session.Status == SessionStatuses.Open;
        await SaveSessionChangesAsync(administratorId);
        if (!db.Database.IsSqlServer())
            await AuditAsync(administratorId, session.Status == SessionStatuses.Open ? "ReopenReservations" : "CloseReservations", id, null, new { session.Status }, new { Status = previous });
        return ServiceResult<int>.Ok(id);
    }

    public async Task<ServiceResult<int>> CancelSessionAsync(int administratorId, int id, string reason)
    {
        reason = reason.Trim();
        if (reason.Length < 3) return ServiceResult<int>.Fail("invalid", "Informe o motivo do cancelamento da sessão.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        db.ChangeTracker.Clear();
        var session = await db.VisitSessions.Include(item => item.Reservations).SingleOrDefaultAsync(item => item.Id == id);
        if (session is null) return ServiceResult<int>.Fail("not_found", "Sessão não encontrada.");
        if (session.Status == SessionStatuses.Cancelled) return ServiceResult<int>.Ok(id, "A sessão já estava cancelada.");
        foreach (var reservation in session.Reservations.Where(item => item.Status == ReservationStatuses.Confirmed))
        {
            reservation.Status = ReservationStatuses.Cancelled;
            reservation.CancelledAtUtc = clock.UtcNow.UtcDateTime;
            reservation.CancellationSource = CancellationSources.Gallery;
            reservation.CancellationReason = reason;
        }
        var previous = new { session.Status, session.ReservedQuantity };
        session.ReservedQuantity = 0; session.Status = SessionStatuses.Cancelled; session.IsActive = false;
        session.CancellationReason = reason; session.CancelledAtUtc = clock.UtcNow.UtcDateTime;
        await SaveSessionChangesAsync(administratorId);
        if (!db.Database.IsSqlServer())
            await AuditAsync(administratorId, "Cancel", id, reason, new { session.Status, session.ReservedQuantity }, previous);
        await tx.CommitAsync();
        return ServiceResult<int>.Ok(id);
    }

    private async Task AuditAsync(int administratorId, string action, int entityId, string? reason, object newValues, object? previousValues = null)
    {
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdministratorId = administratorId, Action = action, EntityType = "VisitSession", EntityId = entityId,
            OccurredAtUtc = clock.UtcNow.UtcDateTime, Reason = reason,
            PreviousValues = previousValues is null ? null : System.Text.Json.JsonSerializer.Serialize(previousValues),
            NewValues = System.Text.Json.JsonSerializer.Serialize(newValues)
        });
        await db.SaveChangesAsync();
    }

    private async Task SaveSessionChangesAsync(int administratorId)
    {
        if (!db.Database.IsSqlServer())
        {
            await db.SaveChangesAsync();
            return;
        }

        var connection = db.Database.GetDbConnection();
        var closeAfter = connection.State != ConnectionState.Open;
        if (closeAfter) await connection.OpenAsync();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'AuroraAdminId', @value={administratorId}");
            await db.SaveChangesAsync();
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'AuroraAdminId', @value=NULL");
            if (closeAfter) await connection.CloseAsync();
        }
    }

    private static string? ValidateExposure(ExposureAdminRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.ImageUrl) || string.IsNullOrWhiteSpace(request.Period))
            return "Título, imagem e período são obrigatórios.";
        return request.StartDate == default || request.EndDate.Date < request.StartDate.Date ? "O período da exposição é inválido." : null;
    }

    private static string? ValidateProduct(ProductAdminRequest request)
    {
        return string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ImageUrl) || request.Price <= 0
            ? "Nome, imagem e preço positivo são obrigatórios." : null;
    }
}
