# Gerenciamento ágil — Galeria Aurora — PIM IV

## 1. Natureza deste documento

Este documento combina um retrato verificável do repositório com um planejamento proposto para o fechamento do PIM IV. Ele não representa atas de reunião, sprints históricas, participantes ou datas passadas, pois esses registros não estão disponíveis no repositório.

Classificação usada:

- **Implementado e validado:** existe no código e possui teste automatizado ou verificação executada.
- **Implementado, validação parcial:** existe no código, mas falta alguma verificação externa ou manual.
- **Implementado localmente:** está na árvore de trabalho, ainda não integrado ao `main` remoto.
- **Pendente:** há evidência objetiva de que o comportamento ainda precisa ser alterado ou verificado.
- **Dependência externa:** depende de outro integrante ou ambiente e não é atribuído ao responsável pelo site.

## 2. Estado atual verificado

### Implementado e validado

- Página pública com exposições, produtos e sessões carregados do banco.
- Cadastro, login, logout, sessão por cookie, perfis de visitante e administrador e proteção das rotas administrativas.
- Reserva de ingresso, agendamento, cancelamento, controle de capacidade, propriedade da reserva e idempotência.
- Carrinho, confirmação de preço, histórico de compras e cancelamento sem apagar os itens históricos.
- Administração de exposições, produtos e sessões, incluindo auditoria e regras de capacidade.
- Privacidade, solicitação de exclusão, anonimização e política de retenção protegida.
- Migração para SQL Server, procedures de reserva e cancelamento e trigger de auditoria.
- Navegação do cabeçalho corrigida para visitante, usuário e administrador.
- Indicadores de vendas corrigidos localmente para considerar somente pedidos confirmados.
- Scripts SQL Server de estrutura e demonstração executados em banco isolado.
- Build, testes automatizados e publicação .NET executados localmente.

### Implementado localmente ou com validação parcial

- Correção do indicador de vendas e seu teste ainda estão na árvore de trabalho, não no `main` remoto.
- Scripts SQL de entrega e teste de validação estão na árvore de trabalho.
- Dockerfile, `.dockerignore`, plano de nuvem e pipeline de CI estão preparados localmente.
- O estágio equivalente de restore, build, testes e `dotnet publish` foi validado; o `docker build` não pôde ser executado porque Docker não está instalado no ambiente local.
- A pipeline foi revisada estaticamente, mas ainda não existe registro de uma execução no GitHub Actions.

### Pendências confirmadas no código

- O painel administrativo ainda exibe estados internos, como `Open`, diretamente ao usuário.
- A tela de análise ainda trata falha de carregamento como ausência de dados.
- A mensagem vazia ainda não usa o texto final definido para a entrega.
- A tela de análise ainda não oferece nova tentativa após falha.
- Chart.js ainda é carregado por CDN sem versão fixa e não existe cópia local.
- Falta uma verificação manual documentada dos gráficos com dados, sem dados e com erro, incluindo computador, celular e teclado.
- A aplicação ainda não possui health checks dedicados nem persistência compartilhada das chaves de proteção de dados para escala horizontal.

### Dependência de outro integrante

A integração mobile é uma dependência externa ao escopo de desenvolvimento do site. Este planejamento prevê somente o apoio necessário do lado web — contrato dos endpoints, autenticação, ambiente e dados de teste. O desenvolvimento, empacotamento e validação do aplicativo mobile pertencem ao integrante responsável pelo aplicativo e não são atribuídos ao responsável pela aplicação web.

## 3. Product Backlog

Prioridades: **P0** bloqueia a entrega; **P1** essencial ao PIM IV; **P2** importante; **P3** evolução posterior.

