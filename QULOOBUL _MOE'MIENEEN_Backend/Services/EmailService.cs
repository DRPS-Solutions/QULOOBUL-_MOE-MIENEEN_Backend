using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// Transactional email over the Resend REST API (architecture doc, "Reliable notification
    /// delivery" / Recommendation Summary). A notification always lands in-app; the email is the
    /// second channel so a staff member does not have to check the dashboard to learn about it.
    ///
    /// No API key configured (local dev, CI, tests) means this is a no-op — the platform runs
    /// without it rather than failing every write that raises a notification.
    /// </summary>
    public class EmailService(
        IHttpClientFactory http, IConfiguration config, AppDbContext db, ILogger<EmailService> log)
    {
        public const string SentAction = "email.sent";
        private const string Endpoint = "https://api.resend.com/emails";
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Emails the owner of an already-committed notification. Call after SaveChanges, alongside
        /// NotificationService.PushAsync. Never throws: a bounced email must not roll back the task
        /// assignment or the approval that caused it.
        /// </summary>
        public async Task SendAsync(Notification notification, string subject)
        {
            var key = config["Email:ResendApiKey"];
            if (string.IsNullOrWhiteSpace(key) || notification.IsMuted) return;

            if (notification.NotificationId == 0)
                throw new InvalidOperationException("Save the notification before emailing it: its id is the idempotency key.");

            try
            {
                // The doc asks for "an idempotency log in Azure SQL". Audit_Log already is one:
                // append-only, keyed to a user, and it makes a duplicate send visible rather than silent.
                if (await db.AuditLogs.AnyAsync(a => a.Action == SentAction
                        && a.EntityType == nameof(Notification)
                        && a.EntityId == notification.NotificationId))
                    return;

                var address = await db.Users
                    .Where(u => u.UserId == notification.UserId && u.IsActive)
                    .Select(u => u.Email).FirstOrDefaultAsync();
                if (string.IsNullOrWhiteSpace(address)) return;

                if (!await PostAsync(key, address, subject, notification.Message)) return;

                db.AuditLogs.Add(new AuditLog
                {
                    UserId = notification.UserId,
                    Action = SentAction,
                    EntityType = nameof(Notification),
                    EntityId = notification.NotificationId,
                    Timestamp = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Emailing notification {Id} failed", notification.NotificationId);
            }
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Account mail (SM-03 activation / password reset): goes to an address, not a notification,
        /// because the recipient cannot sign in yet. Never throws. With no API key the link is logged
        /// instead, so a local or CI setup can still activate accounts.
        /// </summary>
        public async Task SendAccountEmailAsync(string address, string subject, string message)
        {
            var key = config["Email:ResendApiKey"];
            if (string.IsNullOrWhiteSpace(key))
            {
                log.LogInformation("Email not configured; would send \"{Subject}\" to {Address}: {Message}",
                    subject, address, message);
                return;
            }
            try { await PostAsync(key, address, subject, message); }
            catch (Exception ex) { log.LogError(ex, "Sending \"{Subject}\" failed", subject); }
        }
//----------------------------------------------------------------------------------------------------------------------//
        private async Task<bool> PostAsync(string key, string address, string subject, string message)
        {
            var client = http.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            var response = await client.PostAsJsonAsync(Endpoint, new
            {
                from = config["Email:From"] ?? "QULOOBUL MOE'MIENEEN <noreply@quloobul.local>",
                to = new[] { address },
                subject,
                // The message carries user-entered text (task titles, request details), so it is
                // encoded rather than interpolated into the markup.
                html = $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>",
            });

            if (!response.IsSuccessStatusCode)
                log.LogWarning("Resend rejected \"{Subject}\": {Status}", subject, response.StatusCode);
            return response.IsSuccessStatusCode;
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
