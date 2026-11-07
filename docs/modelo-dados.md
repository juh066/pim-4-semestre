# Modelo de dados — PIM IV

## Modelo entidade-relacionamento

```mermaid
erDiagram
    Users ||--o{ visit_reservations : realiza
    Users ||--o{ purchase_orders : confirma
    Users ||--o{ cart_items : possui
    Users ||--o{ account_deletion_requests : solicita
    Exposicoes ||--o{ visit_sessions : oferece
    visit_sessions ||--o{ visit_reservations : recebe
    Produtos ||--o{ cart_items : compoe
    purchase_orders ||--|{ purchase_items : contem
    Produtos o|--o{ purchase_items : referencia

    Users {
        int Id PK
        nvarchar Email UK
        nvarchar PasswordHash
        nvarchar Role
        bit IsActive
    }
    Exposicoes {
        int Id PK
        nvarchar Titulo
        date StartDate
        date EndDate
    }
    visit_sessions {
        int Id PK
        int ExposureId FK
        date VisitDate
        time StartTime
        int Capacity
        int ReservedQuantity
        nvarchar Status
    }
    visit_reservations {
        int Id PK
        int UserId FK
        int VisitSessionId FK
        nvarchar Type
        int Quantity
        nvarchar Status
        nvarchar RequestId UK
    }
    Produtos {
        int Id PK
        nvarchar Nome
        decimal Preco
    }
    purchase_orders {
        int Id PK
        int UserId FK
        nvarchar RequestId UK
        decimal TotalPrice
        nvarchar Status
    }
    purchase_items {
        int Id PK
        int PurchaseOrderId FK
        int ProductId FK
        decimal UnitPrice
        decimal TotalPrice
    }
```

## Modelo lógico

`Users` concentra autenticação e perfil. Exposições possuem sessões; ingressos e agendamentos são tipos diferentes em `visit_reservations`, mas compartilham a capacidade da sessão. Pedidos e itens preservam nomes e valores confirmados, mesmo que o produto mude depois. Carrinho, idempotência, solicitações de exclusão, auditoria e históricos legados permanecem separados conforme sua finalidade.

As relações históricas usam exclusão restrita. Produtos podem ficar nulos somente nos itens históricos, que já guardam nome e preço. O carrinho é transitório e pode ser eliminado junto com a conta durante a anonimização.

## Modelo físico

- Chaves primárias: `int IDENTITY(1,1)`.
- Valores monetários: `decimal(18,2)`.
- Datas de exposição e sessão: `date`; horário local da sessão: `time(0)` no fuso de São Paulo.
- Instantes de criação, cancelamento e auditoria: `datetime2`, armazenados em UTC.
- E-mail possui índice único.
- Reservas possuem unicidade por usuário e chave da solicitação.
- Sessões possuem unicidade por exposição, data e horário.
- Restrições `CHECK` impedem preços ou quantidades inválidas e ocupação fora de `0..Capacity`.
- Chaves estrangeiras históricas usam `NO ACTION`; não há CPF no modelo.

As tabelas `data_import_history` e `data_migration_issues` registram a transferência e inconsistências legadas sem inventar relacionamentos. `admin_audit_logs` é reutilizada pela trigger de sessões.
