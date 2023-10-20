using System.Security.AccessControl;

namespace ProactiveCaching
{
    public class ProactiveCache<T> : IDisposable
    {
        private readonly Func<Task<T>> _refreshDelegate;
        private readonly SafeProactiveLogger? _safeLogger;
        private Task<T> _cachedDataTask;
        private System.Timers.Timer _timer;

        /// <summary>Initializes a new instance of the <see cref="ProactiveCache{T}"/> class.</summary>
        /// <param name="refreshDelegate">The delegate to fetch fresh data of type <typeparamref name="T"/>.</param>
        /// <param name="refreshInterval">The period after which the cache should be refreshed.</param>
        /// <param name="logger">Optional logger instance for logging events within the cache.</param>
        /// <param name="refreshStart">The desired time for the first refresh. If null, uses the current time.</param>
        public ProactiveCache(Func<Task<T>> refreshDelegate, TimeSpan refreshInterval, IProactiveLogger? logger = null, DateTime? refreshStart = null)
        {
            _refreshDelegate = refreshDelegate ?? throw new ArgumentNullException(nameof(refreshDelegate));
            _safeLogger = logger != null ? new SafeProactiveLogger(logger) : null;

            // Initialize the cache immediately
            _cachedDataTask = ForceRefreshAsync();

            // ------------- Setup periodic refresh timer -----------
            var startTime = refreshStart ?? DateTime.Now;
            var now = DateTime.Now;
            var firstInterval = startTime - now;

            while (firstInterval < TimeSpan.Zero)
            {
                var countIntervalsToAdd = Math.Ceiling((double)Math.Abs(firstInterval.TotalMilliseconds) / (double)refreshInterval.TotalMilliseconds);
                startTime = startTime.AddTicks((long)(refreshInterval.Ticks * countIntervalsToAdd));
                firstInterval = startTime - now;
            }

            _timer = new System.Timers.Timer(firstInterval.TotalMilliseconds);
            _timer.Elapsed += async (sender, e) =>
            {
                // Reset the interval after the first tick
                _timer.Interval = refreshInterval.TotalMilliseconds;

                // Refresh the cache
                await ForceRefreshAsync();
            };
            _timer.Start();
        }

        /// <summary>Asynchronously retrieves the cached data.</summary>
        /// <returns>The cached data of type <typeparamref name="T"/>.</returns>
        public async Task<T> GetDataAsync()
        {
            return await _cachedDataTask;
        }

        /// <summary>Asynchronoushly forces refresh of the value</summary>
        /// <returns>The cached data of type <typeparamref name="T"/>.</returns>
        public async Task<T> ForceRefreshAsync()
        {
            try
            {
                var newData = await _refreshDelegate();
                _cachedDataTask = Task.FromResult(newData); // Update the cached data task with the new value.
                _safeLogger?.LogInformation("Cache refreshed successfully.");
            }
            catch (Exception ex)
            {   
                _safeLogger?.LogError(ex, $"Error encountered during refresh: {ex.Message}");

                // We only throw if _cachedDataTask is not defined, as we require the calling application to continue running using old cached data.
                if (_cachedDataTask == null) throw;
            }
            return await _cachedDataTask;
        }

        /// <summary>Releases the unmanaged resources used by the ProactiveCache and optionally releases the managed resources.</summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                // Dispose managed state.
                _timer?.Dispose();
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) method above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

    }
}