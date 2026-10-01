using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace QULOOBUL__MOE_MIENEEN_Shared.Notifications
{
    public record NotificationDto(
    int NotificationId, string Type, string Message, bool IsRead,
    string? EntityType, int? EntityId);
//----------------------------------------------------------------------------------------------------------------------//
    public record MarkReadRequest
    {
        [Required, MinLength(1)] 
        public IReadOnlyList<int> NotificationIds { get; init; } = [];
    }
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>UC-08 per-type preference. A type with no saved preference is enabled.</summary>
    public record NotificationPreferenceDto(string Type, bool Enabled);

    public record UpdateNotificationPreferencesRequest
    {
        [Required, MinLength(1)]
        public IReadOnlyList<NotificationPreferenceDto> Preferences { get; init; } = [];
    }
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>Admin-only manual broadcast (UC-08 alternate flow).</summary>
    public record SendNotificationRequest
    {
        [Required, MinLength(1)] 
        public IReadOnlyList<int> UserIds { get; init; } = [];

        [Required, MaxLength(500)] 
        public string Message { get; init; } = string.Empty;

        [MaxLength(100)] 
        public string Type { get; init; } = "Announcement";
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//