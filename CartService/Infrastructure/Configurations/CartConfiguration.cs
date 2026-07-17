using CartService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CartService.Infrastructure.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts", table =>
        {
            table.HasCheckConstraint("CK_Carts_Currency", "length(\"Currency\") = 3 AND \"Currency\" = upper(\"Currency\")");
            table.HasCheckConstraint("CK_Carts_Subtotal", "\"Subtotal\" >= 0");
            table.HasCheckConstraint("CK_Carts_TotalQuantity", "\"TotalQuantity\" >= 0");
            table.HasCheckConstraint("CK_Carts_DistinctItemCount", "\"DistinctItemCount\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.CheckoutOperationId).HasMaxLength(150);
        builder.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ExpiresAtUtc);
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.CheckoutOperationId).IsUnique().HasFilter("\"CheckoutOperationId\" IS NOT NULL");
        builder.HasIndex(x => x.CompletedOrderId);
        builder.HasIndex(x => x.CustomerId).IsUnique().HasDatabaseName("UX_Carts_Customer_Open")
            .HasFilter("\"Status\" IN (1, 2)");
        builder.HasMany(x => x.Items).WithOne(x => x.Cart).HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
    }
}
