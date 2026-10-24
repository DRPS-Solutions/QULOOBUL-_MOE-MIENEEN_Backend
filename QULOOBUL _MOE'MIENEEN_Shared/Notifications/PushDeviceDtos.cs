using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace QULOOBUL__MOE_MIENEEN_Shared.Notifications
{
    /// <summary>
    /// This is sent by the Mobile App after sign-in so the API
    /// can register the phone with Azure Notification Hub.
    /// PushChannel is the FCM registration token Firebase gives the app.
    /// </summary>
    public record PushInstallationRequest
    {
        [Required, MaxLength(64)]
        public string InstallationId { get; init; } = string.Empty;

        [Required, MaxLength(4096)]
        public string PushChannel { get; init; } = string.Empty;

        [MaxLength(20)]
        public string Platform { get; init; } = "android";
    }
}
