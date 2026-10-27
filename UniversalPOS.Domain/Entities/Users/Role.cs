namespace UniversalPOS.Domain.Entities;
public class Role
{
 public int Id {get; private set;} public string Name {get; private set;} = string.Empty; public string? Description {get; private set;} public bool IsSystemRole {get; private set;} public bool IsActive {get; private set;} = true;
 private readonly List<UserRole> _userRoles=new(); public IReadOnlyCollection<UserRole> UserRoles=>_userRoles.AsReadOnly();
 private readonly List<RolePermission> _rolePermissions=new(); public IReadOnlyCollection<RolePermission> RolePermissions=>_rolePermissions.AsReadOnly();
 private Role(){} public Role(string name,string? description=null,bool isSystemRole=false){if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Role name is required.");Name=name.Trim();Description=description?.Trim();IsSystemRole=isSystemRole;IsActive=true;}
 public void Rename(string name){if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Role name is required.");Name=name.Trim();} public void ChangeDescription(string? description)=>Description=description?.Trim(); public void Deactivate(){if(IsSystemRole)throw new InvalidOperationException("System roles cannot be deactivated.");IsActive=false;} public void Activate()=>IsActive=true;
}
