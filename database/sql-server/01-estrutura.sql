IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [admin_audit_logs] (
    [Id] int NOT NULL IDENTITY,
    [AdministratorId] int NULL,
    [Action] nvarchar(80) NOT NULL,
    [EntityType] nvarchar(80) NOT NULL,
    [EntityId] int NOT NULL,
    [OccurredAtUtc] datetime2 NOT NULL,
    [Reason] nvarchar(500) NULL,
    [PreviousValues] nvarchar(max) NULL,
    [NewValues] nvarchar(max) NULL,
    CONSTRAINT [PK_admin_audit_logs] PRIMARY KEY ([Id])
);

CREATE TABLE [data_import_history] (
    [Id] int NOT NULL IDENTITY,
    [SourcePath] nvarchar(500) NOT NULL,
    [SourceSha256] nvarchar(64) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [StartedAtUtc] datetime2 NOT NULL,
    [CompletedAtUtc] datetime2 NULL,
    [Details] nvarchar(2000) NULL,
    CONSTRAINT [PK_data_import_history] PRIMARY KEY ([Id])
);

CREATE TABLE [data_migration_issues] (
    [Id] int NOT NULL IDENTITY,
    [Entity] nvarchar(80) NOT NULL,
    [LegacyId] int NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_data_migration_issues] PRIMARY KEY ([Id])
);

CREATE TABLE [Exposicoes] (
    [Id] int NOT NULL IDENTITY,
    [Titulo] nvarchar(140) NOT NULL,
    [Descricao] nvarchar(1000) NOT NULL,
    [ImagemUrl] nvarchar(300) NOT NULL,
    [Data] nvarchar(80) NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Exposicoes] PRIMARY KEY ([Id])
);

CREATE TABLE [idempotency_records] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [Scope] nvarchar(40) NOT NULL,
    [RequestId] nvarchar(64) NOT NULL,
    [ResultId] int NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_idempotency_records] PRIMARY KEY ([Id])
);

CREATE TABLE [Ingressos] (
    [Id] int NOT NULL IDENTITY,
    [NomeVisitante] nvarchar(max) NOT NULL,
    [Quantidade] int NOT NULL,
    [DataVisita] datetime2 NOT NULL,
    CONSTRAINT [PK_Ingressos] PRIMARY KEY ([Id])
);

CREATE TABLE [Produtos] (
    [Id] int NOT NULL IDENTITY,
    [Nome] nvarchar(120) NOT NULL,
    [Preco] decimal(18,2) NOT NULL,
    [ImagemUrl] nvarchar(300) NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Produtos] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Produtos_Preco] CHECK (Preco > 0)
);

CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [PasswordHash] nvarchar(200) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Role] nvarchar(20) NOT NULL,
    [IsActive] bit NOT NULL,
    [SessionVersion] nvarchar(64) NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

