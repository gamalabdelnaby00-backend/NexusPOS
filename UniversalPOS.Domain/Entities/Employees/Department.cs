using System;
namespace UniversalPOS.Domain.Entities;

/// <summary>Organisational department an employee belongs to (sales, accounting, pharmacy, ...).</summary>
public class Department
{
    private Department() { } // EF Core

    public Department(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Department name is required.", nameof(name));
        Name = name.Trim();
        IsActive = true;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Department name is required.", nameof(name));
        Name = name.Trim();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
