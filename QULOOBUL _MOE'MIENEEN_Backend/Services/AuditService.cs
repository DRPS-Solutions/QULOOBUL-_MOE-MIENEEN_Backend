using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// Append-only writes to Audit_Log. Nothing here updates or deletes a row
    /// (architecture doc, "Audit_Log: an append-only record").
    /// The row is added to the caller's Unit of Work and committed by the caller's
    /// SaveChanges, so the audit entry lands in the same transaction as the change it records.
    /// Reference: Fowler, M. (2003) Patterns of enterprise application architecture. Boston: Addison-Wesley.
    /// Reference: OWASP Foundation (2025) OWASP Top 10:2025. Available at: https://owasp.org/Top10/2025/ (Accessed: 8 August 2026).
    /// </summary>
    public class AuditService(AppDbContext db)
    {
        /// <summary>Queue an audit entry. Caller calls SaveChanges to commit it.</summary>
        public AuditLog Record(int userId, string action, string entityType, int entityId)
        {
            var entry = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Timestamp = DateTime.UtcNow,
            };
            db.AuditLogs.Add(entry);
            return entry;
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Queue an audit entry and commit it now, for callers with nothing else pending.</summary>
        public async Task RecordAndSaveAsync(int userId, string action, string entityType, int entityId)
        {
            Record(userId, action, entityType, entityId);
            await db.SaveChangesAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>The user's most recent audit entries, newest first. Read-only.</summary>
        public Task<List<AuditEntryDto>> ForUserAsync(int userId, int take) =>
            db.AuditLogs.AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.Timestamp)
                .Take(Math.Clamp(take, 1, 500))
                .Select(a => new AuditEntryDto(a.Timestamp, a.Action, a.EntityType, a.EntityId))
                .ToListAsync();
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//