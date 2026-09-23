using System.Data.Common;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace QULOOBUL__MOE_MIENEEN_Backend.Data
{
    /// <summary>
    /// Hands the signed-in user's id to SQL Server as SESSION_CONTEXT('UserId') on every connection,
    /// which the Notification row-level security policy filters on (architecture doc, "Defence in
    /// depth": the database refuses another user's rows even if an application check is missed).
    /// Read-only, so SQL running on the connection cannot impersonate someone else. No user (login,
    /// background jobs, migrations) leaves it unset, and the policy lets those through.
    /// </summary>
    public class SessionContextInterceptor(IHttpContextAccessor http) : DbConnectionInterceptor
    {
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            using var cmd = Command(connection);
            cmd?.ExecuteNonQuery();
        }

        public override async Task ConnectionOpenedAsync(
            DbConnection connection, ConnectionEndEventData eventData, CancellationToken ct = default)
        {
            await using var cmd = Command(connection);
            if (cmd is not null) await cmd.ExecuteNonQueryAsync(ct);
        }

        private DbCommand? Command(DbConnection connection)
        {
            if (!int.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return null;

            var cmd = connection.CreateCommand();
            cmd.CommandText = "EXEC sp_set_session_context @key = N'UserId', @value = @id, @read_only = 1;";
            var p = cmd.CreateParameter();
            p.ParameterName = "@id";
            p.Value = userId;
            cmd.Parameters.Add(p);
            return cmd;
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