CREATE TABLE [visit_sessions] (
    [Id] int NOT NULL IDENTITY,
    [ExposureId] int NOT NULL,
    [VisitDate] date NOT NULL,
    [StartTime] time(0) NOT NULL,
    [Capacity] int NOT NULL,
    [ReservedQuantity] int NOT NULL,
    [IsActive] bit NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [CancellationReason] nvarchar(500) NULL,
    [CancelledAtUtc] datetime2 NULL,
    CONSTRAINT [PK_visit_sessions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_visit_sessions_Capacity] CHECK (Capacity > 0),
    CONSTRAINT [CK_visit_sessions_Occupancy] CHECK (ReservedQuantity >= 0 AND ReservedQuantity <= Capacity),
    CONSTRAINT [FK_visit_sessions_Exposicoes_ExposureId] FOREIGN KEY ([ExposureId]) REFERENCES [Exposicoes] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [account_deletion_requests] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [RequestedAtUtc] datetime2 NOT NULL,
    [CompletedAtUtc] datetime2 NULL,
    [CompletedByAdministratorId] int NULL,
    CONSTRAINT [PK_account_deletion_requests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_account_deletion_requests_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [cart_items] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [ProductId] int NOT NULL,
    [ProductName] nvarchar(120) NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [Quantity] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_cart_items] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_cart_items_Quantity] CHECK (Quantity > 0),
    CONSTRAINT [FK_cart_items_Produtos_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Produtos] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_cart_items_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [purchase_orders] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [RequestId] nvarchar(64) NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [TotalPrice] decimal(18,2) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CancelledAtUtc] datetime2 NULL,
    CONSTRAINT [PK_purchase_orders] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_purchase_orders_TotalPrice] CHECK (TotalPrice >= 0),
    CONSTRAINT [FK_purchase_orders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [user_appointments] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [EventName] nvarchar(140) NOT NULL,
    [AppointmentDate] datetime2 NOT NULL,
    [Quantity] int NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_user_appointments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_user_appointments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [user_purchases] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [ProductName] nvarchar(120) NOT NULL,
    [Quantity] int NOT NULL,
    [TotalPrice] decimal(18,2) NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_user_purchases] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_user_purchases_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [user_tickets] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [EventName] nvarchar(140) NOT NULL,
    [EventDate] datetime2 NOT NULL,
    [Quantity] int NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_user_tickets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_user_tickets_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [visit_reservations] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [VisitSessionId] int NOT NULL,
    [Type] nvarchar(20) NOT NULL,
    [Quantity] int NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [RequestId] nvarchar(64) NOT NULL,
    [ExposureName] nvarchar(140) NOT NULL,
    [VisitDate] date NOT NULL,
    [StartTime] time(0) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CancelledAtUtc] datetime2 NULL,
    [CancellationSource] nvarchar(20) NULL,
    [CancellationReason] nvarchar(500) NULL,
    CONSTRAINT [PK_visit_reservations] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_visit_reservations_Quantity] CHECK (Quantity > 0),
    CONSTRAINT [FK_visit_reservations_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_visit_reservations_visit_sessions_VisitSessionId] FOREIGN KEY ([VisitSessionId]) REFERENCES [visit_sessions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [purchase_items] (
    [Id] int NOT NULL IDENTITY,
    [PurchaseOrderId] int NOT NULL,
    [ProductId] int NULL,
    [ProductName] nvarchar(120) NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [Quantity] int NOT NULL,
    [TotalPrice] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_purchase_items] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_purchase_items_Quantity] CHECK (Quantity > 0),
    CONSTRAINT [FK_purchase_items_Produtos_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Produtos] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_purchase_items_purchase_orders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [purchase_orders] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_account_deletion_requests_UserId_Status] ON [account_deletion_requests] ([UserId], [Status]);

CREATE INDEX [IX_cart_items_ProductId] ON [cart_items] ([ProductId]);

CREATE UNIQUE INDEX [IX_cart_items_UserId_ProductId] ON [cart_items] ([UserId], [ProductId]);

CREATE INDEX [IX_data_import_history_SourceSha256] ON [data_import_history] ([SourceSha256]);

CREATE UNIQUE INDEX [IX_data_migration_issues_Entity_LegacyId] ON [data_migration_issues] ([Entity], [LegacyId]);

CREATE UNIQUE INDEX [IX_idempotency_records_UserId_Scope_RequestId] ON [idempotency_records] ([UserId], [Scope], [RequestId]);

CREATE INDEX [IX_purchase_items_ProductId] ON [purchase_items] ([ProductId]);

CREATE INDEX [IX_purchase_items_PurchaseOrderId] ON [purchase_items] ([PurchaseOrderId]);

CREATE UNIQUE INDEX [IX_purchase_orders_UserId_RequestId] ON [purchase_orders] ([UserId], [RequestId]);

CREATE INDEX [IX_user_appointments_UserId] ON [user_appointments] ([UserId]);

CREATE INDEX [IX_user_purchases_UserId] ON [user_purchases] ([UserId]);

CREATE INDEX [IX_user_tickets_UserId] ON [user_tickets] ([UserId]);

CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);

CREATE UNIQUE INDEX [IX_visit_reservations_UserId_RequestId] ON [visit_reservations] ([UserId], [RequestId]);

CREATE INDEX [IX_visit_reservations_VisitSessionId] ON [visit_reservations] ([VisitSessionId]);

CREATE UNIQUE INDEX [IX_visit_sessions_ExposureId_VisitDate_StartTime] ON [visit_sessions] ([ExposureId], [VisitDate], [StartTime]);

GO

