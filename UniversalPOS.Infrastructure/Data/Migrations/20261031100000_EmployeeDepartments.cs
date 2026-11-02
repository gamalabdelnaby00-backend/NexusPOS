using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalPOS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId", table: "Employees", type: "int", nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name", table: "Departments", column: "Name", unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId", table: "Employees", column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Departments_DepartmentId", table: "Employees", column: "DepartmentId",
                principalTable: "Departments", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Employees_Departments_DepartmentId", table: "Employees");
            migrationBuilder.DropIndex(name: "IX_Employees_DepartmentId", table: "Employees");
            migrationBuilder.DropColumn(name: "DepartmentId", table: "Employees");
            migrationBuilder.DropTable(name: "Departments");
        }
    }
}
