using System.Threading.Channels;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    public record PushJob(int NotificationId, int UserId, string TypeName, string Message, string? EntityType, int? EntityId);

    public class PushQueue
    {
        private readonly Channel<PushJob> _channel = Channel.CreateBounded<PushJob>(
            new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

        public void Enqueue(PushJob pushJob) => _channel.Writer.TryWrite(pushJob);

        public IAsyncEnumerable<PushJob> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);

        /// <summary>
        /// Takes one waiting push without blocking
        /// </summary>
        public bool TryDequeue(out PushJob? pushJob) => _channel.Reader.TryRead(out pushJob);
    }
}