| ID | Funcionalidade ou entrega | Prioridade | Estado | Critérios de aceitação |
|---|---|---:|---|---|
| PB-01 | Catálogo público de exposições e produtos | P1 | Implementado e validado | Página inicial consulta o banco; mostra somente registros ativos; mantém layout responsivo. |
| PB-02 | Autenticação e autorização | P0 | Implementado e validado | Cadastro e login válidos; e-mail normalizado; sessão revogável; visitante não acessa administração; credenciais não aparecem em logs. |
| PB-03 | Ingressos e agendamentos | P0 | Implementado e validado | Reserva respeita período, horário e capacidade; requisições repetidas são idempotentes; cancelamento devolve vagas e preserva histórico. |
| PB-04 | Carrinho e compras simuladas | P1 | Implementado e validado | Preço vem do servidor; alteração exige confirmação; checkout gera histórico; cancelamento preserva pedido e itens. |
| PB-05 | Administração do catálogo e das sessões | P1 | Implementado e validado | Administrador cria, edita e desativa catálogo; capacidade não fica menor que reservas; cancelamento registra motivo e auditoria. |
| PB-06 | Indicadores coerentes | P1 | Implementado localmente e validado | Vendas somam somente pedidos confirmados; cancelamento remove quantidade e receita dos indicadores; ingressos contam somente reservas confirmadas. |
| PB-07 | Textos e estados administrativos em português | P1 | Pendente | Valores internos continuam em inglês no banco; interface apresenta “Aberta”, “Encerrada”, “Cancelada” e “Concluída” conforme o estado. |
| PB-08 | Estados da tela de análise | P1 | Pendente | Carregamento, vazio e erro são estados distintos; vazio mostra “Ainda não há registros para exibir”; erro mostra “Não foi possível carregar os dados” e botão para tentar novamente. |
| PB-09 | Gráficos disponíveis sem internet | P1 | Pendente | Chart.js usa versão fixa em arquivo local; gráficos funcionam sem acesso à CDN; paleta e tipografia permanecem inalteradas. |
| PB-10 | Migração e operação SQL Server | P0 | Implementado e validado | Migration cria estrutura; procedures impedem excesso de reservas e cancelam com segurança; trigger audita alterações; testes usam banco isolado. |
| PB-11 | Scripts SQL para entrega | P1 | Implementado localmente e validado | Estrutura coincide com migration; seed não contém dados pessoais; scripts executam em banco novo; instruções definem ordem. |
| PB-12 | Privacidade e retenção | P1 | Implementado e validado | Exclusão anonimiza dados e revoga sessão; retenção não executa sem data configurada; ações sensíveis são protegidas. |
| PB-13 | Navegação por ícones e fragmentos | P1 | Implementado e validado | Ícones navegam da página inicial e de páginas internas; ações de carrinho, perfil e administração continuam independentes. |
| PB-14 | Empacotamento Docker | P2 | Implementado, validação parcial | Imagem publica ASP.NET Core em porta 8080, executa sem root e não inclui banco, chaves, testes ou credenciais; `docker build` conclui em ambiente com Docker. |
| PB-15 | Integração contínua | P1 | Implementado, validação parcial | Runner Windows confirma LocalDB; restore, build e todos os testes são obrigatórios; imagem Docker é construída sem publicação após os testes. |
| PB-16 | Plano de nuvem e operação | P2 | Implementado como planejamento | Documento diferencia planejado de implantado e cobre HTTPS, conexão, segredos, logs, monitoramento, backup, restauração e escala. |
| PB-17 | Integração mobile | P1 | Dependência externa | Integrante mobile confirma contrato, autenticação e fluxos; equipe web apenas disponibiliza e documenta endpoints necessários; aplicação mobile não faz parte desta atribuição. |
| PB-18 | Health checks e chaveiro compartilhado | P3 local / P0 antes de escala real | Pendente | Endpoints de atividade e prontidão não expõem segredos; chaves de cookies persistem fora da instância antes de escala horizontal. |
| PB-19 | Homologação final multidispositivo | P1 | Pendente | Fluxos críticos passam em computador e celular; navegação funciona por teclado; gráficos são verificados nos três estados; evidências e limitações são registradas. |
| PB-20 | Documentação ágil e checklist final | P1 | Implementado nesta revisão | Backlogs, cronograma, Kanban, definição de pronto, checklist e resultados reais ficam versionados sem inventar histórico. |

## 4. Sprint Backlog proposto para fechamento

Não há registro suficiente para numerar esta sprint ou tratá-la como já iniciada. Abaixo está uma **sprint de fechamento proposta**, recomendada para cinco dias úteis após aprovação do escopo.

| ID | Tarefa proposta | Origem | Prioridade | Dependência | Resultado esperado |
|---|---|---|---:|---|---|
| SB-01 | Traduzir estados somente na apresentação administrativa | PB-07 | P1 | Nenhuma | Banco permanece inalterado e painel exibe textos naturais em português. |
| SB-02 | Separar carregamento, vazio e erro na análise e adicionar nova tentativa | PB-08 | P1 | Nenhuma | Mensagens finais e botão de nova tentativa funcionam sem alterar o estilo visual. |
| SB-03 | Fixar e armazenar Chart.js localmente | PB-09 | P1 | Licença do arquivo preservada | Gráficos funcionam com rede externa indisponível. |
| SB-04 | Criar testes ou roteiro reproduzível para dados, vazio e falha | PB-08, PB-09, PB-19 | P1 | SB-02 e SB-03 | Três estados validados e registrados em computador, celular e teclado. |
| SB-05 | Executar o workflow no GitHub e guardar o resultado | PB-15 | P1 | Arquivos locais integrados ao repositório | Job Windows comprova LocalDB e 100% dos testes; job Linux comprova `docker build`. |
| SB-06 | Revisar contrato necessário ao mobile e entregar ao integrante responsável | PB-17 | P1 | Disponibilidade do integrante mobile | Lista de endpoints, autenticação, erros e ambiente entregue; nenhum desenvolvimento do app atribuído ao web. |
| SB-07 | Rodar regressão completa e smoke test dos fluxos principais | PB-19 | P1 | SB-01 a SB-05 | Build sem erros, testes aprovados e navegação manual crítica registrada. |
| SB-08 | Consolidar documentação e pendências aceitas | PB-20 | P1 | SB-07 | Checklist final reflete evidências reais; limitações ficam explícitas. |
| SB-09 | Decidir health checks e armazenamento de chaves conforme destino da entrega | PB-18 | P2/P0 condicional | Decisão sobre implantação real | Item fica implementado antes de escala real ou formalmente fora do escopo da entrega local. |

