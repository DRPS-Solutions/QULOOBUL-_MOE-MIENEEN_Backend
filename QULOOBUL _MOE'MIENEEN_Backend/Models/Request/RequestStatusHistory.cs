using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;


namespace QULOOBUL__MOE_MIENEEN_Backend.Models.Request
{
    [Table("Request_Status_History")]
    public class RequestStatusHistory
    {
        [Key] public int HistoryId { get; set; }
        public int RequestId { get; set; }
        [MaxLength(30)] public string NewStatus { get; set; } = String.Empty;
        /// <summary>The reviewer's reason on an approve/decline, so the requester can read it on the request.</summary>
        [MaxLength(500)] public string? Note { get; set; }
        public int ChangedBy { get; set; }
        public DateTime TimeCreated { get; set; }

        [ForeignKey(nameof(RequestId))] public ResourceRequest? Request { get; set; }
        [ForeignKey(nameof(ChangedBy))] public User? ChangedByUser { get; set; }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//