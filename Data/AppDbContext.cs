using Microsoft.EntityFrameworkCore;
using AuroraGaleria.Models;

namespace AuroraGaleria.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Exposicao> Exposicoes { get; set; }
        public DbSet<Produto> Produtos { get; set; }
        public DbSet<Ingresso> Ingressos { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<UserTicket> UserTickets { get; set; }
        public DbSet<UserAppointment> UserAppointments { get; set; }
        public DbSet<UserPurchase> UserPurchases { get; set; }
        public DbSet<VisitSession> VisitSessions { get; set; }
        public DbSet<VisitReservation> VisitReservations { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseItem> PurchaseItems { get; set; }
        public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }
        public DbSet<AccountDeletionRequest> AccountDeletionRequests { get; set; }
        public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasIndex(user => user.Email).IsUnique();
                entity.Property(user => user.Name).HasMaxLength(100).IsRequired();
                entity.Property(user => user.Email).HasMaxLength(256).IsRequired();
                entity.Property(user => user.PasswordHash).HasMaxLength(200).IsRequired();
                entity.Property(user => user.Role).HasMaxLength(20).IsRequired();
                entity.Property(user => user.SessionVersion).HasMaxLength(64).IsRequired();
            });

            modelBuilder.Entity<Exposicao>(entity =>
            {
                entity.ToTable("Exposicoes");
                entity.Property(item => item.Titulo).HasMaxLength(140).IsRequired();
                entity.Property(item => item.Descricao).HasMaxLength(1000);
                entity.Property(item => item.ImagemUrl).HasMaxLength(300).IsRequired();
                entity.Property(item => item.Periodo).HasColumnName("Data").HasMaxLength(80).IsRequired();
                entity.Property(item => item.Inicio).HasColumnName("StartDate");
                entity.Property(item => item.Fim).HasColumnName("EndDate");
                entity.Property(item => item.Ativa).HasColumnName("IsActive");
            });

            modelBuilder.Entity<Produto>(entity =>
            {
                entity.ToTable("Produtos");
                entity.Property(item => item.Nome).HasMaxLength(120).IsRequired();
                entity.Property(item => item.Preco).HasConversion<double>();
                entity.Property(item => item.ImagemUrl).HasMaxLength(300).IsRequired();
                entity.Property(item => item.Ativo).HasColumnName("IsActive");
            });

            modelBuilder.Entity<CartItem>(entity =>
            {
                entity.ToTable("cart_items");
                entity.HasIndex(item => new { item.UserId, item.ProductId }).IsUnique();
                entity.Property(item => item.ProductName).HasMaxLength(120).IsRequired();
                entity.Property(item => item.UnitPrice).HasConversion<double>();
                entity.Property(item => item.Quantity).IsRequired();
                entity.Property(item => item.CreatedAt).IsRequired();
                entity.HasOne(item => item.User)
                    .WithMany()
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.Product)
                    .WithMany()
                    .HasForeignKey(item => item.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<VisitSession>(entity =>
            {
                entity.ToTable("visit_sessions");
                entity.HasIndex(item => new { item.ExposureId, item.VisitDate, item.StartTime }).IsUnique();
                entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
                entity.Property(item => item.CancellationReason).HasMaxLength(500);
                entity.HasOne(item => item.Exposure)
                    .WithMany(item => item.Sessoes)
                    .HasForeignKey(item => item.ExposureId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<VisitReservation>(entity =>
            {
                entity.ToTable("visit_reservations");
                entity.HasIndex(item => new { item.UserId, item.RequestId }).IsUnique();
                entity.Property(item => item.Type).HasMaxLength(20).IsRequired();
                entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
                entity.Property(item => item.RequestId).HasMaxLength(64).IsRequired();
                entity.Property(item => item.ExposureName).HasMaxLength(140).IsRequired();
                entity.Property(item => item.CancellationSource).HasMaxLength(20);
                entity.Property(item => item.CancellationReason).HasMaxLength(500);
                entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(item => item.VisitSession).WithMany(item => item.Reservations).HasForeignKey(item => item.VisitSessionId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PurchaseOrder>(entity =>
            {
                entity.ToTable("purchase_orders");
                entity.HasIndex(item => new { item.UserId, item.RequestId }).IsUnique();
                entity.Property(item => item.TotalPrice).HasConversion<double>();
                entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PurchaseItem>(entity =>
            {
                entity.ToTable("purchase_items");
                entity.Property(item => item.UnitPrice).HasConversion<double>();
                entity.Property(item => item.TotalPrice).HasConversion<double>();
                entity.HasOne(item => item.PurchaseOrder).WithMany(item => item.Items).HasForeignKey(item => item.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.Product).WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<IdempotencyRecord>(entity =>
            {
                entity.ToTable("idempotency_records");
                entity.HasIndex(item => new { item.UserId, item.Scope, item.RequestId }).IsUnique();
            });

            modelBuilder.Entity<AccountDeletionRequest>(entity =>
            {
                entity.ToTable("account_deletion_requests");
                entity.HasIndex(item => new { item.UserId, item.Status });
                entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
                entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AdminAuditLog>(entity =>
            {
                entity.ToTable("admin_audit_logs");
                entity.Property(item => item.Action).HasMaxLength(80).IsRequired();
                entity.Property(item => item.EntityType).HasMaxLength(80).IsRequired();
                entity.Property(item => item.Reason).HasMaxLength(500);
            });

            modelBuilder.Entity<UserTicket>(entity =>
            {
                entity.ToTable("user_tickets");
                entity.Property(ticket => ticket.EventName).HasMaxLength(140).IsRequired();
                entity.Property(ticket => ticket.Status).HasMaxLength(20).IsRequired();
                entity.HasOne(ticket => ticket.User)
                    .WithMany()
                    .HasForeignKey(ticket => ticket.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserAppointment>(entity =>
            {
                entity.ToTable("user_appointments");
                entity.Property(appointment => appointment.EventName).HasMaxLength(140).IsRequired();
                entity.Property(appointment => appointment.Status).HasMaxLength(20).IsRequired();
                entity.HasOne(appointment => appointment.User)
                    .WithMany()
                    .HasForeignKey(appointment => appointment.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserPurchase>(entity =>
            {
                entity.ToTable("user_purchases");
                entity.Property(purchase => purchase.ProductName).HasMaxLength(120).IsRequired();
                entity.Property(purchase => purchase.TotalPrice).HasConversion<double>();
                entity.Property(purchase => purchase.Status).HasMaxLength(40).IsRequired();
                entity.HasOne(purchase => purchase.User)
                    .WithMany()
                    .HasForeignKey(purchase => purchase.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
