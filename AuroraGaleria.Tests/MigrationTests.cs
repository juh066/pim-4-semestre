using AuroraGaleria.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AuroraGaleria.Tests;

public class MigrationTests
{
    [Fact]
    public async Task MigrationPreservesLegacyRowsAndAddsVersionedSchema()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aurora-legacy-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Foreign Keys=True";
        try
        {
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE Users (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Cpf TEXT NOT NULL, Email TEXT NOT NULL, PasswordHash TEXT NOT NULL, CreatedAt TEXT NOT NULL);
                    CREATE UNIQUE INDEX IX_Users_Email ON Users(Email); CREATE UNIQUE INDEX IX_Users_Cpf ON Users(Cpf);
                    INSERT INTO Users (Id, Name, Cpf, Email, PasswordHash, CreatedAt) VALUES (7, 'Pessoa Legada', '90000000000', 'legado@example.test', 'hash-preservado', CURRENT_TIMESTAMP);
                    CREATE TABLE Exposicoes (Id INTEGER PRIMARY KEY AUTOINCREMENT, Titulo TEXT NOT NULL, Descricao TEXT NOT NULL, ImagemUrl TEXT NOT NULL, Data TEXT NOT NULL);
                    CREATE TABLE Produtos (Id INTEGER PRIMARY KEY AUTOINCREMENT, Nome TEXT NOT NULL, Preco TEXT NOT NULL, ImagemUrl TEXT NOT NULL);
                    INSERT INTO Produtos (Id, Nome, Preco, ImagemUrl) VALUES (9, 'Produto legado', '12.50', '/legado.jpg');
                    """;
                await command.ExecuteNonQueryAsync();
            }

            using var services = new ServiceCollection().AddLogging().AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString)).BuildServiceProvider();
            await DatabaseInitializer.InitializeAsync(services, services.GetRequiredService<ILogger<MigrationTests>>());
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Produto legado", (await db.Produtos.SingleAsync(item => item.Id == 9)).Nome);
            var preserved = await db.Users.SingleAsync(item => item.Id == 7);
            Assert.Equal("legado@example.test", preserved.Email);
            Assert.Equal("hash-preservado", preserved.PasswordHash);
            Assert.Equal(3, await db.Database.SqlQueryRaw<int>("SELECT MAX(Version) AS Value FROM schema_migrations").SingleAsync());
            Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('Users') WHERE name = 'Cpf'").SingleAsync());
            Assert.True(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='visit_reservations'").SingleAsync() == 1);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
