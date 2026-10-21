using Microsoft.EntityFrameworkCore;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Domain.LoanExceptions;
using MS.Cobroup.Backend.Domain.Loans;
using MS.Cobroup.Backend.Domain.Payments;

namespace MS.Cobroup.Backend.Infrastructure.Persistence;

public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerPhone> CustomerPhones => Set<CustomerPhone>();

    public DbSet<CustomerLocation> CustomerLocations => Set<CustomerLocation>();

    public DbSet<CustomerAuditRecord> CustomerAuditRecords => Set<CustomerAuditRecord>();

    public DbSet<LoanExceptionRequest> LoanExceptionRequests => Set<LoanExceptionRequest>();

    public DbSet<Loan> Loans => Set<Loan>();

    public DbSet<Installment> Installments => Set<Installment>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

    public DbSet<PaymentEvidence> PaymentEvidences => Set<PaymentEvidence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("operations");

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(customer => customer.Id);
            entity.Property(customer => customer.FirstNames).HasMaxLength(120).IsRequired();
            entity.Property(customer => customer.LastNames).HasMaxLength(120).IsRequired();
            entity.Property(customer => customer.BusinessOrWorkName).HasMaxLength(160);
            entity.Property(customer => customer.Observations).HasMaxLength(1000);
            entity.Property(customer => customer.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property<uint>("xmin").IsRowVersion();
            entity.HasIndex(customer => new { customer.TenantId, customer.RouteId, customer.Status });
            entity.HasIndex(customer => new { customer.TenantId, customer.LastNames, customer.FirstNames });
            entity.HasMany(customer => customer.Phones)
                .WithOne()
                .HasForeignKey(phone => phone.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(customer => customer.Locations)
                .WithOne()
                .HasForeignKey(location => location.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(customer => customer.Phones).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(customer => customer.Locations).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<CustomerPhone>(entity =>
        {
            entity.ToTable("customer_phones");
            entity.HasKey(phone => phone.Id);
            entity.Property(phone => phone.Number).HasMaxLength(21).IsRequired();
            entity.HasIndex(phone => new { phone.CustomerId, phone.Number }).IsUnique();
        });

        modelBuilder.Entity<CustomerLocation>(entity =>
        {
            entity.ToTable("customer_locations");
            entity.HasKey(location => location.Id);
            entity.Property(location => location.Latitude).HasPrecision(9, 6);
            entity.Property(location => location.Longitude).HasPrecision(9, 6);
            entity.Property(location => location.Alias).HasMaxLength(120);
            entity.Property(location => location.Address).HasMaxLength(300);
            entity.Property(location => location.Source).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.HasIndex(location => location.CustomerId);
        });

        modelBuilder.Entity<CustomerAuditRecord>(entity =>
        {
            entity.ToTable("customer_audit");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Action).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(record => record.DetailsJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(record => new { record.TenantId, record.OccurredAtUtc });
            entity.HasIndex(record => new { record.TenantId, record.CustomerId, record.OccurredAtUtc });
            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(record => record.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoanExceptionRequest>(entity =>
        {
            entity.ToTable("loan_exception_requests");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProposedAmount).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.ProposedDays).IsRequired();
            entity.Property(e => e.ProposedPercentage).HasPrecision(6, 2).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(e => e.CreatedByRole).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(e => e.DecisionNotes).HasMaxLength(500);
            entity.HasIndex(e => new { e.TenantId, e.RouteId, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.CustomerId, e.Status });
            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Loan>(entity =>
        {
            entity.ToTable("loans");
            entity.HasKey(loan => loan.Id);
            entity.Property(loan => loan.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(loan => loan.CreatedByRole).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.ComplexProperty(loan => loan.Terms, terms =>
            {
                terms.Property(t => t.PrincipalAmount).HasPrecision(18, 2).IsRequired();
                terms.Property(t => t.InterestRate).HasPrecision(5, 2).IsRequired();
                terms.Property(t => t.InterestAmount).HasPrecision(18, 2).IsRequired();
                terms.Property(t => t.TotalAmount).HasPrecision(18, 2).IsRequired();
                terms.Property(t => t.PaidAmount).HasPrecision(18, 2).IsRequired();
                terms.Property(t => t.BalanceAmount).HasPrecision(18, 2).IsRequired();
                terms.Property(t => t.PaymentFrequency).HasConversion<string>().HasMaxLength(20).IsRequired();
                terms.Property(t => t.FrequencyNDays);
                terms.Property(t => t.InstallmentCount).IsRequired();
                terms.Property(t => t.StartsAtUtc).IsRequired();
                terms.Property(t => t.DueAtUtc).IsRequired();
            });
            entity.HasIndex(loan => new { loan.TenantId, loan.RouteId, loan.Status });
            entity.HasIndex(loan => new { loan.TenantId, loan.CustomerId, loan.Status });
            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(loan => loan.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(loan => loan.Installments)
                .WithOne()
                .HasForeignKey(installment => installment.LoanId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(loan => loan.Installments).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Installment>(entity =>
        {
            entity.ToTable("installments");
            entity.HasKey(installment => installment.Id);
            entity.Property(installment => installment.ExpectedAmount).HasPrecision(18, 2).IsRequired();
            entity.Property(installment => installment.PaidAmount).HasPrecision(18, 2).IsRequired();
            entity.Property(installment => installment.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasIndex(installment => new { installment.TenantId, installment.LoanId, installment.Number }).IsUnique();
            entity.HasIndex(installment => new { installment.TenantId, installment.DueDate, installment.Status });
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Amount).HasPrecision(18, 2).IsRequired();
            entity.Property(p => p.PaymentMethodName).HasMaxLength(50).IsRequired();
            entity.Property(p => p.Notes).HasMaxLength(500);
            entity.Property(p => p.IdempotencyKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(p => new { p.TenantId, p.IdempotencyKey }).IsUnique();
            entity.HasIndex(p => new { p.TenantId, p.RouteId, p.RegisteredAtUtc });
            entity.HasIndex(p => new { p.TenantId, p.LoanId });
            entity.HasOne<Loan>()
                .WithMany()
                .HasForeignKey(p => p.LoanId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(p => p.Allocations)
                .WithOne()
                .HasForeignKey(a => a.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(p => p.Evidences)
                .WithOne()
                .HasForeignKey(e => e.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(p => p.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(p => p.Evidences).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PaymentAllocation>(entity =>
        {
            entity.ToTable("payment_allocations");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.AmountAllocated).HasPrecision(18, 2).IsRequired();
            entity.HasIndex(a => new { a.PaymentId, a.InstallmentId });
        });

        modelBuilder.Entity<PaymentEvidence>(entity =>
        {
            entity.ToTable("payment_evidences");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.StoragePath).HasMaxLength(500).IsRequired();
            entity.Property(e => e.DeletionReason).HasMaxLength(500);
            entity.HasIndex(e => new { e.TenantId, e.PaymentId, e.IsDeleted });
        });
    }
}
