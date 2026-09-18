using Microsoft.AspNetCore.Mvc;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Tasks;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    public class TasksController(TaskService tasks, TaskAssignmentService assignments) : ApiControllerBase
    {
        [HttpGet("~/api/projects/{projectId:int}/board")]
        public async Task<ActionResult<IReadOnlyList<TaskCardDto>>> Board(int projectId) =>
            Ok(await tasks.BoardAsync(projectId, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TaskCardDto>>> List(DateTime? from, DateTime? to, int? assigneeId) =>
            Ok(await tasks.ListAsync(from, to, assigneeId, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpGet("{id:int}")]
        public async Task<ActionResult<TaskDetailDto>> Get(int id) => Ok(await tasks.DetailAsync(id, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost]
        public async Task<ActionResult<TaskCardDto>> Create(CreateTaskRequest req) =>
            Ok(await tasks.CreateAsync(req, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPatch("{id:int}")]
        public async Task<ActionResult<TaskCardDto>> Update(int id, UpdateTaskRequest req) =>
            Ok(await tasks.UpdateAsync(id, req, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost("{id:int}/move")]
        public async Task<IActionResult> Move(int id, MoveTaskRequest req)
        {
            await tasks.MoveAsync(id, req.NewStatus, UserId());
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost("{id:int}/assignments")]
        public async Task<IActionResult> Assign(int id, AssignTaskRequest req)
        {
            await assignments.AssignAsync(id, req.AssigneeId, UserId());
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        [HttpDelete("{id:int}/assignments/{userId:int}")]
        public async Task<IActionResult> Unassign(int id, int userId)
        {
            await assignments.UnassignAsync(id, userId, UserId());
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost("{id:int}/comments")]
        public async Task<ActionResult<TaskCommentDto>> Comment(int id, AddTaskCommentRequest req) =>
            Ok(await tasks.AddCommentAsync(id, req.Comment, UserId()));
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//