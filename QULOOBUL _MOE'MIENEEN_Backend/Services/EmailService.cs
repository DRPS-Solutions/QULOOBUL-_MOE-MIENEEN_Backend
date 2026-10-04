using Azure;
using Azure.Communication.Email;
using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// Transactional email over Azure Communication Services (architecture doc, "Reliable notification
    /// delivery" / Recommendation Summary). A notification always lands in-app; the email is the
    /// second channel so a staff member does not have to check the dashboard to learn about it.
    ///
    /// No connection string configured (local dev, CI, tests) means this is a no-op — the platform
    /// runs without it rather than failing every write that raises a notification.
    /// Reference: Microsoft (2026) Quickstart: How to send an email using Azure Communication Services. Available at: https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/email/send-email (Accessed: 4 October 2026).
    /// </summary>
    public class EmailService(IConfiguration config, AppDbContext db, ILogger<EmailService> log)
    {
        public const string SentAction = "email.sent";
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Emails the owner of an already-committed notification. Call after SaveChanges, alongside
        /// NotificationService.PushAsync. Never throws: a bounced email must not roll back the task
        /// assignment or the approval that caused it.
        /// </summary>
        public async Task SendAsync(Notification notification, string subject)
        {
            var key = config["Email:ConnectionString"];
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
        /// because the recipient cannot sign in yet. Never throws. Unconfigured, the link is logged
        /// instead, so a local or CI setup can still activate accounts.
        /// </summary>
        public async Task SendAccountEmailAsync(string address, string subject, string message)
        {
            var key = config["Email:ConnectionString"];
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
        private async Task<bool> PostAsync(string connectionString, string address, string subject, string message)
        {
            // Must be an address on the domain linked to the ACS resource: infra/main.bicep sets it
            // to DoNotReply@<the Azure-managed domain>.
            var from = config["Email:From"] ?? throw new InvalidOperationException("Email:From is not set.");
            // The message carries user-entered text (task titles, request details), so it is
            // encoded rather than interpolated into the markup.
            // Reference: OWASP Foundation (2026) Cross Site Scripting Prevention Cheat Sheet. Available at: https://cheatsheetseries.owasp.org/cheatsheets/Cross_Site_Scripting_Prevention_Cheat_Sheet.html (Accessed: 4 October 2026).
            var html = $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>";
            try
            {
                // WaitUntil.Started: return once ACS has accepted it rather than polling for delivery.
                await new EmailClient(connectionString).SendAsync(WaitUntil.Started, from, address, subject, html);
                return true;
            }
            catch (RequestFailedException ex)
            {
                log.LogWarning("ACS rejected \"{Subject}\": {Status} {Code}", subject, ex.Status, ex.ErrorCode);
                return false;
            }
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
