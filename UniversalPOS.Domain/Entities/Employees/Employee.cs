using System;
namespace UniversalPOS.Domain.Entities;

/// <summary>
/// Employee master data. Login credentials live in <see cref="User"/>, which links here optionally,
/// so staff without a login (e.g. warehouse workers) can still be recorded.
/// </summary>
public class Employee
{
    private Employee() { } // EF Core

    public Employee(string code, string arabicName, string englishName, DateOnly birthDate, string phone,
        string jobTitle, int branchId, string qualification, string address, DateOnly hireDate)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Employee code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(arabicName)) throw new ArgumentException("Arabic name is required.", nameof(arabicName));
        if (string.IsNullOrWhiteSpace(englishName)) throw new ArgumentException("English name is required.", nameof(englishName));
        if (string.IsNullOrWhiteSpace(phone)) throw new ArgumentException("Phone is required.", nameof(phone));
        if (string.IsNullOrWhiteSpace(jobTitle)) throw new ArgumentException("Job title is required.", nameof(jobTitle));
        if (branchId <= 0) throw new ArgumentOutOfRangeException(nameof(branchId));
        if (string.IsNullOrWhiteSpace(qualification)) throw new ArgumentException("Qualification is required.", nameof(qualification));
        if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Address is required.", nameof(address));
        if (birthDate >= hireDate) throw new ArgumentException("Hire date must be after the birth date.", nameof(hireDate));

        Code = code.Trim().ToUpperInvariant();
        ArabicName = arabicName.Trim();
        EnglishName = englishName.Trim();
        BirthDate = birthDate;
        Phone = phone.Trim();
        JobTitle = jobTitle.Trim();
        BranchId = branchId;
        Qualification = qualification.Trim();
        Address = address.Trim();
        HireDate = hireDate;
        IsActive = true;
    }

    public int Id { get; private set; }
    /// <summary>Business code typed by the user; unique. Never used as a foreign key.</summary>
    public string Code { get; private set; } = string.Empty;
    public string ArabicName { get; private set; } = string.Empty;
    public string EnglishName { get; private set; } = string.Empty;
    public DateOnly BirthDate { get; private set; }
    public string Phone { get; private set; } = string.Empty;
    public string JobTitle { get; private set; } = string.Empty;
    public int BranchId { get; private set; }
    public string Qualification { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public DateOnly HireDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>Age is derived, never stored.</summary>
    public int AgeOn(DateOnly today)
    {
        int age = today.Year - BirthDate.Year;
        if (today < BirthDate.AddYears(age)) age--;
        return age;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
