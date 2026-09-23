using Microsoft.AspNetCore.Mvc;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Tasks;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    /// <summary>Recurring weekly tasks. Pause with PATCH { "isActive": false }; there is no delete.</summary>
    public class TaskSchedulesController(TaskScheduleService schedules) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<TaskScheduleDto>>> List() => Ok(await schedules.ListAsync(UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost]
        public async Task<ActionResult<TaskScheduleDto>> Create(CreateTaskScheduleRequest req) =>
            Ok(await schedules.CreateAsync(req, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPatch("{id:int}")]
        public async Task<ActionResult<TaskScheduleDto>> Update(int id, UpdateTaskScheduleRequest req) =>
            Ok(await schedules.UpdateAsync(id, req, UserId()));
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
