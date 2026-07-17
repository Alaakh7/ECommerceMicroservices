using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductService.Domain.Entities;

namespace ProductService.Infrastructure.Configurations;

public sealed class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("InventoryTransactions", table => table.HasCheckConstraint("CK_InventoryTransactions_Quantity", "\"Quantity\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OperationId).HasMaxLength(150).IsRequired();
        builder.Property(x => x.OperationType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => new { x.ProductId, x.CreatedAtUtc });
        builder.HasOne(x => x.Product).WithMany(x => x.InventoryTransactions).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(x => !x.Product.IsDeleted);
    }
}
