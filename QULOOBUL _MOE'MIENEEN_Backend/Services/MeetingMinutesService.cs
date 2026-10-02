using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Collaboration;
using QULOOBUL__MOE_MIENEEN_Shared.Collaboration;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>UC-03: create meetings, record/edit/approve minutes. Author + date are set server-side.</summary>
    public class MeetingMinutesService(AppDbContext db, AuditService audit, AccessGuard guard)
    {
        /// <summary>
        /// The caller's meetings, newest first, with the latest minutes (edits append a new record).
        /// Organised-by or invited-to only, per the anti-IDOR NFR; an admin sees all of them.
        /// </summary>
        public async Task<List<MeetingDto>> ListAsync(int userId) =>
            await VisibleMeetings(userId, await guard.IsAdminAsync(userId))
                .OrderByDescending(m => m.MeetingDate).Select(m => new MeetingDto(
                m.MeetingId, m.OrganiserId, m.Title, m.MeetingDate,
                db.MeetingAttendees.Where(a => a.MeetingId == m.MeetingId).Select(a => a.UserId).ToList(),
                db.MeetingMinutes.Where(x => x.MeetingId == m.MeetingId).OrderByDescending(x => x.MinutesId)
                    .Select(x => new MeetingMinutesDto(
                        x.MinutesId, x.MeetingId, x.RecordedBy, x.TimeRecorded, x.MinutesText, x.IsAiDraft, x.ApprovedBy))
                    .FirstOrDefault()))
                .ToListAsync();
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Booking integrity NFR: the organiser and every invitee must be free at that time.</summary>
        public async Task<int> CreateMeetingAsync(CreateMeetingRequest req, int organiserId)
        {
            var attendees = req.AttendeeIds.Distinct().ToList();
            foreach (var uid in attendees.Append(organiserId).Distinct())
                await EventService.EnsureUserFreeAsync(db, uid, req.MeetingDate);

            var meeting = new Meeting { OrganiserId = organiserId, Title = req.Title, MeetingDate = req.MeetingDate };
            db.Meetings.Add(meeting);
            await db.SaveChangesAsync();

            foreach (var uid in attendees)
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
        /// <summary>
        /// UC-03 alternate flow: "edit or append to previously saved minutes if permitted by their
        /// access level". Permitted = whoever recorded them, the meeting's organiser, or an admin.
        /// The edit is saved as a new version so the earlier text stays on record; the new version
        /// needs approving again, and an AI draft stays a draft until someone approves it.
        /// </summary>
        public async Task<MeetingMinutesDto> EditAsync(int minutesId, EditMinutesRequest req, int editorId)
        {
            var current = await db.MeetingMinutes.Include(m => m.Meeting)
                .FirstOrDefaultAsync(m => m.MinutesId == minutesId)
                ?? throw new KeyNotFoundException($"Minutes {minutesId} not found.");
            await guard.EnsureMeetingAsync(editorId, current.MeetingId);
            if (current.RecordedBy != editorId && current.Meeting!.OrganiserId != editorId
                && !await guard.IsAdminAsync(editorId))
                throw new UnauthorizedAccessException(
                    "Only whoever recorded these minutes, the organiser or an admin may edit them.");

            // Editing an old version would silently drop whatever the newer one added.
            if (await db.MeetingMinutes.AnyAsync(m => m.MeetingId == current.MeetingId && m.MinutesId > minutesId))
                throw new InvalidOperationException("These minutes have been edited since; edit the latest version.");

            var edited = new MeetingMinutes
            {
                MeetingId = current.MeetingId,
                RecordedBy = editorId,
                TimeRecorded = DateTime.UtcNow,
                MinutesText = req.Append ? current.MinutesText + Environment.NewLine + req.MinutesText : req.MinutesText,
                IsAiDraft = current.IsAiDraft,
            };
            db.MeetingMinutes.Add(edited);
            audit.Record(editorId, req.Append ? "meeting.minutes.append" : "meeting.minutes.edit",
                nameof(MeetingMinutes), minutesId);
            await db.SaveChangesAsync();
            return Map(edited);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Signs minutes off and clears the AI-draft flag. Organiser or admin only.</summary>
        public async Task<MeetingMinutesDto> ApproveAsync(int minutesId, int approverId)
        {
            var minutes = await db.MeetingMinutes.Include(m => m.Meeting)
                .FirstOrDefaultAsync(m => m.MinutesId == minutesId)
                ?? throw new KeyNotFoundException($"Minutes {minutesId} not found.");
            await guard.EnsureMeetingAsync(approverId, minutes.MeetingId);
            if (minutes.Meeting!.OrganiserId != approverId && !await guard.IsAdminAsync(approverId))
                throw new UnauthorizedAccessException("Only the meeting's organiser or an admin may approve minutes.");

            minutes.ApprovedBy = approverId;
            minutes.IsAiDraft = false;
            audit.Record(approverId, "meeting.minutes.approve", nameof(MeetingMinutes), minutesId);
            await db.SaveChangesAsync();
            return Map(minutes);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>UC-03 "searchable by date or keyword", over the meetings the caller may see.</summary>
        public async Task<List<MeetingMinutesDto>> SearchAsync(string? keyword, DateTime? from, DateTime? to, int userId)
        {
            var meetingIds = VisibleMeetings(userId, await guard.IsAdminAsync(userId)).Select(m => m.MeetingId);
            keyword = keyword?.Trim().ToLower(); // case-insensitive regardless of the database collation
            return await db.MeetingMinutes
                .Where(m => meetingIds.Contains(m.MeetingId)
                         && !db.MeetingMinutes.Any(x => x.MeetingId == m.MeetingId && x.MinutesId > m.MinutesId) // latest version only
                         && (keyword == null || m.MinutesText.ToLower().Contains(keyword) || m.Meeting!.Title.ToLower().Contains(keyword))
                         && (from == null || m.Meeting!.MeetingDate >= from)
                         && (to == null || m.Meeting!.MeetingDate < to))
                .OrderByDescending(m => m.TimeRecorded)
                .Select(m => new MeetingMinutesDto(
                    m.MinutesId, m.MeetingId, m.RecordedBy, m.TimeRecorded, m.MinutesText, m.IsAiDraft, m.ApprovedBy))
                .ToListAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        public IQueryable<Meeting> VisibleMeetings(int userId, bool isAdmin) =>
            isAdmin ? db.Meetings : db.Meetings.Where(m => m.OrganiserId == userId
                || db.MeetingAttendees.Any(a => a.MeetingId == m.MeetingId && a.UserId == userId));
//----------------------------------------------------------------------------------------------------------------------//
        private static MeetingMinutesDto Map(MeetingMinutes m) => new(
            m.MinutesId, m.MeetingId, m.RecordedBy, m.TimeRecorded, m.MinutesText, m.IsAiDraft, m.ApprovedBy);
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
