using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;


namespace QULOOBUL__MOE_MIENEEN_Backend.Models.Task
{
    [Table("Task_Status_History")]
    public class TaskStatusHistory
    {
        [Key] public int HistoryId { get; set; }
        public int TaskId { get; set; }
        [MaxLength(30)] public string NewStatus { get; set; } = String.Empty;
        public int ChangedBy { get; set; }
        public DateTime TimeCreated { get; set; }

        [ForeignKey(nameof(TaskId))] public ProjectTask? Task { get; set; }
        [ForeignKey(nameof(ChangedBy))] public User? ChangedByUser { get; set; }
    }

}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//