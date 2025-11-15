using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdministratorWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingActionLogging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingActionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Action = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    LaundryRequestId = table.Column<int>(type: "int", nullable: true),
                    AdjustmentId = table.Column<int>(type: "int", nullable: true),
                    CustomerId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomerName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    OldStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NewStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PaymentMethod = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AdjustmentType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PerformedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PerformedByUserName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PerformedByUserEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IpAddress = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActionedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingActionLogs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingActionLogs_Action",
                table: "AccountingActionLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingActionLogs_ActionedAt",
                table: "AccountingActionLogs",
                column: "ActionedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingActionLogs_AdjustmentId",
                table: "AccountingActionLogs",
                column: "AdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingActionLogs_CustomerId",
                table: "AccountingActionLogs",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingActionLogs_CustomerId_ActionedAt",
                table: "AccountingActionLogs",
                columns: new[] { "CustomerId", "ActionedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingActionLogs_LaundryRequestId",
                table: "AccountingActionLogs",
                column: "LaundryRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingActionLogs_PaymentId",
                table: "AccountingActionLogs",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingActionLogs_PaymentId_ActionedAt",
                table: "AccountingActionLogs",
                columns: new[] { "PaymentId", "ActionedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingActionLogs");
        }
    }
}