CREATE OR ALTER PROCEDURE dbo.sp_ReserveVisitSession
    @UserId int,
    @SessionId int,
    @Quantity int,
    @Type nvarchar(20),
    @RequestId nvarchar(64),
    @CreatedAtUtc datetime2,
    @GalleryNow datetime2
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @ExistingId int;
        SELECT @ExistingId = Id
        FROM dbo.visit_reservations WITH (UPDLOCK, HOLDLOCK)
        WHERE UserId = @UserId AND RequestId = @RequestId;

        IF @ExistingId IS NOT NULL
        BEGIN
            COMMIT;
            SELECT N'duplicate' AS ResultCode, N'Solicitação já processada.' AS Message, @ExistingId AS ReservationId;
            RETURN;
        END;

        IF @Quantity < 1 OR @Quantity > 20 OR @Type NOT IN (N'Ticket', N'Appointment') OR NULLIF(LTRIM(RTRIM(@RequestId)), N'') IS NULL
        BEGIN
            COMMIT;
            SELECT N'invalid' AS ResultCode, N'A solicitação de reserva é inválida.' AS Message, CAST(NULL AS int) AS ReservationId;
            RETURN;
        END;

        IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @UserId AND IsActive = 1)
        BEGIN
            COMMIT;
            SELECT N'not_found' AS ResultCode, N'Usuário não encontrado ou inativo.' AS Message, CAST(NULL AS int) AS ReservationId;
            RETURN;
        END;

        DECLARE @VisitDate date, @StartTime time(0), @Capacity int, @Reserved int,
                @Status nvarchar(20), @SessionActive bit, @ExposureActive bit,
                @ExposureStart date, @ExposureEnd date, @ExposureName nvarchar(140);

        SELECT @VisitDate = session.VisitDate, @StartTime = session.StartTime,
               @Capacity = session.Capacity, @Reserved = session.ReservedQuantity,
               @Status = session.Status, @SessionActive = session.IsActive,
               @ExposureActive = exposure.IsActive, @ExposureStart = exposure.StartDate,
               @ExposureEnd = exposure.EndDate, @ExposureName = exposure.Titulo
        FROM dbo.visit_sessions AS session WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN dbo.Exposicoes AS exposure ON exposure.Id = session.ExposureId
        WHERE session.Id = @SessionId;

        IF @VisitDate IS NULL OR @SessionActive = 0 OR @Status <> N'Open' OR @ExposureActive = 0
        BEGIN
            COMMIT;
            SELECT N'closed' AS ResultCode, N'As reservas para esta sessão estão encerradas.' AS Message, CAST(NULL AS int) AS ReservationId;
            RETURN;
        END;

        DECLARE @SessionStart datetime2 = DATEADD(SECOND, DATEDIFF(SECOND, CONVERT(time(0), '00:00:00'), @StartTime), CONVERT(datetime2, @VisitDate));
        IF @VisitDate < CONVERT(date, @GalleryNow) OR @VisitDate < @ExposureStart OR @VisitDate > @ExposureEnd
        BEGIN
            COMMIT;
            SELECT N'invalid_date' AS ResultCode, N'Não é possível reservar uma data passada ou fora do período da exposição.' AS Message, CAST(NULL AS int) AS ReservationId;
            RETURN;
        END;
        IF @SessionStart <= @GalleryNow
        BEGIN
            COMMIT;
            SELECT N'started' AS ResultCode, N'Esta sessão já começou e não aceita novas reservas.' AS Message, CAST(NULL AS int) AS ReservationId;
            RETURN;
        END;
        IF @Reserved + @Quantity > @Capacity
        BEGIN
            COMMIT;
            SELECT N'capacity' AS ResultCode, N'Não há vagas suficientes nesta sessão.' AS Message, CAST(NULL AS int) AS ReservationId;
            RETURN;
        END;

        UPDATE dbo.visit_sessions
        SET ReservedQuantity = ReservedQuantity + @Quantity
        WHERE Id = @SessionId;

        INSERT INTO dbo.visit_reservations
            (UserId, VisitSessionId, Type, Quantity, Status, RequestId, ExposureName,
             VisitDate, StartTime, CreatedAt, CancelledAtUtc, CancellationSource, CancellationReason)
        VALUES
            (@UserId, @SessionId, @Type, @Quantity, N'Confirmed', @RequestId, @ExposureName,
             @VisitDate, @StartTime, @CreatedAtUtc, NULL, NULL, NULL);

        DECLARE @ReservationId int = CONVERT(int, SCOPE_IDENTITY());
        COMMIT;
        SELECT N'ok' AS ResultCode, N'Reserva confirmada.' AS Message, @ReservationId AS ReservationId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH;
