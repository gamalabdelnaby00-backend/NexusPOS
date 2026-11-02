using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalPOS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeFormRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Address", table: "Employees");
            migrationBuilder.DropColumn(name: "ArabicName", table: "Employees");
            migrationBuilder.DropColumn(name: "BirthDate", table: "Employees");
            migrationBuilder.DropColumn(name: "EnglishName", table: "Employees");
            migrationBuilder.DropColumn(name: "Qualification", table: "Employees");

            migrationBuilder.AddColumn<string>(
                name: "Email", table: "Employees", type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(
                name: "FullName", table: "Employees", type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(
                name: "PhotoKey", table: "Employees", type: "nvarchar(300)", maxLength: 300, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Email", table: "Employees");
            migrationBuilder.DropColumn(name: "FullName", table: "Employees");
            migrationBuilder.DropColumn(name: "PhotoKey", table: "Employees");

            migrationBuilder.AddColumn<string>(
                name: "Address", table: "Employees", type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(
                name: "ArabicName", table: "Employees", type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate", table: "Employees", type: "date", nullable: false, defaultValue: new DateOnly(1, 1, 1));
            migrationBuilder.AddColumn<string>(
                name: "EnglishName", table: "Employees", type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(
                name: "Qualification", table: "Employees", type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: "");
        }
    }
}
