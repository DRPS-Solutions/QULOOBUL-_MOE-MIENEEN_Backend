using Microsoft.AspNetCore.Mvc;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Search;

namespace QULOOBUL__MOE_MIENEEN_Backend.Controllers
{
    /// <summary>Global search across tasks, requests, projects, meetings, events and files.</summary>
    public class SearchController(SearchService search) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<List<SearchResultDto>>> Search(string? q) =>
            Ok(await search.SearchAsync(q, UserId()));
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
