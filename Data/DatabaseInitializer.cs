using System.Data;
using AuroraGaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace AuroraGaleria.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureCreatedAsync();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        await EnsureColumnAsync(db, "Users", "Role", "TEXT NOT NULL DEFAULT 'Visitor'");
        await EnsureColumnAsync(db, "Exposicoes", "StartDate", "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'");
        await EnsureColumnAsync(db, "Exposicoes", "EndDate", "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'");
        await EnsureColumnAsync(db, "Exposicoes", "IsActive", "INTEGER NOT NULL DEFAULT 1");
        await EnsureColumnAsync(db, "Produtos", "IsActive", "INTEGER NOT NULL DEFAULT 1");

        foreach (var command in SchemaCommands)
        {
            await db.Database.ExecuteSqlRawAsync(command);
        }

        foreach (var (table, command) in LegacyIssueCommands)
        {
            if (await TableExistsAsync(db, table))
            {
                await db.Database.ExecuteSqlRawAsync(command);
            }
        }

        await db.Database.ExecuteSqlRawAsync(
            "INSERT OR IGNORE INTO schema_migrations (Version, AppliedAt) VALUES (1, {0})",
            DateTime.UtcNow);

        await SeedCatalogAsync(db, logger);
        await transaction.CommitAsync();
    }

    private static async Task EnsureColumnAsync(AppDbContext db, string table, string column, string definition)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        var exists = false;
        await using (var check = connection.CreateCommand())
        {
            check.CommandText = $"PRAGMA table_info(\"{table}\")";
            await using var reader = await check.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }
        if (exists) return;
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}";
        await alter.ExecuteNonQueryAsync();
    }

    private static async Task<bool> TableExistsAsync(AppDbContext db, string table)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = table;
        command.Parameters.Add(parameter);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task SeedCatalogAsync(AppDbContext db, ILogger logger)
    {
        var knownExhibitions = new[]
        {
            new Exposicao { Titulo = "Jean-Michel Basquiat", Descricao = "", ImagemUrl = "/images/basquiat.jpeg", Periodo = "15 Maio - 30 Junho", Inicio = new DateTime(2026, 5, 15), Fim = new DateTime(2026, 6, 30), Ativa = true },
            new Exposicao { Titulo = "Takashi Murakami", Descricao = "", ImagemUrl = "/images/Takashi.jpeg", Periodo = "01 Julho - 15 Agosto", Inicio = new DateTime(2026, 7, 1), Fim = new DateTime(2026, 8, 15), Ativa = true },
            new Exposicao { Titulo = "Banksy", Descricao = "", ImagemUrl = "/images/bansky.jpeg", Periodo = "20 Agosto - 30 Setembro", Inicio = new DateTime(2026, 8, 20), Fim = new DateTime(2026, 9, 30), Ativa = true }
        };

        if (!await db.Exposicoes.AnyAsync())
        {
            db.Exposicoes.AddRange(knownExhibitions);
            await db.SaveChangesAsync();
        }
        else
        {
            var existing = await db.Exposicoes.ToListAsync();
            foreach (var exposure in existing.Where(item => item.Inicio == default || item.Fim == default))
            {
                var known = knownExhibitions.SingleOrDefault(item => item.Titulo == exposure.Titulo);
                if (known is null)
                {
                    await RecordMigrationIssueAsync(db, "Exposicoes", exposure.Id, "Datas antigas sem correspondência segura; requer revisão administrativa.");
                    exposure.Ativa = false;
                    continue;
                }

                exposure.Inicio = known.Inicio;
                exposure.Fim = known.Fim;
                exposure.Periodo = known.Periodo;
            }
            await db.SaveChangesAsync();
        }

        if (!await db.Produtos.AnyAsync())
        {
            db.Produtos.AddRange(
                new Produto { Nome = "Caneca Aurora", Preco = 49.90m, ImagemUrl = "/images/caneca_aurora.jpeg", Ativo = true },
                new Produto { Nome = "Tote Bag", Preco = 69.90m, ImagemUrl = "/images/basquiat_tote_bag.webp", Ativo = true },
                new Produto { Nome = "Caderno Artístico", Preco = 39.90m, ImagemUrl = "/images/sn_x1000-pad_750x1000_f8f8f8.webp", Ativo = true });
            await db.SaveChangesAsync();
        }

        logger.LogInformation("Banco atualizado para o esquema PIM IV sem recriação. Versão de esquema: 1.");
    }

    private static Task RecordMigrationIssueAsync(AppDbContext db, string entity, int legacyId, string description)
    {
        return db.Database.ExecuteSqlRawAsync(
            "INSERT OR IGNORE INTO data_migration_issues (Entity, LegacyId, Description, CreatedAt) VALUES ({0}, {1}, {2}, {3})",
            entity, legacyId, description, DateTime.UtcNow);
    }

    private static readonly string[] SchemaCommands =
    {
        "CREATE TABLE IF NOT EXISTS schema_migrations (Version INTEGER NOT NULL PRIMARY KEY, AppliedAt TEXT NOT NULL);",
        "CREATE TABLE IF NOT EXISTS data_migration_issues (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Entity TEXT NOT NULL, LegacyId INTEGER NOT NULL, Description TEXT NOT NULL, CreatedAt TEXT NOT NULL, UNIQUE(Entity, LegacyId));",
        """
        CREATE TABLE IF NOT EXISTS visit_sessions (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ExposureId INTEGER NOT NULL,
            VisitDate TEXT NOT NULL, StartTime TEXT NOT NULL, Capacity INTEGER NOT NULL CHECK (Capacity > 0),
            ReservedQuantity INTEGER NOT NULL DEFAULT 0 CHECK (ReservedQuantity >= 0 AND ReservedQuantity <= Capacity),
            IsActive INTEGER NOT NULL DEFAULT 1,
            CONSTRAINT FK_visit_sessions_Exposicoes_ExposureId FOREIGN KEY (ExposureId) REFERENCES Exposicoes (Id) ON DELETE RESTRICT,
            UNIQUE(ExposureId, VisitDate, StartTime));
        """,
        """
        CREATE TABLE IF NOT EXISTS visit_reservations (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, UserId INTEGER NOT NULL, VisitSessionId INTEGER NOT NULL,
            Type TEXT NOT NULL, Quantity INTEGER NOT NULL CHECK (Quantity > 0), Status TEXT NOT NULL, RequestId TEXT NOT NULL,
            ExposureName TEXT NOT NULL, VisitDate TEXT NOT NULL, StartTime TEXT NOT NULL, CreatedAt TEXT NOT NULL,
            CONSTRAINT FK_visit_reservations_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT,
            CONSTRAINT FK_visit_reservations_visit_sessions_VisitSessionId FOREIGN KEY (VisitSessionId) REFERENCES visit_sessions (Id) ON DELETE RESTRICT,
            UNIQUE(UserId, RequestId));
        """,
        """
        CREATE TABLE IF NOT EXISTS purchase_orders (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, UserId INTEGER NOT NULL, RequestId TEXT NOT NULL,
            Status TEXT NOT NULL, TotalPrice REAL NOT NULL, CreatedAt TEXT NOT NULL,
            CONSTRAINT FK_purchase_orders_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT,
            UNIQUE(UserId, RequestId));
        """,
        """
        CREATE TABLE IF NOT EXISTS purchase_items (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, PurchaseOrderId INTEGER NOT NULL, ProductId INTEGER NULL,
            ProductName TEXT NOT NULL, UnitPrice REAL NOT NULL, Quantity INTEGER NOT NULL CHECK (Quantity > 0), TotalPrice REAL NOT NULL,
            CONSTRAINT FK_purchase_items_purchase_orders_PurchaseOrderId FOREIGN KEY (PurchaseOrderId) REFERENCES purchase_orders (Id) ON DELETE CASCADE,
            CONSTRAINT FK_purchase_items_Produtos_ProductId FOREIGN KEY (ProductId) REFERENCES Produtos (Id) ON DELETE SET NULL);
        """,
        "CREATE TABLE IF NOT EXISTS idempotency_records (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, UserId INTEGER NOT NULL, Scope TEXT NOT NULL, RequestId TEXT NOT NULL, ResultId INTEGER NULL, CreatedAt TEXT NOT NULL, UNIQUE(UserId, Scope, RequestId));"
    };

    private static readonly (string Table, string Command)[] LegacyIssueCommands =
    {
        ("user_tickets", "INSERT OR IGNORE INTO data_migration_issues (Entity, LegacyId, Description, CreatedAt) SELECT 'user_tickets', Id, 'Ingresso legado preservado sem sessão correspondente; requer revisão administrativa.', CURRENT_TIMESTAMP FROM user_tickets;"),
        ("user_appointments", "INSERT OR IGNORE INTO data_migration_issues (Entity, LegacyId, Description, CreatedAt) SELECT 'user_appointments', Id, 'Agendamento legado preservado sem sessão correspondente; requer revisão administrativa.', CURRENT_TIMESTAMP FROM user_appointments;"),
        ("user_purchases", "INSERT OR IGNORE INTO data_migration_issues (Entity, LegacyId, Description, CreatedAt) SELECT 'user_purchases', Id, 'Compra legada preservada sem produto associado com segurança.', CURRENT_TIMESTAMP FROM user_purchases;")
    };
}
