using Microsoft.AspNetCore.Mvc;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Collaboration;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    public class EventsController(EventService events) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<EventDto>>> List(DateTime? from, DateTime? to) =>
            Ok(await events.ListAsync(from, to));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpGet("{id:int}")]
        public async Task<ActionResult<EventDto>> Get(int id) => Ok(await events.GetAsync(id));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost]
        public async Task<ActionResult<EventDto>> Create(CreateEventRequest req) =>
            Ok(await events.CreateAsync(req, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpGet("{id:int}/attendees")]
        public async Task<ActionResult<List<EventAttendeeDto>>> Attendees(int id) =>
            Ok(await events.AttendeesAsync(id));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost("{id:int}/attendees")]
        public async Task<IActionResult> AddAttendee(int id, AddEventAttendeeRequest req)
        {
            await events.AddAttendeeAsync(id, req.UserId, UserId());
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>RSVP for yourself; there is no route for answering on someone else's behalf.</summary>
        [HttpPost("{id:int}/response")]
        public async Task<IActionResult> Respond(int id, RespondToEventRequest req)
        {
            await events.RespondAsync(id, UserId(), req.Status);
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        [HttpDelete("{id:int}/attendees/{userId:int}")]
        public async Task<IActionResult> RemoveAttendee(int id, int userId)
        {
            await events.RemoveAttendeeAsync(id, userId, UserId());
            return NoContent();
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
