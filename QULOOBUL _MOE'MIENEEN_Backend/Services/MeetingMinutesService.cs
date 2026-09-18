using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Collaboration;
using QULOOBUL__MOE_MIENEEN_Shared.Collaboration;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>UC-03: create meetings, record/approve minutes. Author + date are set server-side.</summary>
    public class MeetingMinutesService(AppDbContext db, AuditService audit, AccessGuard guard)
    {
        /// <summary>
        /// The caller's meetings, newest first, with the latest minutes (edits append a new record).
        /// Organised-by or invited-to only, per the anti-IDOR NFR; an admin sees all of them.
        /// </summary>
        public async Task<List<MeetingDto>> ListAsync(int userId)
        {
            var q = db.Meetings.AsQueryable();
            if (!await guard.IsAdminAsync(userId))
                q = q.Where(m => m.OrganiserId == userId
                              || db.MeetingAttendees.Any(a => a.MeetingId == m.MeetingId && a.UserId == userId));

            return await q.OrderByDescending(m => m.MeetingDate).Select(m => new MeetingDto(
                m.MeetingId, m.OrganiserId, m.Title, m.MeetingDate,
                db.MeetingAttendees.Where(a => a.MeetingId == m.MeetingId).Select(a => a.UserId).ToList(),
                db.MeetingMinutes.Where(x => x.MeetingId == m.MeetingId).OrderByDescending(x => x.MinutesId)
                    .Select(x => new MeetingMinutesDto(
                        x.MinutesId, x.MeetingId, x.RecordedBy, x.TimeRecorded, x.MinutesText, x.IsAiDraft, x.ApprovedBy))
                    .FirstOrDefault()))
                .ToListAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
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
//----------------------------------------------------------------------------------------------------------------------//
        public async Task<MeetingMinutesDto> RecordAsync(int meetingId, RecordMinutesRequest req, int recordedBy)
        {
            await guard.EnsureMeetingAsync(recordedBy, meetingId);

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
//----------------------------------------------------------------------------------------------------------------------//
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
//----------------------------------------------------------------------------------------------------------------------//
        public Task<List<MeetingMinutesDto>> SearchAsync(string? keyword, DateTime? from, DateTime? to) =>
            db.MeetingMinutes
                .Where(m => (keyword == null || m.MinutesText.Contains(keyword))
                         && (from == null || m.TimeRecorded >= from)
                         && (to == null || m.TimeRecorded <= to))
                .Select(m => Map(m)).ToListAsync();
//----------------------------------------------------------------------------------------------------------------------//
        private static MeetingMinutesDto Map(MeetingMinutes m) => new(
            m.MinutesId, m.MeetingId, m.RecordedBy, m.TimeRecorded, m.MinutesText, m.IsAiDraft, m.ApprovedBy);
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//