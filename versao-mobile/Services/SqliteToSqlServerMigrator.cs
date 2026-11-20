using System.Security.Cryptography;
using AuroraGaleria.Data;
using AuroraGaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace AuroraGaleria.Services;

public sealed class SqliteToSqlServerMigrator(
    AppDbContext target,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<SqliteToSqlServerMigrator> logger)
{
    private static readonly (string Name, Func<AppDbContext, Task<int>> Count)[] Tables =
    {
        ("Users", db => db.Users.CountAsync()),
        ("Exposicoes", db => db.Exposicoes.CountAsync()),
        ("Produtos", db => db.Produtos.CountAsync()),
        ("Ingressos", db => db.Ingressos.CountAsync()),
        ("visit_sessions", db => db.VisitSessions.CountAsync()),
        ("visit_reservations", db => db.VisitReservations.CountAsync()),
        ("cart_items", db => db.CartItems.CountAsync()),
        ("user_tickets", db => db.UserTickets.CountAsync()),
        ("user_appointments", db => db.UserAppointments.CountAsync()),
        ("purchase_orders", db => db.PurchaseOrders.CountAsync()),
        ("purchase_items", db => db.PurchaseItems.CountAsync()),
        ("user_purchases", db => db.UserPurchases.CountAsync()),
        ("idempotency_records", db => db.IdempotencyRecords.CountAsync()),
        ("account_deletion_requests", db => db.AccountDeletionRequests.CountAsync()),
        ("admin_audit_logs", db => db.AdminAuditLogs.CountAsync()),
        ("data_migration_issues", db => db.DataMigrationIssues.CountAsync())
    };

    public async Task<ServiceResult<bool>> ImportAsync()
    {
        if (!target.Database.IsSqlServer())
            return ServiceResult<bool>.Fail("provider", "A importação exige que o banco de destino seja SQL Server.");

        var sourcePath = GetSourcePath();
        if (!File.Exists(sourcePath))
            return ServiceResult<bool>.Fail("source", $"O SQLite de origem não foi encontrado em: {sourcePath}");

        await using var sourceStream = File.OpenRead(sourcePath);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(sourceStream));
        if (await target.DataImportHistories.AnyAsync(item => item.Status == DataImportStatuses.Completed))
            return ServiceResult<bool>.Fail("already_imported", "O SQL Server já possui uma importação concluída. Nenhum dado foi copiado.");
        if (await HasOperationalDataAsync())
            return ServiceResult<bool>.Fail("not_empty", "O SQL Server já contém dados operacionais. Use um banco novo ou restaure um backup antes de importar.");

        var history = new DataImportHistory
        {
            SourcePath = sourcePath,
            SourceSha256 = hash,
            Status = DataImportStatuses.Running,
            StartedAtUtc = DateTime.UtcNow
        };
        target.DataImportHistories.Add(history);
        await target.SaveChangesAsync();

        try
        {
            await using var source = CreateSourceContext(sourcePath);
            await using var transaction = await target.Database.BeginTransactionAsync();

            await CopyUsersAsync(source);
            await CopyExposuresAsync(source);
            await CopyProductsAsync(source);
            await CopyIngressoAsync(source);
            await CopySessionsAsync(source);
            await CopyReservationsAsync(source);
            await CopyCartAsync(source);
            await CopyLegacyHistoryAsync(source);
            await CopyOrdersAsync(source);
            await CopyIdempotencyAsync(source);
            await CopyDeletionRequestsAsync(source);
            await CopyAuditAsync(source);
            await CopyMigrationIssuesAsync(source);

            await transaction.CommitAsync();

            var verification = await CompareAsync(source);
            history = await target.DataImportHistories.SingleAsync(item => item.Id == history.Id);
            history.Status = verification.Success ? DataImportStatuses.Completed : DataImportStatuses.Failed;
            history.CompletedAtUtc = DateTime.UtcNow;
            history.Details = verification.Message;
            await target.SaveChangesAsync();

            return verification.Success
                ? ServiceResult<bool>.Ok(true, $"Importação concluída e conferida.\n{verification.Message}")
                : ServiceResult<bool>.Fail("verification", $"Os dados foram copiados, mas a conferência encontrou divergências.\n{verification.Message}");
        }
        catch (Exception exception)
        {
            target.ChangeTracker.Clear();
            history = await target.DataImportHistories.SingleAsync(item => item.Id == history.Id);
            history.Status = DataImportStatuses.Failed;
            history.CompletedAtUtc = DateTime.UtcNow;
            history.Details = exception.Message;
            await target.SaveChangesAsync();
            logger.LogError(exception, "Falha ao importar o SQLite para o SQL Server.");
            return ServiceResult<bool>.Fail("import_failed", $"A importação falhou e a transação foi revertida: {exception.Message}");
        }
    }

    public async Task<ServiceResult<bool>> VerifyAsync()
    {
        if (!target.Database.IsSqlServer())
            return ServiceResult<bool>.Fail("provider", "A conferência exige um destino SQL Server.");
        var sourcePath = GetSourcePath();
        if (!File.Exists(sourcePath))
            return ServiceResult<bool>.Fail("source", $"O SQLite de origem não foi encontrado em: {sourcePath}");
        await using var source = CreateSourceContext(sourcePath);
        return await CompareAsync(source);
    }

    private async Task<ServiceResult<bool>> CompareAsync(AppDbContext source)
    {
        var lines = new List<string>();
        var valid = true;
        foreach (var table in Tables)
        {
            var sourceCount = await table.Count(source);
            var targetCount = await table.Count(target);
            lines.Add($"{table.Name}: SQLite={sourceCount}, SQL Server={targetCount}");
            valid &= sourceCount == targetCount;
        }

        var sourceTotal = await source.PurchaseOrders.SumAsync(item => (decimal?)item.TotalPrice) ?? 0;
        var targetTotal = await target.PurchaseOrders.SumAsync(item => (decimal?)item.TotalPrice) ?? 0;
        var sourceCancelled = await source.VisitReservations.CountAsync(item => item.Status == ReservationStatuses.Cancelled);
        var targetCancelled = await target.VisitReservations.CountAsync(item => item.Status == ReservationStatuses.Cancelled);
        var sourceOccupancy = await source.VisitSessions.SumAsync(item => (int?)item.ReservedQuantity) ?? 0;
        var targetOccupancy = await target.VisitSessions.SumAsync(item => (int?)item.ReservedQuantity) ?? 0;
        var sourceUsers = await source.Users.AsNoTracking().OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.Email, item.PasswordHash, item.Role }).ToListAsync();
        var targetUsers = await target.Users.AsNoTracking().OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.Email, item.PasswordHash, item.Role }).ToListAsync();

        valid &= sourceTotal == targetTotal;
        valid &= sourceCancelled == targetCancelled;
        valid &= sourceOccupancy == targetOccupancy;
        valid &= sourceUsers.SequenceEqual(targetUsers);
        lines.Add($"Totais de pedidos: SQLite={sourceTotal:F2}, SQL Server={targetTotal:F2}");
        lines.Add($"Reservas canceladas: SQLite={sourceCancelled}, SQL Server={targetCancelled}");
        lines.Add($"Ocupação total: SQLite={sourceOccupancy}, SQL Server={targetOccupancy}");
        lines.Add($"Identificadores, e-mails, hashes e perfis: {(sourceUsers.SequenceEqual(targetUsers) ? "iguais" : "divergentes")}");
        return valid
            ? ServiceResult<bool>.Ok(true, string.Join(Environment.NewLine, lines))
            : ServiceResult<bool>.Fail("divergence", string.Join(Environment.NewLine, lines));
    }

    private async Task<bool> HasOperationalDataAsync() =>
        await target.Users.AnyAsync() || await target.Exposicoes.AnyAsync() || await target.Produtos.AnyAsync() ||
        await target.VisitSessions.AnyAsync() || await target.VisitReservations.AnyAsync() || await target.PurchaseOrders.AnyAsync();

    private AppDbContext CreateSourceContext(string sourcePath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={sourcePath};Mode=ReadOnly;Foreign Keys=True")
            .Options;
        return new AppDbContext(options);
    }

    private string GetSourcePath()
    {
        var configured = configuration["DataMigration:SqlitePath"] ?? "aurora.db";
        return Path.GetFullPath(configured, environment.ContentRootPath);
    }

    private async Task InsertWithIdentityAsync<TEntity>(string table, IEnumerable<TEntity> rows) where TEntity : class
    {
        var list = rows.ToList();
        if (list.Count == 0) return;
#pragma warning disable EF1002 // table vem somente da lista interna de entidades conhecidas.
        await target.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [dbo].[{table}] ON");
        try
        {
            target.Set<TEntity>().AddRange(list);
            await target.SaveChangesAsync();
            target.ChangeTracker.Clear();
        }
        finally
        {
            await target.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT [dbo].[{table}] OFF");
        }
        await target.Database.ExecuteSqlRawAsync($"DBCC CHECKIDENT ('[dbo].[{table}]', RESEED)");
#pragma warning restore EF1002
    }

    private async Task CopyUsersAsync(AppDbContext source) => await InsertWithIdentityAsync("Users",
        (await source.Users.AsNoTracking().ToListAsync()).Select(item => new User
        {
            Id = item.Id, Name = item.Name, Email = item.Email, PasswordHash = item.PasswordHash,
            CreatedAt = item.CreatedAt, Role = item.Role, IsActive = item.IsActive, SessionVersion = item.SessionVersion
        }));

    private async Task CopyExposuresAsync(AppDbContext source) => await InsertWithIdentityAsync("Exposicoes",
        (await source.Exposicoes.AsNoTracking().ToListAsync()).Select(item => new Exposicao
        {
            Id = item.Id, Titulo = item.Titulo, Descricao = item.Descricao, ImagemUrl = item.ImagemUrl,
            Periodo = item.Periodo, Inicio = item.Inicio, Fim = item.Fim, Ativa = item.Ativa
        }));

    private async Task CopyProductsAsync(AppDbContext source) => await InsertWithIdentityAsync("Produtos",
        (await source.Produtos.AsNoTracking().ToListAsync()).Select(item => new Produto
        {
            Id = item.Id, Nome = item.Nome, Preco = item.Preco, ImagemUrl = item.ImagemUrl, Ativo = item.Ativo
        }));

    private async Task CopyIngressoAsync(AppDbContext source) => await InsertWithIdentityAsync("Ingressos",
        (await source.Ingressos.AsNoTracking().ToListAsync()).Select(item => new Ingresso
        {
            Id = item.Id, NomeVisitante = item.NomeVisitante, Quantidade = item.Quantidade, DataVisita = item.DataVisita
        }));

    private async Task CopySessionsAsync(AppDbContext source) => await InsertWithIdentityAsync("visit_sessions",
        (await source.VisitSessions.AsNoTracking().ToListAsync()).Select(item => new VisitSession
        {
            Id = item.Id, ExposureId = item.ExposureId, VisitDate = item.VisitDate, StartTime = item.StartTime,
            Capacity = item.Capacity, ReservedQuantity = item.ReservedQuantity, IsActive = item.IsActive,
            Status = item.Status, CancellationReason = item.CancellationReason, CancelledAtUtc = item.CancelledAtUtc
        }));

    private async Task CopyReservationsAsync(AppDbContext source) => await InsertWithIdentityAsync("visit_reservations",
        (await source.VisitReservations.AsNoTracking().ToListAsync()).Select(item => new VisitReservation
        {
            Id = item.Id, UserId = item.UserId, VisitSessionId = item.VisitSessionId, Type = item.Type,
            Quantity = item.Quantity, Status = item.Status, RequestId = item.RequestId,
            ExposureName = item.ExposureName, VisitDate = item.VisitDate, StartTime = item.StartTime,
            CreatedAt = item.CreatedAt, CancelledAtUtc = item.CancelledAtUtc,
            CancellationSource = item.CancellationSource, CancellationReason = item.CancellationReason
        }));

    private async Task CopyCartAsync(AppDbContext source) => await InsertWithIdentityAsync("cart_items",
        (await source.CartItems.AsNoTracking().ToListAsync()).Select(item => new CartItem
        {
            Id = item.Id, UserId = item.UserId, ProductId = item.ProductId, ProductName = item.ProductName,
            UnitPrice = item.UnitPrice, Quantity = item.Quantity, CreatedAt = item.CreatedAt
        }));

    private async Task CopyLegacyHistoryAsync(AppDbContext source)
    {
        await InsertWithIdentityAsync("user_tickets", (await source.UserTickets.AsNoTracking().ToListAsync()).Select(item => new UserTicket
        {
            Id = item.Id, UserId = item.UserId, EventName = item.EventName, EventDate = item.EventDate,
            Quantity = item.Quantity, Status = item.Status, CreatedAt = item.CreatedAt
        }));
        await InsertWithIdentityAsync("user_appointments", (await source.UserAppointments.AsNoTracking().ToListAsync()).Select(item => new UserAppointment
        {
            Id = item.Id, UserId = item.UserId, EventName = item.EventName, AppointmentDate = item.AppointmentDate,
            Quantity = item.Quantity, Status = item.Status, CreatedAt = item.CreatedAt
        }));
        await InsertWithIdentityAsync("user_purchases", (await source.UserPurchases.AsNoTracking().ToListAsync()).Select(item => new UserPurchase
        {
            Id = item.Id, UserId = item.UserId, ProductName = item.ProductName, Quantity = item.Quantity,
            TotalPrice = item.TotalPrice, Status = item.Status, CreatedAt = item.CreatedAt
        }));
    }

    private async Task CopyOrdersAsync(AppDbContext source)
    {
        await InsertWithIdentityAsync("purchase_orders", (await source.PurchaseOrders.AsNoTracking().ToListAsync()).Select(item => new PurchaseOrder
        {
            Id = item.Id, UserId = item.UserId, RequestId = item.RequestId, Status = item.Status,
            TotalPrice = item.TotalPrice, CreatedAt = item.CreatedAt, CancelledAtUtc = item.CancelledAtUtc
        }));
        await InsertWithIdentityAsync("purchase_items", (await source.PurchaseItems.AsNoTracking().ToListAsync()).Select(item => new PurchaseItem
        {
            Id = item.Id, PurchaseOrderId = item.PurchaseOrderId, ProductId = item.ProductId,
            ProductName = item.ProductName, UnitPrice = item.UnitPrice, Quantity = item.Quantity, TotalPrice = item.TotalPrice
        }));
    }

    private async Task CopyIdempotencyAsync(AppDbContext source) => await InsertWithIdentityAsync("idempotency_records",
        (await source.IdempotencyRecords.AsNoTracking().ToListAsync()).Select(item => new IdempotencyRecord
        {
            Id = item.Id, UserId = item.UserId, Scope = item.Scope, RequestId = item.RequestId,
            ResultId = item.ResultId, CreatedAt = item.CreatedAt
        }));

    private async Task CopyDeletionRequestsAsync(AppDbContext source) => await InsertWithIdentityAsync("account_deletion_requests",
        (await source.AccountDeletionRequests.AsNoTracking().ToListAsync()).Select(item => new AccountDeletionRequest
        {
            Id = item.Id, UserId = item.UserId, Status = item.Status, RequestedAtUtc = item.RequestedAtUtc,
            CompletedAtUtc = item.CompletedAtUtc, CompletedByAdministratorId = item.CompletedByAdministratorId
        }));

    private async Task CopyAuditAsync(AppDbContext source) => await InsertWithIdentityAsync("admin_audit_logs",
        (await source.AdminAuditLogs.AsNoTracking().ToListAsync()).Select(item => new AdminAuditLog
        {
            Id = item.Id, AdministratorId = item.AdministratorId, Action = item.Action,
            EntityType = item.EntityType, EntityId = item.EntityId, OccurredAtUtc = item.OccurredAtUtc,
            Reason = item.Reason, PreviousValues = item.PreviousValues, NewValues = item.NewValues
        }));

    private async Task CopyMigrationIssuesAsync(AppDbContext source) => await InsertWithIdentityAsync("data_migration_issues",
        (await source.DataMigrationIssues.AsNoTracking().ToListAsync()).Select(item => new DataMigrationIssue
        {
            Id = item.Id, Entity = item.Entity, LegacyId = item.LegacyId,
            Description = item.Description, CreatedAt = item.CreatedAt
        }));
}
