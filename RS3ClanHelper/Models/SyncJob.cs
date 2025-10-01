using System.Threading;

namespace RS3ClanHelper.Models
{
    public class SyncJob
    {
        public int IntervalHours { get; set; }
        public CancellationTokenSource Cts { get; set; } = new();
    }
}
