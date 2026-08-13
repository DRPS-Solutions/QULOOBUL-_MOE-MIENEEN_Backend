using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.Request
{
    [Table("Request_Status_History")]
    public class RequestStatusHistory
    {
        [Key] public int HistoryId { get; set; }
        public int RequestId { get; set; }
        [MaxLength(30)] public string NewStatus { get; set; } = String.Empty;
        public int ChangedBy { get; set; }
        public DateTime TimeCreated { get; set; }

        [ForeignKey(nameof(RequestId))] public ResourceRequest? Request { get; set; }
        [ForeignKey(nameof(ChangedBy))] public User? ChangedByUser { get; set; }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//