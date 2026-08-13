using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;


namespace QULOOBUL__MOE_MIENEEN_Backend.Models.Collaboration
{
    [Table("Meeting_Attendee")]
    public class MeetingAttendee
    {
        [Key] public int AttendeeId { get; set; }
        public int MeetingId { get; set; }
        public int UserId { get; set; }

        public Meeting? Meeting { get; set; }
        public User? User { get; set; }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//