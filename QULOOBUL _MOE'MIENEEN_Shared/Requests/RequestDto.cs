using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace QULOOBUL__MOE_MIENEEN_Shared.Requests
{
    public record ResourceRequestDto(
    int RequestId, int RequestedBy, int CategoryId, string CategoryName,
    string Status, string Priority, DateTime DateSubmitted, int? ReviewedBy,
    IReadOnlyList<RequestStatusHistoryDto> History,
    string? Detail);
//----------------------------------------------------------------------------------------------------------------------//
    public record CreateResourceRequest
    {
        [Required]
        public int CategoryId { get; init; }

        [MaxLength(20)] 
        public string Priority { get; init; } = "Normal";

        [MaxLength(1000)] 
        public string? Detail { get; init; }
    }
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>Admin review — SM-02 guarded server-side.</summary>
    public record ReviewRequestRequest
    {
        [Required] 
        public bool Approve { get; init; }

        [MaxLength(500)] 
        public string? Note { get; init; }
    }
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>SM-02 resubmit: optionally revise what is being asked for.</summary>
    public record ResubmitRequestRequest
    {
        [MaxLength(1000)]
        public string? Detail { get; init; }
    }
//----------------------------------------------------------------------------------------------------------------------//
    public record RequestStatusHistoryDto(string NewStatus, int ChangedBy, DateTime TimeCreated);
    public record RequestCategoryDto(int CategoryId, string CategoryName);
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//