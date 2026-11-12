# Banco SQL Server — entrega PIM IV

Os arquivos devem ser executados em um banco SQL Server novo e vazio, já criado pela equipe. Nenhum script cria, apaga ou substitui um banco.

## Ordem de execução

1. Conecte-se ao banco novo e confirme que ele é o banco selecionado.
2. Execute `01-estrutura.sql`. Este arquivo foi gerado diretamente da migration `20261107161648_InitialSqlServer` pelo EF Core 10.0.8; foram acrescentados somente separadores de lote `GO` antes das procedures e da trigger, exigidos pelo SQL Server para executar esses comandos fora do pipeline do EF.
3. Execute `02-dados-demonstracao.sql` para incluir um catálogo fictício mínimo. O arquivo é opcional e idempotente.
4. Inicie a aplicação apontando `ConnectionStrings:DefaultConnection` para esse banco. Usuários devem ser cadastrados pela própria aplicação; os scripts não incluem contas nem dados pessoais.

Exemplo com `sqlcmd`:

```powershell
sqlcmd -S "SERVIDOR" -d "BANCO_NOVO" -E -b -i "01-estrutura.sql"
sqlcmd -S "SERVIDOR" -d "BANCO_NOVO" -E -b -i "02-dados-demonstracao.sql"
```

Substitua `SERVIDOR` e `BANCO_NOVO` pelos valores do ambiente. Use `-U` e `-P` no lugar de `-E` somente quando a instalação exigir autenticação SQL Server.

## Objetos de negócio

- `dbo.sp_ReserveVisitSession`: registra uma reserva de ingresso ou agendamento em transação serializável, valida usuário, período, horário e capacidade, impede duplicidade pelo identificador da requisição e atualiza as vagas ocupadas.
- `dbo.sp_CancelVisitReservation`: cancela somente a reserva pertencente ao usuário, impede cancelamento depois do início da sessão, devolve a quantidade ao estoque de vagas e trata novas tentativas de forma idempotente.
- `dbo.trg_visit_sessions_audit`: grava em `dbo.admin_audit_logs` as alterações de capacidade, estado e motivo de cancelamento das sessões. Quando definido pela aplicação, o administrador é lido de `SESSION_CONTEXT(N'AuroraAdminId')`.

## Compatibilidade

O script utiliza recursos do SQL Server empregados pela migration, incluindo `CREATE OR ALTER`, `SESSION_CONTEXT` e `FOR JSON`. Recomenda-se SQL Server 2016 SP1 ou superior. A estrutura deve continuar sendo evoluída pelas migrations; se o modelo mudar, regenere `01-estrutura.sql` em vez de editá-lo manualmente.
