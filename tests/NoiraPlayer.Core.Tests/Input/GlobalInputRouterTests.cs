using System;
using System.Collections.Generic;
using System.Linq;
using NoiraPlayer.Core.Input;
using Xunit;

namespace NoiraPlayer.Core.Tests.Input;

public sealed class GlobalInputRouterTests
{
    [Fact]
    public void Latest_Registration_Owns_Input_And_Dispose_Restores_Previous_Context()
    {
        var resets = 0;
        var router = new GlobalInputRouter(() => resets++);
        var browse = new List<InputEnvelope>();
        var playback = new List<InputEnvelope>();
        using var browseRegistration = router.Register(InputContext.BrowseWeb, browse.Add);

        router.Dispatch(Envelope(1));
        using (router.Register(InputContext.NativePlayback, playback.Add))
        {
            router.Dispatch(Envelope(2));
        }
        router.Dispatch(Envelope(3));

        Assert.Equal([1L, 3L], browse.Select(item => item.Sequence));
        Assert.Equal([2L], playback.Select(item => item.Sequence));
        Assert.Equal(InputContext.BrowseWeb, router.ActiveContext);
        Assert.Equal(3, resets);
    }

    [Fact]
    public void Consumer_Exception_Is_Reported_Without_Escaping_Dispatch()
    {
        var observed = new List<(InputContext Context, Exception Error)>();
        var router = new GlobalInputRouter(
            () => undefined(),
            (context, error) => observed.Add((context, error)));
        var failure = new InvalidOperationException("transport failed");
        using var registration = router.Register(
            InputContext.BrowseWeb,
            _ => throw failure);

        Exception? exception = null;
        try
        {
            router.Dispatch(Envelope(1));
        }
        catch (Exception error)
        {
            exception = error;
        }

        Assert.Null(exception);
        Assert.Equal(InputContext.BrowseWeb, Assert.Single(observed).Context);
        Assert.Same(failure, observed[0].Error);
    }

    [Fact]
    public void Dispose_Is_Idempotent()
    {
        var resets = 0;
        var router = new GlobalInputRouter(() => resets++);
        var registration = router.Register(InputContext.BrowseWeb, _ => { });

        registration.Dispose();
        registration.Dispose();

        Assert.Equal(InputContext.None, router.ActiveContext);
        Assert.Equal(2, resets);
    }

    private static InputEnvelope Envelope(long sequence) => new(
        sequence,
        InputCommand.Accept,
        InputPhase.Pressed,
        InputDeviceKind.Gamepad,
        sequence);

    private static void undefined()
    {
    }
}
