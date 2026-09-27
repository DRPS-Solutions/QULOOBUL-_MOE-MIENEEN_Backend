using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace QULOOBUL__MOE_MIENEEN_Shared.Directory
{
    public record UserSummaryDto(int UserId, string FullName, string Email, string Role, bool IsActive, string Status = "Active");
//----------------------------------------------------------------------------------------------------------------------//
    public record UserDto(
        int UserId, string FirstName, string Surname, string Email, string CellNo,
        string Role, bool IsActive, string Status = "Active");
//----------------------------------------------------------------------------------------------------------------------//
    public record CreateUserRequest
    {
        [Required, MaxLength(100)] 
        public string FirstName { get; init; } = string.Empty;

        [Required, MaxLength(100)] 
        public string Surname { get; init; } = string.Empty;

        [Required, EmailAddress, MaxLength(256)] 
        public string Email { get; init; } = string.Empty;

        [Required, Phone, MaxLength(20)]
        public string CellNo { get; init; } = string.Empty;

        [Required, Range(1, int.MaxValue)]
        public int RoleId { get; init; }
    }
//----------------------------------------------------------------------------------------------------------------------//
    public record UpdateUserRequest
    {
        [MaxLength(100)] 
        public string? FirstName { get; init; }

        [MaxLength(100)] 
        public string? Surname { get; init; }

        [Phone, MaxLength(20)] 
        public string? CellNo { get; init; }

        [Range(1, int.MaxValue)]
        public int? RoleId { get; init; }
    }
//----------------------------------------------------------------------------------------------------------------------//
    public record RoleDto(int RoleId, string RoleName, int HierarchyLevel);

    public record ProjectDto(int ProjectId, int OwnerId, string ProjectName, string Status, string? Description);
    public record ProjectSummaryDto(int ProjectId, string ProjectName, string Status, int MemberCount, int OpenTaskCount);
//----------------------------------------------------------------------------------------------------------------------//
    public record CreateProjectRequest
    {
        [Required, MaxLength(200)]
        public string ProjectName { get; init; } = string.Empty;

        [MaxLength(30)]
        public string Status { get; init; } = "Active";

        [MaxLength(2000)]
        public string? Description { get; init; }

        /// <summary>The project's coordinator; defaults to the creator.</summary>
        [Range(1, int.MaxValue)]
        public int? OwnerId { get; init; }
    }
//----------------------------------------------------------------------------------------------------------------------//
    public record UpdateProjectRequest
    {
        [MaxLength(200)]
        public string? ProjectName { get; init; }

        [MaxLength(30)]
        public string? Status { get; init; }
        [MaxLength(2000)]
        public string? Description { get; init; }
        [Range(1, int.MaxValue)]
        public int? OwnerId { get; init; }
    }
//----------------------------------------------------------------------------------------------------------------------//
    public record ProjectMemberDto(int UserId, string FullName, DateTime DateAdded);
    public record AddProjectMemberRequest { [Required, Range(1, int.MaxValue)] public int UserId { get; init; } }
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>UC-06 admin dashboard row.</summary>
    public record StaffOverviewRow(
        int UserId, string FullName, string Role, int ActiveTasks, int OverdueTasks, int OpenRequests);

}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//