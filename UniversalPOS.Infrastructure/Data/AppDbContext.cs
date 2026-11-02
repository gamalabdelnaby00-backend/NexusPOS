using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Entities.Branches;
using UniversalPOS.Domain.Entities.Settings;
using UniversalPOS.Domain.Enums;
using UniversalPOS.Domain.Entities.Products.Translations;
using UniversalPOS.Infrastructure.Repositories;
using UniversalPOS.Application.Abstractions.Repositories;

namespace UniversalPOS.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options): base(options)
    {

    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductTranslation> ProductTranslations => Set<ProductTranslation>();
    public DbSet<CategoryTranslation> CategoryTranslations => Set<CategoryTranslation>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
    public DbSet<PosTerminal> PosTerminals => Set<PosTerminal>();
    public DbSet<CashShift> CashShifts => Set<CashShift>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<StockAdjustmentItem> StockAdjustmentItems => Set<StockAdjustmentItem>();
    public DbSet<SaleReturnPayment> SaleReturnPayments => Set<SaleReturnPayment>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<SaleReturnItem> SaleReturnItems => Set<SaleReturnItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Category
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedOnAdd();
            entity.Property(c => c.Name).IsRequired().HasMaxLength(200);
            entity.Property(c => c.IsActive).IsRequired();
            entity.Property(c => c.Code).HasMaxLength(50);
            entity.HasIndex(c => c.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
        });

        // Product
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).ValueGeneratedOnAdd();
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Price).HasPrecision(18, 2);
            entity.HasIndex(p => p.Barcode).IsUnique();
            entity.Property(p => p.Barcode).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Code).IsRequired().HasMaxLength(50);
            entity.HasIndex(p => p.Code).IsUnique();
            entity.Property(p => p.CostPrice).HasPrecision(18, 2);
            entity.Property(p => p.MinStock).HasPrecision(18, 3);
            entity.Property(p => p.ImageKey).HasMaxLength(500);
            entity.Property(p => p.UnitOfMeasure).HasConversion<int>().IsRequired();
            entity.Property(p => p.IsActive).IsRequired();
            entity.HasOne<Category>().WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        // Localized product and category display text. One translation per language per record.
        modelBuilder.Entity<ProductTranslation>(entity =>
        {
            entity.HasKey(x => new { x.ProductId, x.LanguageCode });
            entity.Property(x => x.LanguageCode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CategoryTranslation>(entity =>
        {
            entity.HasKey(x => new { x.CategoryId, x.LanguageCode });
            entity.Property(x => x.LanguageCode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        // Sale
        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).ValueGeneratedOnAdd();
            entity.Property(s => s.SaleDate).IsRequired();
            entity.Property(s => s.PaymentStatus).HasConversion<int>();
            entity.Property(s => s.Status).HasConversion<int>();
            entity.Property(s => s.OperationId);
            entity.HasIndex(s => s.OperationId).IsUnique().HasFilter("[OperationId] IS NOT NULL");
            entity.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CashShift>().WithMany().HasForeignKey(s => s.CashShiftId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PosTerminal>().WithMany().HasForeignKey(s => s.TerminalId).OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(s => s.TotalAmount);entity.Ignore(s => s.PaidAmount);
            entity.Ignore(s => s.Subtotal);entity.Ignore(s => s.TaxableAmount);entity.Ignore(s => s.TaxAmount);
            entity.Property(s => s.DiscountAmount).HasPrecision(18, 2);
            entity.Property(s => s.TaxRate).HasPrecision(5, 2);
            entity.Property(s => s.TaxName).HasMaxLength(100);
            entity.Property(s => s.CustomerPhone).HasMaxLength(30);
            entity.HasOne<User>().WithMany().HasForeignKey(s => s.DiscountAppliedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(s => s.Items).WithOne().HasForeignKey("SaleId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(s => s.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(s => s.Payments).WithOne().HasForeignKey("SaleId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(s => s.Payments).HasField("_payments").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // SaleItem
        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Id).ValueGeneratedOnAdd();
            entity.Property(i => i.UnitPrice).HasPrecision(18, 2);
            entity.Property(i => i.Quantity).HasPrecision(18, 3);
            entity.Ignore(i => i.TotalPrice);
            entity.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // Payment
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).ValueGeneratedOnAdd();
            entity.Property(p => p.Amount).HasPrecision(18, 2);
            entity.Property(p => p.Method).HasConversion<int>();
        });

        // User security
        modelBuilder.Entity<User>(e => 
        { 
            e.HasKey(x=>x.Id); 
            e.Property(x=>x.UserName).HasMaxLength(100).IsRequired(); 
            e.HasIndex(x=>x.UserName).IsUnique(); 
            e.Property(x=>x.DisplayName).HasMaxLength(200).IsRequired();
            e.Property(x=>x.PasswordHash).HasMaxLength(500).IsRequired(); 
        });
        modelBuilder.Entity<Role>(e => 
        { 
            e.HasKey(x=>x.Id); 
            e.Property(x=>x.Name).HasMaxLength(100).IsRequired(); 
            e.HasIndex(x=>x.Name).IsUnique(); 
            e.Property(x=>x.Description).HasMaxLength(500); 
        });
        modelBuilder.Entity<Permission>(e => 
        { 
            e.HasKey(x=>x.Id); 
            e.Property(x=>x.Code).HasMaxLength(150).IsRequired(); 
            e.HasIndex(x=>x.Code).IsUnique(); 
            e.Property(x=>x.Name).HasMaxLength(200).IsRequired(); 
            e.Property(x=>x.Module).HasMaxLength(100).IsRequired(); 
            e.Property(x=>x.Description).HasMaxLength(500); 
        });
        modelBuilder.Entity<UserRole>(e => 
        { 
            e.HasKey(x=>new {x.UserId,x.RoleId}); 
            e.HasOne(x=>x.User).WithMany(x=>x.UserRoles).HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade); 
            e.HasOne(x=>x.Role).WithMany(x=>x.UserRoles).HasForeignKey(x=>x.RoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<User>().Navigation(x=>x.UserRoles).HasField("_userRoles").UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.Entity<User>().Navigation(x=>x.PermissionOverrides).HasField("_permissionOverrides").UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.Entity<Role>().Navigation(x=>x.UserRoles).HasField("_userRoles").UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.Entity<Role>().Navigation(x=>x.RolePermissions).HasField("_rolePermissions").UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.Entity<Permission>().Navigation(x=>x.RolePermissions).HasField("_rolePermissions").UsePropertyAccessMode(PropertyAccessMode.Field);
        modelBuilder.Entity<RolePermission>(e => { e.HasKey(x=>new {x.RoleId,x.PermissionId}); e.HasOne(x=>x.Role)
            .WithMany(x=>x.RolePermissions).HasForeignKey(x=>x.RoleId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x=>x.Permission)
            .WithMany(x=>x.RolePermissions).HasForeignKey(x=>x.PermissionId).OnDelete(DeleteBehavior.Restrict); });
        modelBuilder.Entity<UserPermissionOverride>(e => 
        { 
            e.HasKey(x=>x.Id); 
            e.Property(x=>x.Reason).HasMaxLength(1000).IsRequired(); 
            e.HasIndex(x=>new {x.UserId,x.PermissionId}).IsUnique(); 
            e.HasOne(x=>x.User).WithMany(x=>x.PermissionOverrides).HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade); 
            e.HasOne(x=>x.Permission).WithMany().HasForeignKey(x=>x.PermissionId).OnDelete(DeleteBehavior.Restrict); 
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // Branches (single MAIN branch today; the schema allows adding more later)
        modelBuilder.Entity<Branch>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.TaxRateOverride).HasPrecision(5, 2);
        });

        // Employees: business Code is unique; relationships use the internal Id.
        modelBuilder.Entity<Employee>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.JobTitle).HasMaxLength(100).IsRequired();
            e.Property(x => x.PhotoKey).HasMaxLength(300);
            e.Property(x => x.HireDate).HasColumnType("date");
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Department>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        // One login account per employee at most.
        modelBuilder.Entity<User>(e =>
        {
            e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("[EmployeeId] IS NOT NULL");
        });

        modelBuilder.Entity<SystemSetting>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TaxName).HasMaxLength(100).IsRequired();
            e.Property(x => x.TaxRate).HasPrecision(5, 2);
            e.Property(x => x.MaxCashierDiscountPercent).HasPrecision(5, 2);
            e.Property(x => x.DefaultLanguage).HasMaxLength(20).IsRequired();
            e.Property(x => x.DefaultTheme).HasMaxLength(30).IsRequired();
        });

        // Terminals and cash custody
        modelBuilder.Entity<PosTerminal>(e=>
        {
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Code).HasMaxLength(100).IsRequired();
            e.HasIndex(x=>x.Code).IsUnique();
            e.Property(x=>x.Name).HasMaxLength(200).IsRequired();
        });
        modelBuilder.Entity<CashShift>(e=>
        {
            e.HasKey(x=>x.Id);
            e.Property(x=>x.OpeningFloat).HasPrecision(18,2);
            e.Property(x=>x.CountedCash).HasPrecision(18,2);
            e.Property(x=>x.ExpectedCash).HasPrecision(18,2);
            e.Property(x=>x.Difference).HasPrecision(18,2);
            e.Property(x=>x.Status).HasConversion<int>();
            e.Property(x=>x.CloseNotes).HasMaxLength(1000);
            e.HasOne<User>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x=>x.DifferenceApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PosTerminal>().WithMany().HasForeignKey(x=>x.TerminalId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.TerminalId).IsUnique().HasFilter("[Status] = 0");
            e.HasIndex(x => x.UserId).IsUnique().HasFilter("[Status] = 0");
        });
        modelBuilder.Entity<CashMovement>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            e.HasOne<CashShift>().WithMany().HasForeignKey(x => x.CashShiftId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CashMovement>().WithMany().HasForeignKey(x => x.ReversesMovementId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ReversesMovementId).IsUnique().HasFilter("[ReversesMovementId] IS NOT NULL");
        });

        // Purchases and inventory ledger
        modelBuilder.Entity<Supplier>(e=>
        {
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Name).HasMaxLength(200).IsRequired();
            e.Property(x=>x.Phone).HasMaxLength(50);
            e.Property(x=>x.TaxNumber).HasMaxLength(100);
        });

        modelBuilder.Entity<Purchase>(e =>
        {
            e.HasKey(x => x.Id);
            e.Ignore(x => x.TotalAmount);
            e.Property(x => x.SupplierInvoiceNumber).HasMaxLength(100);
            e.Property(x => x.Status).HasConversion<int>();
            e.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey("PurchaseId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PurchaseItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UnitCost).HasPrecision(18, 2);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Ignore(x => x.LineTotal);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StockMovement>(e=>
        {
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Type).HasConversion<int>();
            e.Property(x=>x.QuantityChange).HasPrecision(18,3);
            e.Property(x=>x.ReferenceType).HasMaxLength(100).IsRequired();
            e.Property(x=>x.ReferenceId).HasMaxLength(100).IsRequired();
            e.Property(x=>x.Reason).HasMaxLength(1000).IsRequired();
            e.HasOne<Product>().WithMany().HasForeignKey(x=>x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x=>new{x.ProductId,x.CreatedAtUtc});           
        });

        // Current stock balance: one row per product, SQL Server rowversion for optimistic concurrency.
        modelBuilder.Entity<StockBalance>(e =>
        {
            e.HasKey(x => x.ProductId);
            e.Property(x => x.Quantity).HasPrecision(18, 3).IsRequired();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.ToTable(t => t.HasCheckConstraint("CK_StockBalances_Quantity_NonNegative", "[Quantity] >= 0"));
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockAdjustment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Status).HasConversion<int>();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.StockAdjustmentId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        modelBuilder.Entity<StockAdjustmentItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SystemQuantity).HasPrecision(18, 3);
            e.Property(x => x.CountedQuantity).HasPrecision(18, 3);
            e.Ignore(x => x.Difference);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // Returns and audit
        modelBuilder.Entity<SaleReturn>(e=>
        {
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Status).HasConversion<int>();
            e.Property(x=>x.Reason).HasMaxLength(1000).IsRequired();
            e.Ignore(x=>x.TotalRefund);
            e.HasOne<Sale>().WithMany().HasForeignKey(x=>x.SaleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x=>x.ProcessedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x=>x.Items).WithOne().HasForeignKey("SaleReturnId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x=>x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        modelBuilder.Entity<SaleReturnItem>(e=>
        {
            e.HasKey(x=>x.Id);
            e.Property(x=>x.UnitRefund).HasPrecision(18,2);
            e.Property(x=>x.Quantity).HasPrecision(18,3);
            e.Ignore(x=>x.LineTotal);
            e.HasOne<SaleItem>().WithMany().HasForeignKey(x=>x.SaleItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Product>().WithMany().HasForeignKey(x=>x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SaleReturnPayment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Method).HasConversion<int>();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.ExternalReference).HasMaxLength(200);
            e.HasOne<SaleReturn>().WithMany().HasForeignKey(x => x.SaleReturnId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ProcessedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CashShift>().WithMany().HasForeignKey(x => x.CashShiftId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Payment>().WithMany().HasForeignKey(x => x.OriginalPaymentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AuditLog>(e=>
        {
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Id).ValueGeneratedOnAdd();
            e.Property(x=>x.Action).HasMaxLength(150).IsRequired();
            e.Property(x=>x.EntityName).HasMaxLength(150).IsRequired();
            e.Property(x=>x.EntityId).HasMaxLength(100);
            e.Property(x=>x.Details).HasMaxLength(4000);
            e.Property(x=>x.TerminalCode).HasMaxLength(100);
            e.HasIndex(x=>x.CreatedAtUtc);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}