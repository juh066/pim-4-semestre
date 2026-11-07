# Galeria Aurora — PIM IV

Aplicação acadêmica em ASP.NET Core MVC, Entity Framework Core e SQL Server. Compras e ingressos são **simulações**: o sistema não solicita dados de cartão, não cobra valores e não deve ser apresentado como integração de pagamento real.

## Executar

Requisitos: SDK .NET 10 e SQL Server. No Windows, a configuração de desenvolvimento usa LocalDB com autenticação integrada.

```bash
sqllocaldb start MSSQLLocalDB
dotnet tool restore
dotnet restore
dotnet run
```

A URL é exibida no terminal (por padrão, `http://localhost:5140`). Na primeira execução, as migrações criam ou atualizam o banco `AuroraGaleria` e o catálogo inicial é inserido somente quando as tabelas estão vazias.

## Banco, backup e migração

As migrações em `Data/Migrations` são a fonte principal do esquema SQL Server. O script integral está em `docs/sql/aurora-sqlserver.sql`. O guia `docs/sql-server.md` explica configuração, transferência somente leitura do SQLite, conferência, backup, restauração e corte definitivo. O modelo entidade-relacionamento e os modelos lógico/físico estão em `docs/modelo-dados.md`.

Para transferir o banco existente para um SQL Server novo:

```powershell
Copy-Item aurora.db backups/aurora-antes-do-sqlserver.db
dotnet run -- --migrate-sqlite-to-sqlserver
dotnet run -- --verify-sqlserver-import
```

## Administrador

O cadastro comum sempre cria o papel `Visitor`. Para uma instalação normal, o primeiro administrador deve ser criado pelo comando local abaixo, e somente enquanto nenhum administrador existir. Não há senha fixa nesse fluxo.

```bash
AURORA_ADMIN_NAME="Administrador" \
AURORA_ADMIN_EMAIL="admin@example.test" \
AURORA_ADMIN_PASSWORD="uma-senha-forte-com-12-ou-mais" \
dotnet run -- --create-admin
```

Use dados fictícios. Depois do login, o administrador acessa `/admin` para cadastrar, editar, desativar ou reativar exposições e produtos, criar sessões e ativá-las ou desativá-las. Desativar preserva o histórico. O comando inicial é bloqueado após a criação do primeiro administrador.

### Cadastro administrativo para demonstração

Em desenvolvimento, a seção `AcademicDemo` de `appsettings.Development.json` habilita uma opção discreta no cadastro. Ao selecionar **Cadastrar como administrador**, use o código de demonstração `123`. O código é validado no servidor e não é salvo na conta nem registrado em logs.

O recurso fica desativado por padrão em `appsettings.json`. Para ativá-lo sem editar arquivos, use configuração de ambiente antes de iniciar a aplicação:

```bash
AcademicDemo__EnableAdminRegistration=true \
AcademicDemo__AdminRegistrationCode=123 \
dotnet run
```

Para desativar explicitamente, defina `AcademicDemo__EnableAdminRegistration=false`. Quando desativado, a opção não aparece e o servidor rejeita tentativas de cadastro administrativo, mesmo que o navegador envie diretamente o papel, o código ou um parâmetro de ativação.

O código `123` existe somente para facilitar a apresentação acadêmica. Ele **não é uma proteção adequada para produção**. Em qualquer ambiente real, mantenha esse recurso desativado e use um processo seguro de provisionamento administrativo.

## Regras implementadas

- O carrinho recebe somente identificador, quantidade e chave da solicitação. Nome e preço são consultados no banco.
- Produto inexistente/inativo e quantidade fora de 1 a 20 são rejeitados.
- O preço armazenado no carrinho é uma referência. Se o preço oficial mudar antes da confirmação, o checkout retorna conflito, atualiza o carrinho e exige uma nova confirmação. O pedido guarda nome, preço unitário e total efetivamente confirmados.
- Exposições e produtos públicos vêm do banco; apenas registros ativos são exibidos.
- Uma sessão possui exposição, data, horário, capacidade e vagas ocupadas. Ingressos e agendamentos usam a mesma reserva e, portanto, consomem uma vaga uma única vez.
- No SQL Server, as procedures de reserva e cancelamento atualizam histórico e ocupação na mesma transação serializável, impedindo ultrapassar a capacidade ou liberar vagas duas vezes.
- Datas passadas e datas fora do período da exposição são rejeitadas no servidor. A interface oferece somente sessões futuras válidas.
- Chaves de solicitação impedem repetição de inclusão, reserva e checkout.
- Visitantes veem e alteram apenas registros associados ao identificador presente no cookie autenticado. Administração e análises exigem o papel `Administrator` no servidor.
- Operações que alteram estado usam token antiforgery no cabeçalho ou no formulário; cookies são `HttpOnly` e `SameSite=Strict`.

O cadastro não coleta CPF. Contas e históricos usam apenas o identificador interno. As regras completas de privacidade, retenção, fuso, cancelamento e administração estão em `docs/regras-de-negocio.md`.

## Testes

```bash
dotnet test AuroraGaleria.Tests/AuroraGaleria.Tests.csproj
dotnet list package --vulnerable --include-transitive
```

A suíte cobre as regras existentes e inclui testes contra SQL Server real para procedures, concorrência, cancelamento idempotente, trigger em múltiplas linhas e fluxos HTTP. Resultados e ambiente estão em `docs/testes-sql-server.md`.

## Dependências e acessibilidade

`SQLitePCLRaw.bundle_e_sqlite3` está fixado em 3.0.5 para substituir a versão transitiva 2.1.11 afetada por `GHSA-2m69-gcr7-jv3q`. O documento está em `pt-BR`, os campos têm rótulos, o foco do carrinho retorna ao acionador e os controles possuem foco visível e ajustes para telas pequenas. O carregamento do VLibras é isolado: uma indisponibilidade externa apenas gera aviso no console.

## Limites do escopo

- O aplicativo mobile não faz parte desta etapa; os serviços de domínio foram separados para permitir futuro consumo por API.
- O SQLite permanece somente como origem preservada da importação e banco isolado dos testes rápidos; não é o banco principal.
- Ainda é necessário informar a data da avaliação final e controlar backups externos conforme a política de retenção.
- Pagamento real, publicação e contratação de serviços permanecem fora do escopo.
