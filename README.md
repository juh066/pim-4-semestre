# Galeria Aurora — PIM III / preparação para o PIM IV

Aplicação acadêmica em ASP.NET Core MVC, Entity Framework Core e SQLite. Compras e ingressos são **simulações**: o sistema não solicita dados de cartão, não cobra valores e não deve ser apresentado como integração de pagamento real.

## Executar

Requisitos: SDK .NET 10.

```bash
dotnet restore
dotnet run
```

A URL é exibida no terminal (por padrão, `http://localhost:5140` no perfil de desenvolvimento). Na primeira execução, a aplicação cria ou atualiza `aurora.db` incrementalmente e insere o catálogo inicial somente quando as tabelas estão vazias.

## Banco, backup e migração

Antes de aplicar esta versão ao banco de trabalho foi criada a cópia `backups/aurora-before-pim4-20261103.db` (arquivos `.db` são ignorados pelo Git). Para outro ambiente, faça a sua própria cópia antes da primeira execução:

```bash
cp aurora.db backups/aurora-antes-da-migracao.db
dotnet run
```

O inicializador versionado em `Data/DatabaseInitializer.cs` altera o esquema sem apagar ou recriar o banco. Ele mantém tabelas legadas, acrescenta papéis, estado ativo, sessões, reservas, pedidos, itens históricos e idempotência. Registros legados que não podem ser relacionados com segurança a uma sessão ou produto são preservados e anotados em `data_migration_issues`; nenhuma associação é inventada.

## Administrador

O cadastro comum sempre cria o papel `Visitor`. Para uma instalação normal, o primeiro administrador deve ser criado pelo comando local abaixo, e somente enquanto nenhum administrador existir. Não há senha fixa nesse fluxo.

```bash
AURORA_ADMIN_NAME="Administrador" \
AURORA_ADMIN_CPF="CPF_FICTICIO_VALIDO" \
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
- A atualização condicional de vagas ocorre dentro de transação SQLite e impede ultrapassar a capacidade. Cancelar libera as vagas na mesma transação.
- Datas passadas e datas fora do período da exposição são rejeitadas no servidor. A interface oferece somente sessões futuras válidas.
- Chaves de solicitação impedem repetição de inclusão, reserva e checkout.
- Visitantes veem e alteram apenas registros associados ao identificador presente no cookie autenticado. Administração e análises exigem o papel `Administrator` no servidor.
- Operações que alteram estado usam token antiforgery no cabeçalho ou no formulário; cookies são `HttpOnly` e `SameSite=Strict`.

O CPF foi mantido porque já faz parte do cadastro e da restrição de unicidade do projeto, mas é tratado como identificador acadêmico fictício: qualquer sequência de exatamente 11 dígitos é aceita, desde que ainda não esteja cadastrada. Para o PIM IV, a equipe deve decidir se existe justificativa acadêmica/legal para coletá-lo; se não houver, uma migração posterior deve torná-lo opcional ou removê-lo.

## Testes

```bash
dotnet test AuroraGaleria.Tests/AuroraGaleria.Tests.csproj
dotnet list package --vulnerable --include-transitive
```

A suíte cobre preço adulterado, produto inexistente/inativo, quantidade inválida, datas inválidas, autorização administrativa, isolamento entre usuários, concorrência de capacidade, cancelamento, idempotência, cadastro/login/logout/carrinho/histórico e preservação de dados na migração.

## Dependências e acessibilidade

`SQLitePCLRaw.bundle_e_sqlite3` está fixado em 3.0.5 para substituir a versão transitiva 2.1.11 afetada por `GHSA-2m69-gcr7-jv3q`. O documento está em `pt-BR`, os campos têm rótulos, o foco do carrinho retorna ao acionador e os controles possuem foco visível e ajustes para telas pequenas. O carregamento do VLibras é isolado: uma indisponibilidade externa apenas gera aviso no console.

## Pendências para o PIM IV

- O aplicativo mobile não faz parte desta etapa; os serviços de domínio foram separados para permitir futuro consumo por API.
- O manual do PIM IV exige procedures e triggers. SQLite não oferece stored procedures. Não houve troca silenciosa de banco: será necessária uma migração futura planejada para um SGBD compatível (por exemplo, PostgreSQL ou SQL Server), com procedures/triggers equivalentes, testes de concorrência e plano de rollback.
- Antes dessa migração, definir retenção de CPF, política de privacidade, fuso horário oficial, regras de reembolso/cancelamento e operação administrativa das sessões.
- Pagamento real, publicação e contratação de serviços permanecem fora do escopo.
