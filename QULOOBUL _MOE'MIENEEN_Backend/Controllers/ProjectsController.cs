using Microsoft.AspNetCore.Mvc;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    // A project's task board is served by TasksController (GET api/projects/{id}/board).
    public class ProjectsController(ProjectService projects) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<ProjectSummaryDto>>> List() => Ok(await projects.ListAsync());
//----------------------------------------------------------------------------------------------------------------------//
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProjectDto>> Get(int id) => Ok(await projects.GetAsync(id));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost]
        public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest req) =>
            Ok(await projects.CreateAsync(req, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPatch("{id:int}")]
        public async Task<ActionResult<ProjectDto>> Update(int id, UpdateProjectRequest req) =>
            Ok(await projects.UpdateAsync(id, req, UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpGet("{id:int}/members")]
        public async Task<ActionResult<List<ProjectMemberDto>>> Members(int id) => Ok(await projects.MembersAsync(id));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost("{id:int}/members")]
        public async Task<IActionResult> AddMember(int id, AddProjectMemberRequest req)
        {
            await projects.AddMemberAsync(id, req.UserId, UserId());
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        [HttpDelete("{id:int}/members/{userId:int}")]
        public async Task<IActionResult> RemoveMember(int id, int userId)
        {
            await projects.RemoveMemberAsync(id, userId, UserId());
            return NoContent();
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//