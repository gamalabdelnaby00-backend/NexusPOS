namespace UniversalPOS.Domain.Entities;
public class Permission
{
 public int Id {get; private set;} public string Code {get; private set;}=string.Empty; public string Name {get; private set;}=string.Empty; public string Module {get; private set;}=string.Empty; public string? Description {get; private set;} public bool IsActive {get; private set;}=true;
 private readonly List<RolePermission> _rolePermissions=new(); public IReadOnlyCollection<RolePermission> RolePermissions=>_rolePermissions.AsReadOnly();
 private Permission(){} public Permission(string code,string name,string module,string? description=null){if(string.IsNullOrWhiteSpace(code))throw new ArgumentException("Permission code is required.");if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Permission name is required.");if(string.IsNullOrWhiteSpace(module))throw new ArgumentException("Module is required.");Code=code.Trim();Name=name.Trim();Module=module.Trim();Description=description?.Trim();}
 public void UpdateDetails(string name,string module,string? description){if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(module))throw new ArgumentException("Name and module are required.");Name=name.Trim();Module=module.Trim();Description=description?.Trim();} public void Deactivate()=>IsActive=false; public void Activate()=>IsActive=true;
}
