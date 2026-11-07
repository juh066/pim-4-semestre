# Testes executados no SQL Server

Ambiente utilizado em 7 de novembro de 2026: SQL Server 2025 LocalDB 17.0.4025.3, .NET SDK 10.0.403 e bancos de teste exclusivos, removidos ao final.

Resultados confirmados:

- Suíte completa: 46 testes aprovados, sem falhas; 4 deles executados em bancos SQL Server reais e isolados.
- Criação do esquema por migração, incluindo procedures e trigger.
- Importação real do SQLite: contagens, total de pedidos, estados, ocupação, IDs, e-mails, hashes e perfis iguais entre origem e destino.
- Duas requisições disputando a última vaga: uma confirmação e uma rejeição por capacidade.
- Tentativa de cancelar reserva alheia bloqueada.
- Cancelamento repetido sem devolver a vaga duas vezes.
- Atualização administrativa passando o identificador autenticado pelo contexto da conexão.
- Trigger registrando alteração de várias sessões em uma única instrução.
- Cadastro e edição de exposições, produtos e sessões no SQL Server.
- Fluxo HTTP em SQL Server: cadastro, login, reserva, cancelamento, carrinho, checkout simulado, cancelamento do pedido, logout, cadastro administrativo e acesso à administração.

Comandos de reprodução:

```powershell
dotnet test AuroraGaleria.Tests/AuroraGaleria.Tests.csproj --filter "FullyQualifiedName~SqlServerIntegrationTests"
dotnet test AuroraGaleria.Tests/AuroraGaleria.Tests.csproj
dotnet run -- --verify-sqlserver-import
```

Os testes SQLite existentes continuam sendo usados como testes rápidos de regras e regressão. Eles não são considerados prova das procedures, da trigger ou da concorrência do SQL Server; essa prova é feita pelos testes de integração acima.
