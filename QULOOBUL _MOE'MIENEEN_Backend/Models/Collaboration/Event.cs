using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.Collaboration
{
    [Table("Event")]
    public class Event
    {
        [Key] public int EventId { get; set; }
        public int OrganiserId { get; set; }
        [MaxLength(200)] public string Title { get; set; } = String.Empty;
        public DateTime EventDate { get; set; }
        [MaxLength(200)] public string Location { get; set; } = String.Empty;

        [ForeignKey(nameof(OrganiserId))] public User? Organiser { get; set; }
    }
}
