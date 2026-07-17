using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders"); b.HasKey(x => x.Id);
        b.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.CreateOperationId).HasMaxLength(150).IsRequired();
        b.Property(x => x.CartCheckoutOperationId).HasMaxLength(150);
        b.Property(x => x.CancellationOperationId).HasMaxLength(150);
        b.Property(x => x.CompletionOperationId).HasMaxLength(150);
        b.Property(x => x.FailureCode).HasMaxLength(100);
        b.Property(x => x.FailureMessage).HasMaxLength(500);
        foreach (var name in new[] { nameof(Order.Subtotal), nameof(Order.DiscountAmount), nameof(Order.TaxAmount), nameof(Order.ShippingAmount), nameof(Order.TotalAmount) })
            b.Property(name).HasPrecision(18, 2);
        b.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        b.HasIndex(x => x.OrderNumber).IsUnique();
        b.HasIndex(x => x.CreateOperationId).IsUnique();
        b.HasIndex(x => x.CartId);
        b.HasIndex(x => x.CartId).IsUnique().HasFilter("\"Status\" IN (1, 2, 3, 4, 7)");
        b.HasIndex(x => x.CustomerId); b.HasIndex(x => x.Status); b.HasIndex(x => x.CreatedAtUtc);
        b.HasIndex(x => x.UpdatedAtUtc); b.HasIndex(x => x.NextRetryAtUtc);
        b.HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Addresses).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.StatusHistory).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Operations).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("OrderItems"); b.HasKey(x => x.Id);
        b.Property(x => x.Sku).HasMaxLength(100).IsRequired(); b.Property(x => x.ProductName).HasMaxLength(250).IsRequired(); b.Property(x => x.ImageUrl).HasMaxLength(2048);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2); b.Property(x => x.LineTotal).HasPrecision(18, 2);
        b.Property(x => x.InventoryDecreaseOperationId).HasMaxLength(150).IsRequired(); b.Property(x => x.InventoryRestoreOperationId).HasMaxLength(150).IsRequired();
        b.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        b.HasIndex(x => new { x.OrderId, x.ProductId }).IsUnique(); b.HasIndex(x => x.InventoryDecreaseOperationId).IsUnique();
        b.HasIndex(x => x.InventoryRestoreOperationId).IsUnique(); b.HasIndex(x => x.InventoryStatus);
    }
}

public sealed class OrderAddressSnapshotConfiguration : IEntityTypeConfiguration<OrderAddressSnapshot>
{
    public void Configure(EntityTypeBuilder<OrderAddressSnapshot> b)
    {
        b.ToTable("OrderAddressSnapshots"); b.HasKey(x => x.Id);
        b.Property(x => x.RecipientName).HasMaxLength(200).IsRequired(); b.Property(x => x.AddressLine1).HasMaxLength(250).IsRequired();
        b.Property(x => x.AddressLine2).HasMaxLength(250); b.Property(x => x.City).HasMaxLength(100).IsRequired(); b.Property(x => x.StateOrProvince).HasMaxLength(100);
        b.Property(x => x.PostalCode).HasMaxLength(20); b.Property(x => x.CountryCode).HasMaxLength(2).IsRequired(); b.Property(x => x.PhoneNumber).HasMaxLength(30);
        b.HasIndex(x => new { x.OrderId, x.AddressType }).IsUnique();
    }
}

public sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> b)
    {
        b.ToTable("OrderStatusHistories"); b.HasKey(x => x.Id); b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.ChangedBy).HasMaxLength(50).IsRequired(); b.Property(x => x.CorrelationId).HasMaxLength(150);
        b.HasIndex(x => x.OrderId); b.HasIndex(x => x.CreatedAtUtc);
    }
}

public sealed class OrderOperationConfiguration : IEntityTypeConfiguration<OrderOperation>
{
    public void Configure(EntityTypeBuilder<OrderOperation> b)
    {
        b.ToTable("OrderOperations"); b.HasKey(x => x.Id); b.Property(x => x.OperationId).HasMaxLength(150).IsRequired();
        b.Property(x => x.RequestHash).HasMaxLength(64).IsRequired(); b.Property(x => x.ErrorCode).HasMaxLength(100); b.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        b.HasIndex(x => x.OperationId).IsUnique(); b.HasIndex(x => x.OrderId); b.HasIndex(x => x.Status); b.HasIndex(x => x.CreatedAtUtc);
    }
}
