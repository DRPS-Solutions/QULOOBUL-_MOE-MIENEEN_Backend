using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;


namespace QULOOBUL__MOE_MIENEEN_Backend.Models.Task
{
    /// <summary>Recurring weekly task template — Saturday's tasks differ from Friday's.</summary>
    [Table("Task_Schedule")]
    public class TaskSchedule
    {
        [Key] public int ScheduleId { get; set; }
        public int CreatedBy { get; set; }
        public int AssignedTo { get; set; }
        [MaxLength(200)] public string Title { get; set; } = String.Empty;
        [MaxLength(10)] public string DayOfWeek { get; set; } = String.Empty;
        public bool IsActive { get; set; } = true;

        [ForeignKey(nameof(CreatedBy))] public User? Creator { get; set; }
        [ForeignKey(nameof(AssignedTo))] public User? Assignee { get; set; }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//