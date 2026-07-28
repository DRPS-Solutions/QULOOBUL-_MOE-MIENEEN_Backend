using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models;

[Table("Meeting")]
public class Meeting
{
    [Key] public int MeetingId { get; set; }
    public int OrganiserId { get; set; }
    [MaxLength(200)] public string Title { get; set; } = "";
    public DateTime MeetingDate { get; set; }

    [ForeignKey(nameof(OrganiserId))] public User? Organiser { get; set; }
}

[Table("Meeting_Attendee")]
public class MeetingAttendee
{
    [Key] public int AttendeeId { get; set; }
    public int MeetingId { get; set; }
    public int UserId { get; set; }

    public Meeting? Meeting { get; set; }
    public User? User { get; set; }
}

[Table("Meeting_Minutes")]
public class MeetingMinutes
{
    [Key] public int MinutesId { get; set; }
    public int MeetingId { get; set; }
    public int RecordedBy { get; set; }
    public DateTime TimeRecorded { get; set; }
    public string MinutesText { get; set; } = "";

    /// <summary>AI-generated minutes are a draft until a person approves them.</summary>
    public bool IsAiDraft { get; set; }
    public int? ApprovedBy { get; set; }

    public Meeting? Meeting { get; set; }
    [ForeignKey(nameof(RecordedBy))] public User? Recorder { get; set; }
    [ForeignKey(nameof(ApprovedBy))] public User? Approver { get; set; }
}

[Table("Event")]
public class Event
{
    [Key] public int EventId { get; set; }
    public int OrganiserId { get; set; }
    [MaxLength(200)] public string Title { get; set; } = "";
    public DateTime EventDate { get; set; }
    [MaxLength(200)] public string Location { get; set; } = "";

    [ForeignKey(nameof(OrganiserId))] public User? Organiser { get; set; }
}

[Table("Event_Attendee")]
public class EventAttendee
{
    [Key] public int AttendeeId { get; set; }
    public int EventId { get; set; }
    public int UserId { get; set; }
    [MaxLength(30)] public string Status { get; set; } = "";

    public Event? Event { get; set; }
    public User? User { get; set; }
}
