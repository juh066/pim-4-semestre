# Web e mobile com banco SQL Server compartilhado

As duas aplicações usam a instância SQL Server LocalDB `(localdb)\MSSQLLocalDB` e o banco `AuroraGaleria`. A conexão utiliza autenticação integrada do Windows e está configurada nos dois arquivos `appsettings.json`.

## Pré-requisitos

- .NET SDK 10;
- SQL Server LocalDB disponível no Windows;
- banco `AuroraGaleria` acessível pelo usuário que executa as aplicações.

Para utilizar outra instância, defina `ConnectionStrings__DefaultConnection` e `DatabaseProvider=SqlServer` no ambiente de **ambos** os processos. Não grave usuário, senha ou outras credenciais no repositório.

## Executar simultaneamente

No diretório raiz, execute a aplicação web:

```powershell
dotnet run --project .\AuroraGaleria.csproj --launch-profile http
```

A web ficará disponível em `http://localhost:5140`.

Em outro terminal, execute a versão responsiva:

```powershell
dotnet run --project .\versao-mobile\AuroraGaleria.csproj --launch-profile http
```

A versão mobile ficará disponível em `http://localhost:5150`.

Cada aplicação mantém seu próprio cookie e suas próprias chaves de proteção. A mesma conta pode ser utilizada nas duas, mas é necessário entrar separadamente em cada versão.

## Inicialização do banco

Os dois projetos usam o mesmo modelo e as mesmas migrations do Entity Framework Core. A aplicação web continua responsável por conferir o catálogo demonstrativo e as sessões iniciais. A versão mobile aplica migrations pendentes, mas não executa o povoamento inicial; isso evita que a inicialização simultânea tente duplicar os dados de demonstração.

O arquivo legado `aurora.db` não é mais a fonte de dados da versão mobile. Ele deve ser preservado fora do controle de versão enquanto houver necessidade de auditoria ou recuperação.

O importador SQLite existente é destinado a um SQL Server novo e sem dados operacionais. Ele recusa um destino preenchido e não deve ser usado como rotina de mesclagem. Antes de qualquer importação, faça backup do SQL Server, copie o SQLite e confira conflitos de identificadores e e-mails.

## Acesso por celular físico

O celular acessa a aplicação ASP.NET Core no computador; ele não acessa o SQL Server diretamente. Em uma rede local confiável, execute a mobile escutando na interface de rede:

```powershell
$env:ASPNETCORE_URLS = "http://0.0.0.0:5150"
dotnet run --project .\versao-mobile\AuroraGaleria.csproj --no-launch-profile
```

Descubra o IPv4 local do computador e abra `http://IP_DO_COMPUTADOR:5150` no celular. Computador e celular precisam estar na mesma rede, e o Firewall do Windows deve permitir somente a porta da aplicação nessa rede. Não abra a porta do SQL Server nem publique o LocalDB na internet.

## Preservação e validação realizada

Antes da alteração, foi produzido um backup `COPY_ONLY` do banco `AuroraGaleria` e o conjunto foi validado com `RESTORE VERIFYONLY`. Também foram copiadas as bases SQLite encontradas. A auditoria do SQLite mobile encontrou apenas 3 exposições, 3 produtos e 24 sessões demonstrativas, sem usuários, reservas, agendamentos ou compras; por isso nenhum dado foi importado no SQL Server já preenchido.

Os dois projetos compilaram sem avisos ou erros. A suíte automatizada executou 48 testes com sucesso. Em uma restauração isolada do backup, 18 verificações cruzadas confirmaram compartilhamento de usuários, catálogo, reservas, agendamentos e compras, cancelamento com liberação de vaga, exclusão de compras canceladas dos indicadores, isolamento entre usuários, proteção administrativa e concorrência da última vaga.
