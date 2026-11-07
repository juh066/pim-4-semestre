# SQL Server — configuração, migração e recuperação

## Banco principal

A aplicação usa SQL Server como banco principal. Em desenvolvimento, a configuração padrão utiliza autenticação integrada do Windows e a instância LocalDB:

```text
Server=(localdb)\MSSQLLocalDB;Database=AuroraGaleria;Trusted_Connection=True;TrustServerCertificate=True
```

Nenhuma senha é versionada. Em outro ambiente, informe a conexão pela variável `ConnectionStrings__DefaultConnection` ou por configuração local não versionada. `DatabaseProvider` deve permanecer como `SqlServer`.

Na primeira execução, `DatabaseInitializer` chama as migrações do Entity Framework Core. Elas criam tabelas, chaves, índices, restrições, as procedures e a trigger. Para criar um banco vazio e iniciar o site:

```powershell
sqllocaldb start MSSQLLocalDB
dotnet restore
dotnet run
```

O script idempotente completo está em `docs/sql/aurora-sqlserver.sql`. As migrações em `Data/Migrations` são a fonte principal para a evolução do esquema; o script deve ser regenerado após novas migrações:

```powershell
dotnet tool restore
dotnet dotnet-ef migrations script --idempotent --output docs/sql/aurora-sqlserver.sql
```

## Transferência do SQLite

Antes da transferência final, interrompa gravações na aplicação antiga e faça uma cópia consistente:

```powershell
Copy-Item aurora.db backups/aurora-antes-do-sqlserver.db
```

Configure `DataMigration:SqlitePath` se a origem não for `aurora.db`. O destino deve ser um banco novo, contendo apenas o esquema criado pelas migrações. Execute:

```powershell
dotnet run -- --migrate-sqlite-to-sqlserver
dotnet run -- --verify-sqlserver-import
```

A ferramenta abre o SQLite em modo somente leitura, copia registros na ordem das chaves estrangeiras e preserva os identificadores. `IDENTITY_INSERT` ajusta as identidades ao maior identificador importado. A cópia ocorre em uma transação; em caso de falha, os dados operacionais são revertidos e a ocorrência fica registrada em `data_import_history`.

Uma importação concluída bloqueia novas execuções. O destino também precisa estar vazio para evitar mistura ou duplicação. A conferência compara quantidades por tabela, total dos pedidos, reservas canceladas, ocupação das sessões e os IDs, e-mails, hashes e perfis das contas.

Não apague o SQLite depois da conferência. Guarde-o somente pelo prazo de retenção definido para o projeto. Depois que o SQL Server receber novas operações, voltar ao SQLite exige reconciliar essas operações; trocar apenas a string de conexão causaria perda dos dados novos.

## Backup do SQL Server

Pare a aplicação ou impeça novas gravações durante o corte final. Crie e valide o backup, usando um caminho absoluto acessível pela instância:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "BACKUP DATABASE [AuroraGaleria] TO DISK=N'C:\backups\AuroraGaleria.bak' WITH COPY_ONLY, INIT, CHECKSUM"
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "RESTORE VERIFYONLY FROM DISK=N'C:\backups\AuroraGaleria.bak' WITH CHECKSUM"
```

Backups contêm os mesmos dados pessoais do banco e seguem a mesma política de retenção. Cópias fora da aplicação precisam ser inventariadas e eliminadas manualmente no prazo.

## Restauração

Pare o site e preserve o banco atual para diagnóstico. Depois, restaure uma cópia validada:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "ALTER DATABASE [AuroraGaleria] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [AuroraGaleria] FROM DISK=N'C:\backups\AuroraGaleria.bak' WITH REPLACE, RECOVERY; ALTER DATABASE [AuroraGaleria] SET MULTI_USER"
```

Inicie o site e execute a conferência funcional. Se a restauração representar retorno para um instante anterior, reconcilie operações posteriores antes de liberar novas gravações.

## Procedures e trigger

- `sp_ReserveVisitSession`: valida usuário, sessão, período, horário, estado, capacidade e chave idempotente. Usa transação serializável e bloqueios de atualização para reservar a vaga e criar o histórico atomicamente.
- `sp_CancelVisitReservation`: valida titularidade, estado e início da sessão. Muda o histórico para cancelado e libera a vaga somente na primeira transição.
- `trg_visit_sessions_audit`: registra mudanças reais de capacidade, estado ou cancelamento. Trata várias linhas e lê o administrador de `SESSION_CONTEXT`, definido e limpo pelo servidor autenticado.

As procedures são chamadas por `ReservationService`. A trigger não altera vagas, preços ou estoque. A atualização da ocupação pertence somente às procedures no fluxo SQL Server.
