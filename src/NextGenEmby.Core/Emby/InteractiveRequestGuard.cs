using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NextGenEmby.Core.Emby
{
    public static class InteractiveRequestGuard
    {
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
    }
}
