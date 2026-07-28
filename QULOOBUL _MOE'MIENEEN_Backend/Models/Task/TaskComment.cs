using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.Task
{
    [Table("Task_Comment")]
    public class TaskComment
    {
        [Key] public int CommentId { get; set; }
        public int TaskId { get; set; }
        public int UserId { get; set; }
        public string Comment { get; set; } = String.Empty;
        public DateTime TimeCreated { get; set; }

        [ForeignKey(nameof(TaskId))] public ProjectTask? Task { get; set; }
        public User? User { get; set; }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//