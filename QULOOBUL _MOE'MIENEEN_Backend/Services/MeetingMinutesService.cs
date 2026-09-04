using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Collaboration;
using QULOOBUL__MOE_MIENEEN_Shared.Collaboration;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>UC-03: create meetings, record/approve minutes. Author + date are set server-side.</summary>
    public class MeetingMinutesService(AppDbContext db, AuditService audit)
    {
        public async Task<int> CreateMeetingAsync(CreateMeetingRequest req, int organiserId)
        {
            var meeting = new Meeting { OrganiserId = organiserId, Title = req.Title, MeetingDate = req.MeetingDate };
            db.Meetings.Add(meeting);
            await db.SaveChangesAsync();

            foreach (var uid in req.AttendeeIds.Distinct())
                db.MeetingAttendees.Add(new MeetingAttendee { MeetingId = meeting.MeetingId, UserId = uid });

            audit.Record(organiserId, "meeting.create", nameof(Meeting), meeting.MeetingId);
            await db.SaveChangesAsync();
            return meeting.MeetingId;
        }

        public async Task<MeetingMinutesDto> RecordAsync(int meetingId, RecordMinutesRequest req, int recordedBy)
        {
            if (!await db.Meetings.AnyAsync(m => m.MeetingId == meetingId))
                throw new KeyNotFoundException($"Meeting {meetingId} not found.");

            var minutes = new MeetingMinutes
            {
                MeetingId = meetingId,
                RecordedBy = recordedBy,
                TimeRecorded = DateTime.UtcNow,
                MinutesText = req.MinutesText,
                IsAiDraft = req.IsAiDraft,
            };
            db.MeetingMinutes.Add(minutes);
            audit.Record(recordedBy, "meeting.minutes.record", nameof(MeetingMinutes), meetingId);
            await db.SaveChangesAsync();
            return Map(minutes);
        }

        public async Task<MeetingMinutesDto> ApproveAsync(int minutesId, int approverId)
        {
            var minutes = await db.MeetingMinutes.FindAsync(minutesId)
                ?? throw new KeyNotFoundException($"Minutes {minutesId} not found.");
            minutes.ApprovedBy = approverId;
            minutes.IsAiDraft = false;
            audit.Record(approverId, "meeting.minutes.approve", nameof(MeetingMinutes), minutesId);
            await db.SaveChangesAsync();
            return Map(minutes);
        }

        public Task<List<MeetingMinutesDto>> SearchAsync(string? keyword, DateTime? from, DateTime? to) =>
            db.MeetingMinutes
                .Where(m => (keyword == null || m.MinutesText.Contains(keyword))
                         && (from == null || m.TimeRecorded >= from)
                         && (to == null || m.TimeRecorded <= to))
                .Select(m => Map(m)).ToListAsync();

        private static MeetingMinutesDto Map(MeetingMinutes m) => new(
            m.MinutesId, m.MeetingId, m.RecordedBy, m.TimeRecorded, m.MinutesText, m.IsAiDraft, m.ApprovedBy);
    }
}