using System;
namespace UniversalPOS.Domain.Entities;

/// <summary>
/// Employee master data. Login credentials live in <see cref="User"/>, which links here optionally,
/// so staff without a login (e.g. warehouse workers) can still be recorded.
/// </summary>
public class Employee
{
    private Employee() { } // EF Core

    public Employee(string code, string fullName, string phone, string email,
        string jobTitle, int branchId, DateOnly hireDate, string? photoKey = null, int? departmentId = null)
    {
        Validate(code, fullName, phone, email, jobTitle, branchId);
        Code = code.Trim().ToUpperInvariant();
        FullName = fullName.Trim();
        Phone = phone.Trim();
        Email = email.Trim();
        JobTitle = jobTitle.Trim();
        BranchId = branchId;
        HireDate = hireDate;
        PhotoKey = string.IsNullOrWhiteSpace(photoKey) ? null : photoKey.Trim();
        DepartmentId = departmentId is > 0 ? departmentId : null;
        IsActive = true;
    }

    public int Id { get; private set; }
    /// <summary>Business code (generated automatically as EMP-0001, ...); unique. Never used as a foreign key.</summary>
    public string Code { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string JobTitle { get; private set; } = string.Empty;
    public int BranchId { get; private set; }
    public DateOnly HireDate { get; private set; }
    /// <summary>Relative key of the employee photo file; null when no photo was chosen.</summary>
    public string? PhotoKey { get; private set; }
    /// <summary>Optional department; null when the employee is not assigned to one.</summary>
    public int? DepartmentId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void UpdateDetails(string code, string fullName, string phone, string email,
        string jobTitle, int branchId, DateOnly hireDate, string? photoKey, int? departmentId = null)
    {
        Validate(code, fullName, phone, email, jobTitle, branchId);
        Code = code.Trim().ToUpperInvariant(); FullName = fullName.Trim(); Phone = phone.Trim(); Email = email.Trim();
        JobTitle = jobTitle.Trim(); BranchId = branchId; HireDate = hireDate;
        PhotoKey = string.IsNullOrWhiteSpace(photoKey) ? null : photoKey.Trim();
        DepartmentId = departmentId is > 0 ? departmentId : null;
    }

    private static void Validate(string code, string fullName, string phone, string email, string jobTitle, int branchId)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Employee code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("Full name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(phone)) throw new ArgumentException("Phone is required.", nameof(phone));
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(jobTitle)) throw new ArgumentException("Job title is required.", nameof(jobTitle));
        if (branchId <= 0) throw new ArgumentOutOfRangeException(nameof(branchId));
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
