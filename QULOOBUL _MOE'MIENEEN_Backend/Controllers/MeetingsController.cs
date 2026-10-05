using Microsoft.AspNetCore.Mvc;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Collaboration;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    public class MeetingsController(MeetingMinutesService meetings) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<MeetingDto>>> List() => Ok(await meetings.ListAsync(UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost]
        public async Task<ActionResult<int>> Create(CreateMeetingRequest req) =>
            Ok(await meetings.CreateMeetingAsync(req, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost("{id:int}/minutes")]
        public async Task<ActionResult<MeetingMinutesDto>> RecordMinutes(int id, RecordMinutesRequest req) =>
            Ok(await meetings.RecordAsync(id, req, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Edit or append to saved minutes; returns the new version.</summary>
        [HttpPut("minutes/{minutesId:int}")]
        public async Task<ActionResult<MeetingMinutesDto>> EditMinutes(int minutesId, EditMinutesRequest req) =>
            Ok(await meetings.EditAsync(minutesId, req, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost("minutes/{minutesId:int}/approve")]
        public async Task<ActionResult<MeetingMinutesDto>> ApproveMinutes(int minutesId) =>
            Ok(await meetings.ApproveAsync(minutesId, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpGet("minutes")]
        public async Task<ActionResult<List<MeetingMinutesDto>>> SearchMinutes(string? q, DateTime? from, DateTime? to) =>
            Ok(await meetings.SearchAsync(q, from, to, UserId()));
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
