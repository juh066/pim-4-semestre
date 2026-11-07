# Regras de negócio — Galeria Aurora

## Cadastro e privacidade

O cadastro exige nome, e-mail válido e senha. O CPF não é coletado. Usuários, reservas, agendamentos e pedidos são relacionados pelo identificador interno da conta. Senhas permanecem armazenadas somente como hash BCrypt.

O sistema armazena dados de conta, reservas/agendamentos, carrinho, pedidos simulados e registros administrativos para operar e demonstrar o projeto. Visitantes acessam seus próprios registros; administradores acessam operações, solicitações de exclusão e auditoria.

A área do usuário permite solicitar exclusão. A conclusão administrativa revoga cookies por meio da versão de sessão, desativa a conta, substitui nome/e-mail/senha por valores não recuperáveis, remove carrinho e chaves auxiliares e cancela reservas futuras liberando vagas. Históricos permanecem ligados apenas à conta anonimizada para integridade acadêmica.

## Retenção

Dados pessoais da demonstração devem ser eliminados até 30 dias após a avaliação final. É uma decisão do projeto, não um prazo legal. Configure `Retention:FinalEvaluationDate` no formato `AAAA-MM-DD`; enquanto estiver ausente, nenhuma limpeza é executada.

```powershell
dotnet run -- --retention-preview
dotnet run -- --retention-cleanup --confirm-retention-cleanup
```

O primeiro comando apenas apresenta o impacto. O segundo exige confirmação explícita e só executa após o prazo. A rotina anonimiza; não cria outro backup contendo os dados. Backups externos e o backup anterior à remoção do CPF estão fora do alcance da aplicação e devem ser localizados e eliminados manualmente até o mesmo prazo.

## Fuso horário

O fuso da galeria é `America/Sao_Paulo`, com fallback Windows `E. South America Standard Time`. Datas e horários de sessões representam o horário local da galeria e não foram deslocados na migração. Instantes de criação, cancelamento e auditoria são UTC. Campos que representam somente datas continuam sendo datas.

## Estados e transições

- Sessão: `Open` aceita reservas; `Closed` preserva reservas e bloqueia novas; `Cancelled` cancela reservas ativas com motivo; `Completed` representa sessão iniciada/concluída e não aceita alterações.
- Reserva/agendamento: `Confirmed` pode ser cancelada pelo titular somente antes do início; `Cancelled` preserva histórico, origem (`Visitor` ou `Gallery`), instante e motivo quando informado pela galeria.
- Pedido simulado: `Confirmed` pode ser cancelado pelo titular; `Cancelled` preserva itens e valores. Não existe reembolso, transporte ou processamento financeiro.
- Solicitação de exclusão: `Pending` até a conclusão administrativa; `Completed` após anonimização.

## Capacidade e administração

Ingressos e agendamentos usam a mesma contagem de vagas. Atualizações condicionais em transação impedem ultrapassar a capacidade. Cancelamento libera cada vaga uma única vez; repetição é idempotente. A capacidade pode aumentar e só pode diminuir até o total já ocupado.

Sessões com reservas não permitem mudança de exposição, data ou horário. Para isso, o administrador cancela a sessão com motivo e cria outra. Encerrar novas reservas muda para `Closed` e preserva as existentes. O cancelamento administrativo exige motivo, cancela registros ativos relacionados e zera a ocupação em uma única transação.

Alterações relevantes de sessão registram administrador, ação, registro, UTC, motivo e valores anteriores/novos, sem senhas, códigos administrativos ou dados pessoais desnecessários.

## Migração, restauração e limpeza

O inicializador aplica a versão 3 incrementalmente: remove o índice e a coluna `Cpf`, acrescenta estados/cancelamentos, exclusões e auditoria, preservando IDs, e-mails, hashes, perfis e históricos. Não recria contas nem o banco. Antes da aplicação foi criado `backups/aurora-before-business-rules-20261107.db`.

Para restaurar, pare a aplicação, preserve o banco atual para diagnóstico e copie o backup escolhido para `aurora.db`. O backup anterior à versão 3 ainda contém CPF e deve ter retenção limitada.

## Decisões ainda pendentes

- Informar a data real da avaliação final em configuração.
- Identificar e controlar todas as cópias de segurança externas.
- Planejar migração posterior para SGBD com procedures/triggers, incluindo rollback e testes de concorrência.
- Definir requisitos do futuro aplicativo móvel e operação real; pagamentos continuam fora do escopo.
