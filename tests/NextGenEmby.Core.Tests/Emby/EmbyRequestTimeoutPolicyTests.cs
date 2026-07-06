using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using Xunit;

namespace NextGenEmby.Core.Tests.Emby;

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
}
