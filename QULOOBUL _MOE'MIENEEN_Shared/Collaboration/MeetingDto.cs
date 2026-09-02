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
}
