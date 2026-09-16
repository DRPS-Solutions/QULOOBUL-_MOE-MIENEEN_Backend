using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Task;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;


namespace QULOOBUL__MOE_MIENEEN_Backend.Models.Project
{
    // Named ProjectTask, not Task, so it does not collide with System.Threading.Tasks.Task.
    // Table stays "Task" to match the ERD.
    [Table("Task")]
    public class ProjectTask
    {
        [Key] public int TaskId { get; set; }
        public int ProjectId { get; set; }
        public int CreatedBy { get; set; }
        public int? CompletedBy { get; set; }
        public int? ScheduleId { get; set; }
        [MaxLength(200)] public string Title { get; set; } = String.Empty;
        /// <summary>Not in the original ERD; UC-04 has users add task descriptions.</summary>
        [MaxLength(2000)] public string? Description { get; set; }
        [MaxLength(20)] public string Priority { get; set; } = String.Empty;
        [MaxLength(30)] public string Status { get; set; } = String.Empty;
        public DateTime Deadline { get; set; }

        /// <summary
        /// >0-100%. Partial progress, not just done/not done.
        /// </summary>
        public int PercentComplete { get; set; }

        public Project? Project { get; set; }
        [ForeignKey(nameof(CreatedBy))] public User? Creator { get; set; }
        [ForeignKey(nameof(CompletedBy))] public User? CompletedByUser { get; set; }
        [ForeignKey(nameof(ScheduleId))] public TaskSchedule? Schedule { get; set; }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//