using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CartService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCartServiceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Carts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalQuantity = table.Column<int>(type: "integer", nullable: false),
                    DistinctItemCount = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CheckoutToken = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckoutOperationId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CheckoutExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckedOutAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AbandonedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carts", x => x.Id);
                    table.CheckConstraint("CK_Carts_Currency", "length(\"Currency\") = 3 AND \"Currency\" = upper(\"Currency\")");
                    table.CheckConstraint("CK_Carts_DistinctItemCount", "\"DistinctItemCount\" >= 0");
                    table.CheckConstraint("CK_Carts_Subtotal", "\"Subtotal\" >= 0");
                    table.CheckConstraint("CK_Carts_TotalQuantity", "\"TotalQuantity\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "CartItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProductConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartItems", x => x.Id);
                    table.CheckConstraint("CK_CartItems_LineTotal", "\"LineTotal\" >= 0");
                    table.CheckConstraint("CK_CartItems_Quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_CartItems_UnitPrice", "\"UnitPrice\" > 0");
                    table.ForeignKey(
                        name: "FK_CartItems_Carts_CartId",
                        column: x => x.CartId,
                        principalTable: "Carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_ProductId",
                table: "CartItems",
                columns: new[] { "CartId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ProductId",
                table: "CartItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_CheckoutOperationId",
                table: "Carts",
                column: "CheckoutOperationId",
                unique: true,
                filter: "\"CheckoutOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_CompletedOrderId",
                table: "Carts",
                column: "CompletedOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_CreatedAtUtc",
                table: "Carts",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_ExpiresAtUtc",
                table: "Carts",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_Status",
                table: "Carts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UX_Carts_Customer_Open",
                table: "Carts",
                column: "CustomerId",
                unique: true,
                filter: "\"Status\" IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CartItems");

            migrationBuilder.DropTable(
                name: "Carts");
        }
    }
}
