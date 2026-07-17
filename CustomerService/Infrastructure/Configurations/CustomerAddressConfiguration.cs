using CustomerService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerService.Infrastructure.Configurations;

public sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("CustomerAddresses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Label).HasMaxLength(50);
        builder.Property(x => x.RecipientName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AddressLine1).HasMaxLength(250).IsRequired();
        builder.Property(x => x.AddressLine2).HasMaxLength(250);
        builder.Property(x => x.City).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StateOrProvince).HasMaxLength(100);
        builder.Property(x => x.PostalCode).HasMaxLength(20);
        builder.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength().IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(30);
        builder.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => new { x.CustomerId, x.IsDefaultShipping })
            .HasFilter("\"IsDeleted\" = FALSE AND \"IsDefaultShipping\" = TRUE").IsUnique();
        builder.HasIndex(x => new { x.CustomerId, x.IsDefaultBilling })
            .HasFilter("\"IsDeleted\" = FALSE AND \"IsDefaultBilling\" = TRUE").IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
