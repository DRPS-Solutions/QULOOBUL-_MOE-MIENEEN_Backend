using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models;

[Table("Role")]
public class Role
{
    [Key] public int RoleId { get; set; }
    [MaxLength(100)] public string RoleName { get; set; } = String.Empty;

    /// <summary>Lower number = higher authority. Tasks may be assigned down, never up.</summary>
    public int HierarchyLevel { get; set; }
}

[Table("Permission")]
public class Permission
{
    [Key] public int PermissionId { get; set; }
    [MaxLength(100)] public string PermissionName { get; set; } = String.Empty;
}

[Table("Role_Permission")]
public class RolePermission
{
    [Key] public int RolePermissionId { get; set; }
    public int RoleId { get; set; }
    public int PermissionId { get; set; }

    public Role? Role { get; set; }
    public Permission? Permission { get; set; }
}

[Table("User")]
public class User
{
    [Key] public int UserId { get; set; }
    public int RoleId { get; set; }
    [MaxLength(100)] public string FirstName { get; set; } = String.Empty;
    [MaxLength(100)] public string Surname { get; set; } = String.Empty;
    [MaxLength(256)] public string Email { get; set; } = String.Empty;
    [MaxLength(20)] public string CellNo { get; set; } = String.Empty;
    [MaxLength(256)] public string PasswordHash { get; set; } = String.Empty;
    public bool IsActive { get; set; } = true;

    public Role? Role { get; set; }
}
