using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace QULOOBUL__MOE_MIENEEN_Shared.Collaboration
{
    public record MeetingDto(
    int MeetingId, int OrganiserId, string Title, DateTime MeetingDate,
    IReadOnlyList<int> AttendeeIds, MeetingMinutesDto? Minutes);

    public record CreateMeetingRequest
    {
        [Required, MaxLength(200)] 
        public string Title { get; init; } = string.Empty;
        [Required] 
        public DateTime MeetingDate { get; init; }
        public IReadOnlyList<int> AttendeeIds { get; init; } = [];
    }

    public record MeetingMinutesDto(
        int MinutesId, int MeetingId, int RecordedBy, DateTime TimeRecorded,
        string MinutesText, bool IsAiDraft, int? ApprovedBy);

    public record RecordMinutesRequest
    {
        [Required, MinLength(1)] 
        public string MinutesText { get; init; } = string.Empty;
        public bool IsAiDraft { get; init; }
    }

    /// <summary>UC-03 alternate flow: edit (replace) or append to saved minutes. Saved as a new version.</summary>
    public record EditMinutesRequest
    {
        [Required, MinLength(1)]
        public string MinutesText { get; init; } = string.Empty;
        /// <summary>True adds the text below the current minutes; false replaces them.</summary>
        public bool Append { get; init; }
    }

    public record EventDto(int EventId, int OrganiserId, string Title, DateTime EventDate, string Location);

    public record CreateEventRequest
    {
        [Required, MaxLength(200)]
        public string Title { get; init; } = string.Empty;
        [Required]
        public DateTime EventDate { get; init; }
        [MaxLength(200)]
        public string Location { get; init; } = string.Empty;
    }

    public record EventAttendeeDto(int UserId, string FullName, string Status);

    public record AddEventAttendeeRequest { [Required] public int UserId { get; init; } }

    /// <summary>RSVP against an event the caller is already on.</summary>
    public record RespondToEventRequest
    {
        [Required, RegularExpression("Invited|Accepted|Declined")]
        public string Status { get; init; } = "Accepted";
    }
}
