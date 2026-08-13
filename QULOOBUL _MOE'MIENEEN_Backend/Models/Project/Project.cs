using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;


namespace QULOOBUL__MOE_MIENEEN_Backend.Models.Project
{
    [Table("Project")]
    public class Project
    {
        [Key] public int ProjectId { get; set; }
        public int OwnerId { get; set; }
        [MaxLength(200)] public string ProjectName { get; set; } = String.Empty;
        [MaxLength(30)] public string Status { get; set; } = String.Empty;
        public string? Description { get; set; }

        [ForeignKey(nameof(OwnerId))] public User? Owner { get; set; }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//