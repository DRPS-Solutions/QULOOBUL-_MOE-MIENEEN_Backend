using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;


namespace QULOOBUL__MOE_MIENEEN_Backend.Models.Collaboration
{
    [Table("Event_Attendee")]
    public class EventAttendee
    {
        [Key] public int AttendeeId { get; set; }
        public int EventId { get; set; }
        public int UserId { get; set; }
        [MaxLength(30)] public string Status { get; set; } = String.Empty;

        public Event? Event { get; set; }
        public User? User { get; set; }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//