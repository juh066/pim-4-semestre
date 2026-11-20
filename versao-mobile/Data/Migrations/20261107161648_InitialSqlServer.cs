using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuroraGaleria.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_audit_logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdministratorId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PreviousValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "data_import_history",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourcePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_import_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "data_migration_issues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Entity = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LegacyId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_migration_issues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Exposicoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ImagemUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Data = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exposicoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResultId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ingressos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NomeVisitante = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    DataVisita = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingressos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Produtos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Preco = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ImagemUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Produtos", x => x.Id);
                    table.CheckConstraint("CK_Produtos_Preco", "Preco > 0");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SessionVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "visit_sessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExposureId = table.Column<int>(type: "int", nullable: false),
                    VisitDate = table.Column<DateTime>(type: "date", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time(0)", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visit_sessions", x => x.Id);
                    table.CheckConstraint("CK_visit_sessions_Capacity", "Capacity > 0");
                    table.CheckConstraint("CK_visit_sessions_Occupancy", "ReservedQuantity >= 0 AND ReservedQuantity <= Capacity");
                    table.ForeignKey(
                        name: "FK_visit_sessions_Exposicoes_ExposureId",
                        column: x => x.ExposureId,
                        principalTable: "Exposicoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "account_deletion_requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByAdministratorId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_deletion_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_deletion_requests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cart_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cart_items", x => x.Id);
                    table.CheckConstraint("CK_cart_items_Quantity", "Quantity > 0");
                    table.ForeignKey(
                        name: "FK_cart_items_Produtos_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cart_items_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.Id);
                    table.CheckConstraint("CK_purchase_orders_TotalPrice", "TotalPrice >= 0");
                    table.ForeignKey(
                        name: "FK_purchase_orders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_appointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    AppointmentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_appointments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_purchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_purchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_purchases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_tickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    EventDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_tickets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "visit_reservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    VisitSessionId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExposureName = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    VisitDate = table.Column<DateTime>(type: "date", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time(0)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visit_reservations", x => x.Id);
                    table.CheckConstraint("CK_visit_reservations_Quantity", "Quantity > 0");
                    table.ForeignKey(
                        name: "FK_visit_reservations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_visit_reservations_visit_sessions_VisitSessionId",
                        column: x => x.VisitSessionId,
                        principalTable: "visit_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_items", x => x.Id);
                    table.CheckConstraint("CK_purchase_items_Quantity", "Quantity > 0");
                    table.ForeignKey(
                        name: "FK_purchase_items_Produtos_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_purchase_items_purchase_orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_deletion_requests_UserId_Status",
                table: "account_deletion_requests",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_ProductId",
                table: "cart_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_UserId_ProductId",
                table: "cart_items",
                columns: new[] { "UserId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_import_history_SourceSha256",
                table: "data_import_history",
                column: "SourceSha256");

            migrationBuilder.CreateIndex(
                name: "IX_data_migration_issues_Entity_LegacyId",
                table: "data_migration_issues",
                columns: new[] { "Entity", "LegacyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_UserId_Scope_RequestId",
                table: "idempotency_records",
                columns: new[] { "UserId", "Scope", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_items_ProductId",
                table: "purchase_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_items_PurchaseOrderId",
                table: "purchase_items",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_UserId_RequestId",
                table: "purchase_orders",
                columns: new[] { "UserId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_appointments_UserId",
                table: "user_appointments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_purchases_UserId",
                table: "user_purchases",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_tickets_UserId",
                table: "user_tickets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_visit_reservations_UserId_RequestId",
                table: "visit_reservations",
                columns: new[] { "UserId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_visit_reservations_VisitSessionId",
                table: "visit_reservations",
                column: "VisitSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_visit_sessions_ExposureId_VisitDate_StartTime",
                table: "visit_sessions",
                columns: new[] { "ExposureId", "VisitDate", "StartTime" },
                unique: true);

            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.trg_visit_sessions_audit");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_CancelVisitReservation");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_ReserveVisitSession");

            migrationBuilder.DropTable(
                name: "account_deletion_requests");

            migrationBuilder.DropTable(
                name: "admin_audit_logs");

            migrationBuilder.DropTable(
                name: "cart_items");

            migrationBuilder.DropTable(
                name: "data_import_history");

            migrationBuilder.DropTable(
                name: "data_migration_issues");

            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropTable(
                name: "Ingressos");

            migrationBuilder.DropTable(
                name: "purchase_items");

            migrationBuilder.DropTable(
                name: "user_appointments");

            migrationBuilder.DropTable(
                name: "user_purchases");

            migrationBuilder.DropTable(
                name: "user_tickets");

            migrationBuilder.DropTable(
                name: "visit_reservations");

            migrationBuilder.DropTable(
                name: "Produtos");

            migrationBuilder.DropTable(
                name: "purchase_orders");

            migrationBuilder.DropTable(
                name: "visit_sessions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Exposicoes");
        }
    }
}
