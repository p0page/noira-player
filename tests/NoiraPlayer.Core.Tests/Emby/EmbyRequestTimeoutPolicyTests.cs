using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NoiraPlayer.Core.Emby;
using Xunit;

namespace NoiraPlayer.Core.Tests.Emby;

public sealed class EmbyRequestTimeoutPolicyTests
{
    [Fact]
    public void Interactive_Request_Timeout_Is_Short_Enough_For_TV_Recovery()
    {
        var timeout = EmbyRequestTimeoutPolicy.InteractiveRequestTimeout;

        Assert.True(timeout >= TimeSpan.FromSeconds(5));
        Assert.True(timeout <= TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Interactive_Request_Retry_Count_Is_Bounded_For_TV_Responsiveness()
    {
        Assert.Equal(2, EmbyRequestTimeoutPolicy.InteractiveRequestMaxAttempts);
    }

    [Fact]
    public void Required_Interactive_Request_Retry_Count_Is_Bounded_But_Stronger()
    {
        Assert.Equal(3, EmbyRequestTimeoutPolicy.RequiredInteractiveRequestMaxAttempts);
    }

    [Fact]
    public async Task Interactive_Request_Guard_Returns_Completed_Result()
    {
        var result = await InteractiveRequestGuard.WithTimeoutAsync(
            Task.FromResult("ready"),
            TimeSpan.FromSeconds(1));

        Assert.Equal("ready", result);
    }

    [Fact]
    public async Task Interactive_Request_Guard_Throws_Timeout_When_Request_Does_Not_Complete()
    {
        var pendingRequest = new TaskCompletionSource<string>();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            InteractiveRequestGuard.WithTimeoutAsync(
                pendingRequest.Task,
                TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public async Task Interactive_Request_Guard_Throws_Timeout_When_Request_Factory_Blocks_Before_Returning_Task()
    {
        await Assert.ThrowsAsync<TimeoutException>(() =>
            InteractiveRequestGuard.WithTimeoutAsync(
                () =>
                {
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                    return Task.FromResult("late");
                },
                TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public async Task Interactive_List_Guard_Returns_Empty_List_When_Request_Does_Not_Complete()
    {
        var pendingRequest = new TaskCompletionSource<IReadOnlyList<string>>();

        var result = await InteractiveRequestGuard.TryGetListOrEmptyAsync(
            pendingRequest.Task,
            TimeSpan.FromMilliseconds(1));

        Assert.Empty(result);
    }

    [Fact]
    public async Task Interactive_List_Guard_Returns_Completed_List()
    {
        var result = await InteractiveRequestGuard.TryGetListOrEmptyAsync(
            Task.FromResult<IReadOnlyList<string>>(new[] { "ready" }),
            TimeSpan.FromSeconds(1));

        Assert.Equal(new[] { "ready" }, result);
    }

    [Fact]
    public async Task Interactive_List_Guard_Retries_Request_Factory_Before_Returning_Empty()
    {
        var attempts = 0;

        var result = await InteractiveRequestGuard.TryGetListOrEmptyAsync(
            () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new InvalidOperationException("transient");
                }

                return Task.FromResult<IReadOnlyList<string>>(new[] { "ready" });
            },
            TimeSpan.FromSeconds(1),
            maxAttempts: 2);

        Assert.Equal(2, attempts);
        Assert.Equal(new[] { "ready" }, result);
    }

    [Fact]
    public async Task Interactive_List_Guard_Returns_Empty_After_Bounded_Attempts()
    {
        var attempts = 0;

        var result = await InteractiveRequestGuard.TryGetListOrEmptyAsync<string>(
            () =>
            {
                attempts++;
                throw new InvalidOperationException("still unavailable");
            },
            TimeSpan.FromSeconds(1),
            maxAttempts: 2);

        Assert.Equal(2, attempts);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Required_Interactive_List_Guard_Retries_Empty_Results_Until_Non_Empty()
    {
        var attempts = 0;

        var result = await InteractiveRequestGuard.TryGetRequiredListOrEmptyAsync(
            () =>
            {
                attempts++;
                if (attempts < 3)
                {
                    return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
                }

                return Task.FromResult<IReadOnlyList<string>>(new[] { "library" });
            },
            TimeSpan.FromSeconds(1),
            maxAttempts: 3);

        Assert.Equal(3, attempts);
        Assert.Equal(new[] { "library" }, result);
    }
}
