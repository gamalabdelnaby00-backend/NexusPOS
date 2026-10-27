
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UniversalPOS.Application.Security;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Infrastructure.Data.Seeding;

namespace UniversalPOS.Infrastructure.Data;

public class InitialDataSeeder
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly PermissionSeeder _permissionSeeder;

    public InitialDataSeeder(
        AppDbContext db,
        IConfiguration configuration,
        PermissionSeeder permissionSeeder)
    {
        _db = db;
        _configuration = configuration;
        _permissionSeeder = permissionSeeder;
    }

    public async Task SeedAsync()
    {
        string username =
            _configuration["InitialAdmin:UserName"]?.Trim() ?? "";

        string displayName =
            _configuration["InitialAdmin:DisplayName"]?.Trim() ?? "";

        string password =
            _configuration["InitialAdmin:Password"] ?? "";

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(displayName) ||
            password.Length < 8)
        {
            throw new InvalidOperationException(
                "Initial admin settings are missing or invalid. " +
                "Configure a username, display name, and password " +
                "of at least 8 characters in User Secrets.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        await _permissionSeeder.SeedAsync();

        var role = await _db.Roles
            .SingleOrDefaultAsync(r => r.Name == "Admin");

        if (role is null)
        {
            role = new Role(
                "Admin",
                "System administrator",
                isSystemRole: true);

            _db.Roles.Add(role);
            await _db.SaveChangesAsync();
        }

        var user = await _db.Users
            .SingleOrDefaultAsync(u => u.UserName == username);

        if (user is null)
        {
            user = new User(
                username,
                displayName,
                PasswordHashing.Hash(password));

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        bool hasAdminRole = await _db.UserRoles.AnyAsync(
            ur => ur.UserId == user.Id &&
                  ur.RoleId == role.Id);

        if (!hasAdminRole)
        {
            _db.UserRoles.Add(
                new UserRole(user.Id, role.Id));

            await _db.SaveChangesAsync();
        }

        // Settings row: tax disabled until an Admin or Manager enables it.
        if (!await _db.SystemSettings.AnyAsync())
        {
            _db.SystemSettings.Add(UniversalPOS.Domain.Entities.Settings.SystemSetting.CreateDefault());
            await _db.SaveChangesAsync();
        }

        // One default branch today. More can be added later without schema changes.
        if (!await _db.Branches.AnyAsync(b => b.Code == "MAIN"))
        {
            _db.Branches.Add(new UniversalPOS.Domain.Entities.Branches.Branch(
                "MAIN", "Main Branch"));
            await _db.SaveChangesAsync();
        }

        var terminal = await _db.PosTerminals
            .SingleOrDefaultAsync(t => t.Code == "POS-01");

        if (terminal is null)
        {
            _db.PosTerminals.Add(
                new PosTerminal("POS-01", "Main Cashier"));

            await _db.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }
}
