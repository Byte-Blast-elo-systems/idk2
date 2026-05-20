using System.Threading;

namespace DiscordReactionBot.Services
{
    public class ReactionManager
    {
        private readonly object _lock = new();
        private CancellationTokenSource? _cts;

        public CancellationToken GetToken()
        {
            lock (_lock)
            {
                if (_cts == null || _cts.IsCancellationRequested)
                {
                    _cts?.Dispose();
                    _cts = new CancellationTokenSource();
                }

                return _cts.Token;
            }
        }

        public void CancelCurrent()
        {
            lock (_lock)
            {
                if (_cts == null) return;
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }
    }
}
