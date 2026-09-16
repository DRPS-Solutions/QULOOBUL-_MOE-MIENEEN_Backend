using Microsoft.AspNetCore.Mvc;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Collaboration;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    public class MeetingsController(MeetingMinutesService meetings) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<MeetingDto>>> List() => Ok(await meetings.ListAsync());

        [HttpPost]
        public async Task<ActionResult<int>> Create(CreateMeetingRequest req) =>
            Ok(await meetings.CreateMeetingAsync(req, UserId()));

        [HttpPost("{id:int}/minutes")]
        public async Task<ActionResult<MeetingMinutesDto>> RecordMinutes(int id, RecordMinutesRequest req) =>
            Ok(await meetings.RecordAsync(id, req, UserId()));
    }
}
