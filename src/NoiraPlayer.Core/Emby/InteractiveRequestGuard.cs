using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoiraPlayer.Core.Emby
{
    public static class InteractiveRequestGuard
    {
        public static async Task<T> WithTimeoutAsync<T>(Func<Task<T>> requestFactory, TimeSpan timeout)
        {
            if (requestFactory == null)
            {
                throw new ArgumentNullException(nameof(requestFactory));
            }

            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
            }

            var request = Task.Run(requestFactory);
            return await WithTimeoutAsync(request, timeout).ConfigureAwait(false);
        }

        public static async Task<T> WithTimeoutAsync<T>(Task<T> request, TimeSpan timeout)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
            }

            var completed = await Task.WhenAny(request, Task.Delay(timeout)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, request))
            {
                throw new TimeoutException("The interactive request did not complete in time.");
            }

            return await request.ConfigureAwait(false);
        }

        public static async Task<IReadOnlyList<T>> TryGetListOrEmptyAsync<T>(
            Task<IReadOnlyList<T>> request,
            TimeSpan timeout)
        {
            try
            {
                var result = await WithTimeoutAsync(request, timeout).ConfigureAwait(false);
                return result ?? Array.Empty<T>();
            }
            catch
            {
                return Array.Empty<T>();
            }
        }

        public static async Task<IReadOnlyList<T>> TryGetListOrEmptyAsync<T>(
            Func<Task<IReadOnlyList<T>>> requestFactory,
            TimeSpan timeout,
            int maxAttempts)
        {
            if (requestFactory == null)
            {
                throw new ArgumentNullException(nameof(requestFactory));
            }

            if (maxAttempts <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Attempt count must be positive.");
            }

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var result = await WithTimeoutAsync(requestFactory, timeout).ConfigureAwait(false);
                    return result ?? Array.Empty<T>();
                }
                catch
                {
                    if (attempt == maxAttempts)
                    {
                        return Array.Empty<T>();
                    }
                }
            }

            return Array.Empty<T>();
        }

        public static async Task<IReadOnlyList<T>> TryGetRequiredListOrEmptyAsync<T>(
            Func<Task<IReadOnlyList<T>>> requestFactory,
            TimeSpan timeout,
            int maxAttempts)
        {
            if (requestFactory == null)
            {
                throw new ArgumentNullException(nameof(requestFactory));
            }

            if (maxAttempts <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Attempt count must be positive.");
            }

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var result = await WithTimeoutAsync(requestFactory, timeout).ConfigureAwait(false);
                    if (result != null && result.Count > 0)
                    {
                        return result;
                    }
                }
                catch
                {
                }
            }

            return Array.Empty<T>();
        }
    }
}
