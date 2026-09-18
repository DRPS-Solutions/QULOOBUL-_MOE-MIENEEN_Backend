using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;


namespace QULOOBUL__MOE_MIENEEN_Backend.Models.Request
{
    [Table("Resource_Request")]
    public class ResourceRequest
    {
        [Key] public int RequestId { get; set; }
        public int RequestedBy { get; set; }
        public int CategoryId { get; set; }
        public int? ReviewedBy { get; set; }
        [MaxLength(30)] public string Status { get; set; } = String.Empty;
        [MaxLength(20)] public string Priority { get; set; } = String.Empty;
        public DateTime DateSubmitted { get; set; }
        /// <summary>Not in the original ERD; UC-05's request form describes what is needed.</summary>
        [MaxLength(1000)] public string? Detail { get; set; }

        [ForeignKey(nameof(RequestedBy))] public User? Requester { get; set; }
        [ForeignKey(nameof(CategoryId))] public RequestCategory? Category { get; set; }
        [ForeignKey(nameof(ReviewedBy))] public User? Reviewer { get; set; }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//