using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;


namespace QULOOBUL__MOE_MIENEEN_Backend.Models.Project
{
    /// <summary>Project-scoped visibility: a member allocated to a project sees only that project.</summary>
    [Table("Project_Member")]
    public class ProjectMember
    {
        [Key] public int MembershipId { get; set; }
        public int ProjectId { get; set; }
        public int UserId { get; set; }
        public DateTime DateAdded { get; set; }

        public Project? Project { get; set; }
        public User? User { get; set; }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//