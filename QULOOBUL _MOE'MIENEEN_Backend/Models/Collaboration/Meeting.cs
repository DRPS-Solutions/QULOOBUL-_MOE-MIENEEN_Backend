using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.Collaboration
{
    [Table("Meeting")]
    public class Meeting
    {
        [Key] public int MeetingId { get; set; }
        public int OrganiserId { get; set; }
        [MaxLength(200)] public string Title { get; set; } = String.Empty;
        public DateTime MeetingDate { get; set; }

        [ForeignKey(nameof(OrganiserId))] public User? Organiser { get; set; }
    }
}
