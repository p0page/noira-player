using System;
using System.Collections.Generic;

namespace NoiraPlayer.Core.Input;

public sealed class GlobalInputRouter
{
    private readonly Action<InputContext, Exception>? _onConsumerError;
    private readonly List<Registration> _registrations = [];
    private readonly Action _resetInputState;

    public GlobalInputRouter(
        Action resetInputState,
        Action<InputContext, Exception>? onConsumerError = null)
    {
        _resetInputState = resetInputState ??
            throw new ArgumentNullException(nameof(resetInputState));
        _onConsumerError = onConsumerError;
    }

    public InputContext ActiveContext =>
        _registrations.Count == 0
            ? InputContext.None
            : _registrations[^1].Context;

    public IDisposable Register(InputContext context, Action<InputEnvelope> consumer)
    {
        if (context == InputContext.None)
        {
            throw new ArgumentOutOfRangeException(nameof(context));
        }

        if (consumer == null)
        {
            throw new ArgumentNullException(nameof(consumer));
        }

        _resetInputState();
        var registration = new Registration(this, context, consumer);
        _registrations.Add(registration);
        return registration;
    }

    public void Dispatch(InputEnvelope input)
    {
        if (_registrations.Count == 0)
        {
            return;
        }

        var active = _registrations[^1];
        try
        {
            active.Consumer(input);
        }
        catch (Exception error)
        {
            _onConsumerError?.Invoke(active.Context, error);
        }
    }

    public void ResetInputState()
    {
        _resetInputState();
    }

    private void Remove(Registration registration)
    {
        var index = _registrations.IndexOf(registration);
        if (index < 0)
        {
            return;
        }

        var wasActive = index == _registrations.Count - 1;
        if (wasActive)
        {
            _resetInputState();
        }

        _registrations.RemoveAt(index);
    }

    private sealed class Registration : IDisposable
    {
        private GlobalInputRouter? _owner;

        public Registration(
            GlobalInputRouter owner,
            InputContext context,
            Action<InputEnvelope> consumer)
        {
            _owner = owner;
            Context = context;
            Consumer = consumer;
        }

        public InputContext Context { get; }

        public Action<InputEnvelope> Consumer { get; }

        public void Dispose()
        {
            var owner = _owner;
            if (owner == null)
            {
                return;
            }

            _owner = null;
            owner.Remove(this);
        }
    }
}
