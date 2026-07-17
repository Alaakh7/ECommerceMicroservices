using CustomerService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerService.Infrastructure.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CustomerNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(30);
        builder.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(x => x.CustomerNumber).IsUnique();
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.HasIndex(x => x.FirstName);
        builder.HasIndex(x => x.LastName);
        builder.HasIndex(x => x.PhoneNumber);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.IsDeleted);
        builder.HasMany(x => x.Addresses).WithOne(x => x.Customer).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
