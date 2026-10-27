using System.Security.Cryptography;
namespace UniversalPOS.Domain.Entities;

public class User
{
    public int Id { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; private set; }
    /// <summary>Optional link to the employee record that owns this login account.</summary>
    public int? EmployeeId { get; private set; }
    private readonly List<UserRole> _userRoles = new();
    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();
    private readonly List<UserPermissionOverride> _permissionOverrides = new();
    public IReadOnlyCollection<UserPermissionOverride> PermissionOverrides => _permissionOverrides.AsReadOnly();
    private User() { }
    public User(string userName, string displayName, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(userName)) throw new ArgumentException("Username is required.", nameof(userName));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        UserName = userName.Trim(); DisplayName = displayName.Trim(); PasswordHash = passwordHash; IsActive = true;
    }
    public void ChangeDisplayName(string name) { if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Display name is required."); DisplayName = name.Trim(); }
    public void ChangePasswordHash(string hash) { if (string.IsNullOrWhiteSpace(hash)) throw new ArgumentException("Password hash is required."); PasswordHash = hash; }
    public void LinkEmployee(int employeeId) { if (employeeId <= 0) throw new ArgumentOutOfRangeException(nameof(employeeId)); EmployeeId = employeeId; }
    public void RecordSuccessfulLogin() => LastLoginAtUtc = DateTime.UtcNow;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
