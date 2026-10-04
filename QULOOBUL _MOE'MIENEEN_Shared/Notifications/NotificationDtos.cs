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
    public record NotificationPreferenceDto(string Type, bool Enabled)
    {
        /// <summary>An admin's direct announcement ignores preferences, so clients don't offer a switch for it.</summary>
        public bool CanMute => Type != "Announcement";

        /// <summary>What the switch says on the web Profile and the mobile settings page. Unknown types show the key.</summary>
        public string Label => Type switch
        {
            "TaskAssigned" => "A task is assigned to me",
            "TaskCancelled" => "A task of mine is cancelled",
            "DeadlineApproaching" => "A task deadline is approaching",
            "ProjectAssigned" => "I'm added to an event",
            "ProjectCancelled" => "An event I'm on is cancelled",
            "EventInvite" => "I'm invited to a calendar event",
            "RequestSubmitted" => "A request is submitted for review",
            "RequestReviewed" => "My request is approved or declined",
            "RequestCompleted" => "My request is fulfilled",
            "RequestEscalated" => "A request is escalated",
            "UserLocked" => "An account is locked",
            "PasswordResetRequested" => "Someone asks for a password reset",
            _ => Type,
        };
    }

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