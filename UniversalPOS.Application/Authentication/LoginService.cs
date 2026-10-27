using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Security;

namespace UniversalPOS.Application.Authentication;

public sealed class LoginService
{
    private readonly IUserRepository _userRepository;
    private readonly CurrentUserSession _session;

    public LoginService(IUserRepository userRepository,CurrentUserSession session)
    {
        _userRepository = userRepository;
        _session = session;
    }

    public async Task<bool> ExecuteAsync(string userName,string password)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        var user = await _userRepository.GetByUserNameAsync(userName.Trim());

        if (user is null || !user.IsActive)
            return false;

        if (!PasswordHashing.Verify(password, user.PasswordHash))
        {
            return false;
        }

        var activeRoles = user.UserRoles.Where(ur => ur.Role is not null && ur.Role.IsActive)
            .Select(ur => ur.Role!).ToArray();

        var roleNames = activeRoles.Select(role => role.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var activePermissions = activeRoles.SelectMany(role => role.RolePermissions).Select(rp => rp.Permission)
            .Where(permission => permission is not null && permission.IsActive).Select(permission => permission!)
            .GroupBy(permission => permission.Code,StringComparer.OrdinalIgnoreCase).Select(group => group.First())
            .ToDictionary(permission => permission.Code,permission => true,StringComparer.OrdinalIgnoreCase);

        foreach (var permissionOverride in user.PermissionOverrides)
        {
            var permission = permissionOverride.Permission;

            if (permission is null || !permission.IsActive)
                continue;

            activePermissions[permission.Code] = permissionOverride.IsGranted;
        }

        var permissionCodes = activePermissions.Where(pair => pair.Value).Select(pair => pair.Key).ToArray();

        user.RecordSuccessfulLogin();
        await _userRepository.UpdateAsync(user);

        _session.SignIn(user.Id,user.UserName,user.DisplayName,roleNames,permissionCodes);

        return true;
    }
}
