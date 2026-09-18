using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Collaboration;
using QULOOBUL__MOE_MIENEEN_Shared.Collaboration;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// Organisational events and their attendees. Enforces the "Booking / scheduling integrity" NFR:
    /// double-booking is rejected server-side, on top of the unique (EventId, UserId) constraint the
    /// database already carries.
    ///
    /// ponytail: Event has a single EventDate and no duration, so a clash is an exact-timestamp
    /// collision. Add a duration column and switch these two checks to an interval overlap if the
    /// client starts booking overlapping half-day events.
    /// </summary>
    public class EventService(AppDbContext db, AuditService audit, NotificationService notify)
    {
        public const string Invited = "Invited";
//----------------------------------------------------------------------------------------------------------------------//
        public Task<List<EventDto>> ListAsync(DateTime? from, DateTime? to) =>
            db.Events
                .Where(e => (from == null || e.EventDate >= from) && (to == null || e.EventDate < to))
                .OrderBy(e => e.EventDate)
                .Select(e => new EventDto(e.EventId, e.OrganiserId, e.Title, e.EventDate, e.Location))
                .ToListAsync();
//----------------------------------------------------------------------------------------------------------------------//
        public async Task<EventDto> GetAsync(int eventId) =>
            await db.Events.Where(e => e.EventId == eventId)
                .Select(e => new EventDto(e.EventId, e.OrganiserId, e.Title, e.EventDate, e.Location))
                .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");
//----------------------------------------------------------------------------------------------------------------------//
        public Task<List<EventAttendeeDto>> AttendeesAsync(int eventId) =>
            db.EventAttendees.Where(a => a.EventId == eventId)
                .OrderBy(a => a.User!.FirstName).ThenBy(a => a.User!.Surname)
                .Select(a => new EventAttendeeDto(
                    a.UserId, a.User!.FirstName + " " + a.User.Surname, a.Status))
                .ToListAsync();
//----------------------------------------------------------------------------------------------------------------------//
        public async Task<EventDto> CreateAsync(CreateEventRequest req, int organiserId)
        {
            await EnsureLocationFreeAsync(req.Location, req.EventDate, null);
            await EnsureUserFreeAsync(organiserId, req.EventDate, null);

            var ev = new Event
            {
                OrganiserId = organiserId,
                Title = req.Title.Trim(),
                EventDate = req.EventDate,
                Location = req.Location.Trim(),
            };
            db.Events.Add(ev);
            await db.SaveChangesAsync();                 // assigns ev.EventId
            await audit.RecordAndSaveAsync(organiserId, "event.create", nameof(Event), ev.EventId);

            return new EventDto(ev.EventId, ev.OrganiserId, ev.Title, ev.EventDate, ev.Location);
        }
//----------------------------------------------------------------------------------------------------------------------//
        public async Task AddAttendeeAsync(int eventId, int userId, int actorId)
        {
            var ev = await db.Events.FindAsync(eventId)
                ?? throw new KeyNotFoundException($"Event {eventId} not found.");
            if (!await db.Users.AnyAsync(u => u.UserId == userId && u.IsActive))
                throw new InvalidOperationException($"User {userId} does not exist or is deactivated.");
            if (await db.EventAttendees.AnyAsync(a => a.EventId == eventId && a.UserId == userId))
                throw new InvalidOperationException("That user is already on this event.");

            await EnsureUserFreeAsync(userId, ev.EventDate, eventId);

            db.EventAttendees.Add(new EventAttendee { EventId = eventId, UserId = userId, Status = Invited });
            var notification = await notify.QueueAsync(userId, "EventInvite",
                $"You have been invited to \"{ev.Title}\" on {ev.EventDate:yyyy-MM-dd HH:mm} UTC.",
                nameof(Event), eventId);
            audit.Record(actorId, "event.attendee.add", nameof(EventAttendee), eventId);

            await db.SaveChangesAsync();
            await notify.PushAsync(userId, "eventInvite", new { eventId, title = ev.Title });
            _ = notification;
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>RSVP. Only the attendee changes their own answer.</summary>
        public async Task RespondAsync(int eventId, int userId, string status)
        {
            var attendee = await db.EventAttendees
                .FirstOrDefaultAsync(a => a.EventId == eventId && a.UserId == userId)
                ?? throw new KeyNotFoundException("You are not on this event.");

            attendee.Status = status;
            audit.Record(userId, "event.attendee.respond", nameof(EventAttendee), eventId);
            await db.SaveChangesAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        public async Task RemoveAttendeeAsync(int eventId, int userId, int actorId)
        {
            var attendee = await db.EventAttendees
                .FirstOrDefaultAsync(a => a.EventId == eventId && a.UserId == userId)
                ?? throw new KeyNotFoundException("That user is not on this event.");

            db.EventAttendees.Remove(attendee);
            audit.Record(actorId, "event.attendee.remove", nameof(EventAttendee), eventId);
            await db.SaveChangesAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>One event per location per slot.</summary>
        private async Task EnsureLocationFreeAsync(string location, DateTime when, int? ignoreEventId)
        {
            if (string.IsNullOrWhiteSpace(location)) return;   // an event with no venue cannot clash on one
            var trimmed = location.Trim();
            if (await db.Events.AnyAsync(e => e.EventId != ignoreEventId
                    && e.EventDate == when && e.Location == trimmed))
                throw new InvalidOperationException($"\"{trimmed}\" is already booked at that time.");
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Nobody is in two places at once — checked across events and meetings alike.</summary>
        private async Task EnsureUserFreeAsync(int userId, DateTime when, int? ignoreEventId)
        {
            var clashingEvent = await db.Events.AnyAsync(e => e.EventId != ignoreEventId && e.EventDate == when
                    && (e.OrganiserId == userId
                        || db.EventAttendees.Any(a => a.EventId == e.EventId && a.UserId == userId)))
                || await db.Meetings.AnyAsync(m => m.MeetingDate == when
                    && (m.OrganiserId == userId
                        || db.MeetingAttendees.Any(a => a.MeetingId == m.MeetingId && a.UserId == userId)));

            if (clashingEvent)
                throw new InvalidOperationException("That person is already booked at that time.");
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
