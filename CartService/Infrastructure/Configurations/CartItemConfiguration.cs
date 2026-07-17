using CartService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CartService.Infrastructure.Configurations;

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems", table =>
        {
            table.HasCheckConstraint("CK_CartItems_UnitPrice", "\"UnitPrice\" > 0");
            table.HasCheckConstraint("CK_CartItems_Quantity", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_CartItems_LineTotal", "\"LineTotal\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Sku).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(2048);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique();
        builder.HasIndex(x => x.ProductId);
    }
}
