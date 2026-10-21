using Microsoft.EntityFrameworkCore;
using MS.Cobroup.Backend.Domain.Customers;

namespace MS.Cobroup.Backend.Infrastructure.Persistence;

public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerPhone> CustomerPhones => Set<CustomerPhone>();

    public DbSet<CustomerLocation> CustomerLocations => Set<CustomerLocation>();

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
    }
}
