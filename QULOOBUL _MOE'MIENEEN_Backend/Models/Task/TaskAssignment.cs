using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.Task
{
    [Table("Task_Assignment")]
    public class TaskAssignment
    {
        [Key] public int AssignmentId { get; set; }
        public int TaskId { get; set; }
        public int UserId { get; set; }

        [ForeignKey(nameof(TaskId))] public ProjectTask? Task { get; set; }
        public User? User { get; set; }
    }
}
