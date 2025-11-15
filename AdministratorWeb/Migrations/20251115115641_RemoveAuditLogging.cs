using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdministratorWeb.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAuditLogging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingActionLogs");

            migrationBuilder.DropTable(
                name: "RequestActionLogs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingActionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Action = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActionedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AdjustmentId = table.Column<int>(type: "int", nullable: true),
                    AdjustmentType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CustomerId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomerName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IpAddress = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LaundryRequestId = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OldStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    PaymentMethod = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PerformedByUserEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PerformedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PerformedByUserName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingActionLogs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RequestActionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Action = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActionedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AssignedRobotName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomerId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomerName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IpAddress = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NewStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OldStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PerformedByUserEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PerformedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PerformedByUserName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    RequestType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TotalCost = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    WeightKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestActionLogs", x => x.Id);
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

            migrationBuilder.CreateIndex(
                name: "IX_RequestActionLogs_Action",
                table: "RequestActionLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_RequestActionLogs_ActionedAt",
                table: "RequestActionLogs",
                column: "ActionedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RequestActionLogs_CustomerId",
                table: "RequestActionLogs",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestActionLogs_CustomerId_ActionedAt",
                table: "RequestActionLogs",
                columns: new[] { "CustomerId", "ActionedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestActionLogs_RequestId",
                table: "RequestActionLogs",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestActionLogs_RequestId_ActionedAt",
                table: "RequestActionLogs",
                columns: new[] { "RequestId", "ActionedAt" });
        }
    }
}
