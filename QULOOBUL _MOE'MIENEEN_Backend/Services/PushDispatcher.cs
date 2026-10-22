using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// Third delivery channel next to SignalR (app open) and email: drains PushQueue and
    /// hands each notification to Azure Notification Hubs, so it reaches the phone's notification tray while the
    /// mobile app is closed.
    /// </summary>
    
    public class PushDispatcher(PushQueue pushQueue, PushHubService pushHub, IServiceScopeFactory scopes, ILogger<PushDispatcher> log) 
        : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var pushJob in pushQueue.ReadAllAsync(cancellationToken))
                {
                    if (!pushHub.IsConfigured)
                    {
                        continue;
                    }

                    try
                    {
                        if (await IsActiveAsync(pushJob.UserId, cancellationToken))
                        {
                            await pushHub.SendAsync(pushJob, cancellationToken);
                        }
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        log.LogError(exception, $"Push for notification {pushJob.NotificationId} failed", pushJob.NotificationId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                /* host shutting down */
            }
        }

        //----------------------------------------------------------------------------------------------------------------------//
        private async Task<bool> IsActiveAsync(int userId, CancellationToken cancellationToken)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await db.Users.AnyAsync(user => user.UserId == userId && user.IsActive, cancellationToken);
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