END

GO

CREATE OR ALTER PROCEDURE dbo.sp_CancelVisitReservation
    @UserId int,
    @ReservationId int,
    @CancelledAtUtc datetime2,
    @GalleryNow datetime2
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status nvarchar(20), @Quantity int, @SessionId int,
                @VisitDate date, @StartTime time(0);
        SELECT @Status = reservation.Status, @Quantity = reservation.Quantity,
               @SessionId = reservation.VisitSessionId,
               @VisitDate = reservation.VisitDate, @StartTime = reservation.StartTime
        FROM dbo.visit_reservations AS reservation WITH (UPDLOCK, HOLDLOCK)
        WHERE reservation.Id = @ReservationId AND reservation.UserId = @UserId;

        IF @Status IS NULL
        BEGIN
            COMMIT;
            SELECT N'not_found' AS ResultCode, N'Reserva não encontrada.' AS Message, CAST(NULL AS int) AS ReservationId;
            RETURN;
        END;
        IF @Status = N'Cancelled'
        BEGIN
            COMMIT;
            SELECT N'already_cancelled' AS ResultCode, N'Reserva já estava cancelada.' AS Message, @ReservationId AS ReservationId;
            RETURN;
        END;

        DECLARE @SessionStart datetime2 = DATEADD(SECOND, DATEDIFF(SECOND, CONVERT(time(0), '00:00:00'), @StartTime), CONVERT(datetime2, @VisitDate));
        IF @SessionStart <= @GalleryNow
        BEGIN
            COMMIT;
            SELECT N'started' AS ResultCode, N'Esta sessão já começou e não pode mais ser cancelada.' AS Message, CAST(NULL AS int) AS ReservationId;
            RETURN;
        END;

        UPDATE dbo.visit_sessions
        SET ReservedQuantity = ReservedQuantity - @Quantity
        WHERE Id = @SessionId AND ReservedQuantity >= @Quantity;
        IF @@ROWCOUNT <> 1
        BEGIN
            ROLLBACK;
            SELECT N'consistency' AS ResultCode, N'Não foi possível atualizar a disponibilidade.' AS Message, CAST(NULL AS int) AS ReservationId;
            RETURN;
        END;

        UPDATE dbo.visit_reservations
        SET Status = N'Cancelled', CancelledAtUtc = @CancelledAtUtc,
            CancellationSource = N'Visitor', CancellationReason = NULL
        WHERE Id = @ReservationId AND Status = N'Confirmed';

        COMMIT;
        SELECT N'ok' AS ResultCode, N'Reserva cancelada.' AS Message, @ReservationId AS ReservationId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH;
END

GO

CREATE OR ALTER TRIGGER dbo.trg_visit_sessions_audit
ON dbo.visit_sessions
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.admin_audit_logs
        (AdministratorId, Action, EntityType, EntityId, OccurredAtUtc, Reason, PreviousValues, NewValues)
    SELECT
        TRY_CONVERT(int, SESSION_CONTEXT(N'AuroraAdminId')),
        CASE
            WHEN inserted.Status = N'Cancelled' AND deleted.Status <> N'Cancelled' THEN N'Cancel'
            WHEN inserted.Status <> deleted.Status THEN N'ChangeStatus'
            ELSE N'ChangeCapacity'
        END,
        N'VisitSession', inserted.Id, SYSUTCDATETIME(),
        CASE WHEN inserted.Status = N'Cancelled' THEN inserted.CancellationReason ELSE NULL END,
        (SELECT deleted.Capacity AS Capacity, deleted.ReservedQuantity AS ReservedQuantity,
                deleted.Status AS Status, deleted.CancellationReason AS CancellationReason
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
        (SELECT inserted.Capacity AS Capacity, inserted.ReservedQuantity AS ReservedQuantity,
                inserted.Status AS Status, inserted.CancellationReason AS CancellationReason
         FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    FROM inserted
    INNER JOIN deleted ON deleted.Id = inserted.Id
    WHERE inserted.Capacity <> deleted.Capacity
       OR inserted.Status <> deleted.Status
       OR ISNULL(inserted.CancellationReason, N'') <> ISNULL(deleted.CancellationReason, N'');
END

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261107161648_InitialSqlServer', N'10.0.8');

COMMIT;
GO