## 5. Cronograma sugerido

O cronograma usa dias relativos para não inventar datas de calendário ou compromissos já ocorridos.

| Dia proposto | Foco | Saída verificável |
|---|---|---|
| Dia 1 | Textos administrativos e estados da análise | PB-07 e PB-08 implementados; revisão visual inicial. |
| Dia 2 | Chart.js local e testes dos três estados | PB-09 concluído; evidências com dados, vazio e falha. |
| Dia 3 | Regressão web, responsividade, teclado e contrato mobile | Roteiro manual concluído; contrato encaminhado ao integrante mobile. |
| Dia 4 | Integração contínua e contêiner | Workflow executado no GitHub; LocalDB e `docker build` comprovados ou falhas registradas. |
| Dia 5 | Banco limpo, checklist e pacote de entrega | Scripts conferidos, testes finais executados, documentação consolidada e pendências aceitas listadas. |

Se uma falha funcional for encontrada, ela volta para “A fazer” ou “Em andamento”; não deve ser marcada como pronta apenas para cumprir o cronograma.

## 6. Quadro Kanban

| Backlog | A fazer | Em validação | Concluído | Bloqueado / dependência externa |
|---|---|---|---|---|
| Health checks e chaveiro compartilhado antes de escala real | Traduzir estados administrativos<br>Distinguir vazio de erro nos gráficos<br>Adicionar tentativa novamente<br>Armazenar Chart.js localmente<br>Executar homologação multidispositivo | Dockerfile: falta `docker build` real<br>Pipeline: falta execução no GitHub<br>Materiais locais: aguardam integração ao `main` | Autenticação e autorização<br>Reservas e capacidade<br>Carrinho e histórico<br>Administração<br>Privacidade<br>Navegação do cabeçalho<br>SQL Server e scripts<br>Correção dos indicadores<br>Plano de nuvem<br>Documentação ágil | Integração e validação do aplicativo mobile — responsabilidade do integrante mobile<br>Recursos reais de nuvem — dependem de autorização e ambiente |

## 7. Definição de pronto

### Entrega web

- Critérios de aceitação do item atendidos sem quebrar permissões ou regras de negócio.
- Textos apresentados ao usuário estão em português; valores internos permanecem estáveis quando necessário.
- Fluxo funciona para visitante, usuário e administrador conforme a permissão.
- Estados de carregamento, sucesso, vazio e erro são acessíveis e não dependem somente de cor.
- Testes relacionados e suíte completa passam.
- Verificação manual inclui computador, celular e teclado quando houver interface.
- Não existem erros relevantes no console do navegador.

### Entrega de banco

- Entidades, `AppDbContext`, migration e scripts representam a mesma estrutura.
- Chaves, índices, restrições, procedures e trigger são validados em banco novo e isolado.
- Seed é fictício, idempotente e sem dados pessoais.
- Cancelamentos preservam histórico e indicadores respeitam estados.
- Instruções de execução e limitações estão atualizadas.

### Entrega de integração contínua e Docker

- Restore, build Release e todos os testes executam sem ignorar testes SQL Server.
- LocalDB é verificado explicitamente em runner Windows compatível.
- Dockerfile constrói uma imagem sem credenciais e executa como usuário não privilegiado.
- A pipeline de revisão não publica imagem nem implanta infraestrutura.
- Falha em teste, LocalDB ou build bloqueia o job.

### Entrega mobile — dependência externa

- Contrato web informa endpoints, autenticação, formatos e erros necessários.
- Ambiente de integração e dados fictícios são combinados com o integrante responsável.
- Integrante mobile confirma os fluxos consumidos no aplicativo.
- O desenvolvimento do aplicativo, seu build e seus testes permanecem com o integrante mobile.
- A integração só é considerada pronta quando ambos os lados registrarem a validação; ausência dessa confirmação permanece visível como dependência.

