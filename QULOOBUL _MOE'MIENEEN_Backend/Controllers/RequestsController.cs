using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Requests;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    public class RequestsController(RequestReviewService requests) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<ResourceRequestDto>>> List() => Ok(await requests.ListAsync(UserId()));
//----------------------------------------------------------------------------------------------------------------------//
        [HttpGet("categories")]
        public async Task<ActionResult<List<RequestCategoryDto>>> Categories() => Ok(await requests.CategoriesAsync());
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost]
        public async Task<ActionResult<int>> Submit(CreateResourceRequest req) =>
            Ok((await requests.SubmitAsync(UserId(), req.CategoryId, req.Priority, req.Detail)).RequestId);
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost("{id:int}/review"), Authorize(Policy = Permissions.ReviewRequests)]
        public async Task<IActionResult> Review(int id, ReviewRequestRequest req)
        {
            await requests.ReviewAsync(id, UserId(), req.Approve, req.Note);
            return NoContent();
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//