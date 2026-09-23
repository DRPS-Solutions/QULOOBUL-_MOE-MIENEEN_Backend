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
        // No ReviewRequests policy on the review routes: a facility manager reviews escalated requests
        // without holding it. RequestReviewService makes that call.
        [HttpPost("{id:int}/begin-review")]
        public async Task<IActionResult> BeginReview(int id)
        {
            await requests.BeginReviewAsync(id, UserId());
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost("{id:int}/review")]
        public async Task<IActionResult> Review(int id, ReviewRequestRequest req)
        {
            await requests.ReviewAsync(id, UserId(), req.Approve, req.Note);
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        [HttpPost("{id:int}/resubmit")]
        public async Task<IActionResult> Resubmit(int id, ResubmitRequestRequest req)
        {
            await requests.ResubmitAsync(id, UserId(), req.Detail);
            return NoContent();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>SM-02 fulfil(admin): Approved → Completed.</summary>
        [HttpPost("{id:int}/complete"), Authorize(Policy = Permissions.ReviewRequests)]
        public async Task<IActionResult> Complete(int id)
        {
            await requests.CompleteAsync(id, UserId());
            return NoContent();
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//