### Entrega de documentação

- Cada afirmação de conclusão possui evidência no código, teste ou registro de validação.
- Planejamento futuro está identificado como proposto.
- Não há nomes, reuniões, datas ou sprints inventados.
- Diagramas, comandos e caminhos correspondem ao repositório atual.
- Pendências, bloqueios e verificações não executadas aparecem explicitamente.

### Entrega de nuvem

- Plano diferencia recursos planejados de recursos implantados.
- HTTPS, segredos, conexão, logs, monitoramento, backup, restauração e escala estão cobertos.
- Nenhuma credencial é versionada.
- Antes de implantação real, conexão Azure SQL, identidade gerenciada, chaveiro compartilhado, health checks e restauração são validados em homologação.

## 8. Checklist final

### Web

- [x] Página pública consulta o banco e apresenta catálogo e sessões.
- [x] Cadastro, login, logout, perfis e sessão protegida estão cobertos por testes.
- [x] Ingressos, agendamentos, capacidade e cancelamentos estão cobertos por testes.
- [x] Carrinho, preço do servidor, histórico e cancelamento de compra estão cobertos por testes.
- [x] Administração respeita permissões e regras de catálogo e sessão.
- [x] Navegação do cabeçalho possui correção integrada ao `main`.
- [x] Indicadores de venda ignoram pedidos cancelados na árvore local e possuem teste.
- [ ] Estados administrativos aparecem em português.
- [ ] Tela de análise diferencia carregamento, vazio e erro.
- [ ] Falha na análise oferece nova tentativa.
- [ ] Chart.js está fixado e armazenado localmente.
- [ ] Gráficos foram verificados com dados, vazio e falha em computador, celular e teclado.

### Banco

- [x] Migration SQL Server corresponde ao modelo EF Core atual.
- [x] Procedures de reserva e cancelamento foram validadas em banco isolado.
- [x] Trigger de auditoria foi validada com alteração de capacidade.
- [x] Script estrutural contém tabelas, chaves, índices e restrições.
- [x] Seed separado é fictício, idempotente e sem dados pessoais.
- [x] Ordem de execução está documentada.
- [ ] Restauração em serviço de nuvem real foi testada — depende de infraestrutura não implantada.

### Integração

- [x] Contratos HTTP da aplicação web possuem testes de fluxo.
- [x] Perfis visitante e administrador foram testados.
- [x] Plano de CI exige runner Windows e LocalDB.
- [ ] Pipeline foi executada no GitHub após integração dos arquivos locais.
- [ ] Imagem Docker foi construída em ambiente com Docker.
- [ ] Contrato necessário ao mobile foi entregue e confirmado pelo integrante responsável.
- [ ] Fluxos mobile foram validados pelo integrante responsável pelo aplicativo.

### Testes e qualidade

- [x] Projeto usa xUnit com testes de API, negócio, migração e SQL Server.
- [x] Testes LocalDB criam bancos isolados e removem somente os bancos temporários.
- [x] Compilação e suíte completa foram executadas nesta revisão — resultado registrado na seção 9.
- [ ] Smoke test manual final foi documentado após as pendências visuais.
- [ ] Console do navegador foi verificado sem erros nos fluxos finais.

### Documentação

- [x] Scripts SQL e instruções estão preparados.
- [x] Plano de nuvem, diagrama e responsabilidades estão preparados.
- [x] Product Backlog, Sprint Backlog proposto, cronograma, Kanban e definições de pronto estão preparados.
- [x] Planejamento e histórico verificável estão separados.
- [x] Integração mobile está identificada como dependência de outro integrante.
- [ ] Evidência da execução real da CI foi anexada após envio ao GitHub.

## 9. Registro de validação desta revisão

Os comandos de validação devem ser executados ao final de qualquer atualização deste documento:

```powershell
dotnet build AuroraGaleria.Tests/AuroraGaleria.Tests.csproj --configuration Release --no-restore
dotnet test AuroraGaleria.Tests/AuroraGaleria.Tests.csproj --configuration Release --no-build
```

Resultado da revisão atual:

- Compilação Release: **aprovada, com 0 erros e 0 avisos**.
- Testes automatizados: **48 aprovados, 0 falhas e 0 ignorados**, incluindo os cenários SQL Server LocalDB e a execução dos scripts de entrega em banco isolado.
- Docker build: **não executável neste ambiente enquanto Docker não estiver instalado**.
- GitHub Actions: **não executado localmente; requer integração dos arquivos e execução no GitHub**.
- Testes mobile: **não executados; dependência do integrante responsável pelo aplicativo**.
