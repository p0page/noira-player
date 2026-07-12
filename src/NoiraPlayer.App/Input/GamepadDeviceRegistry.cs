using System;
using System.Collections.Generic;
using System.Diagnostics;
using NoiraPlayer.Core.Input;
using Windows.Gaming.Input;
using Windows.UI.Xaml;

namespace NoiraPlayer.App.Input
{
    internal sealed class GamepadDeviceRegistry
    {
        private readonly Stopwatch _clock = new Stopwatch();
        private readonly Dictionary<Gamepad, GamepadInputNormalizerState> _normalizers = [];
        private readonly List<Gamepad> _gamepads = [];
        private readonly GamepadInputPump<Gamepad> _pump;
        private readonly object _sync = new object();
        private readonly DispatcherTimer _timer;
        private bool _started;

        public GamepadDeviceRegistry()
        {
            _pump = new GamepadInputPump<Gamepad>(
                ReadGamepad,
                () => _clock.ElapsedMilliseconds,
                input => Input?.Invoke(input),
                ReportFailure);
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16),
            };
            _timer.Tick += Timer_OnTick;
        }

        public event Action<InputEnvelope>? Input;

        public void Start()
        {
            if (_started)
            {
                return;
            }

            Gamepad.GamepadAdded += Gamepad_OnAdded;
            Gamepad.GamepadRemoved += Gamepad_OnRemoved;
            try
            {
                lock (_sync)
                {
                    _gamepads.Clear();
                    _normalizers.Clear();
                    foreach (var gamepad in Gamepad.Gamepads)
                    {
                        AddGamepadLocked(gamepad);
                    }
                }
            }
            catch
            {
                Gamepad.GamepadAdded -= Gamepad_OnAdded;
                Gamepad.GamepadRemoved -= Gamepad_OnRemoved;
                throw;
            }
            _clock.Restart();
            _pump.Start();
            _timer.Start();
            _started = true;
            InputDiagnosticsLog.Write("gamepad provider started");
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _timer.Stop();
            Gamepad.GamepadAdded -= Gamepad_OnAdded;
            Gamepad.GamepadRemoved -= Gamepad_OnRemoved;
            _pump.Stop();
            lock (_sync)
            {
                _gamepads.Clear();
                _normalizers.Clear();
            }
            _clock.Reset();
            _started = false;
            InputDiagnosticsLog.Write("gamepad provider stopped");
        }

        public void ResetInputState()
        {
            _pump.Reset();
        }

        private void Gamepad_OnAdded(object? sender, Gamepad gamepad)
        {
            lock (_sync)
            {
                AddGamepadLocked(gamepad);
            }
            InputDiagnosticsLog.Write("controller added");
        }

        private void Gamepad_OnRemoved(object? sender, Gamepad gamepad)
        {
            lock (_sync)
            {
                _gamepads.Remove(gamepad);
                _normalizers.Remove(gamepad);
            }
            InputDiagnosticsLog.Write("controller removed");
        }

        private void Timer_OnTick(object? sender, object e)
        {
            Gamepad[] devices;
            lock (_sync)
            {
                devices = _gamepads.ToArray();
            }

            var previousActive = _pump.ActiveDevice;
            _pump.Poll(devices);
            if (!ReferenceEquals(previousActive, _pump.ActiveDevice) &&
                _pump.ActiveDevice != null)
            {
                InputDiagnosticsLog.Write("controller claimed");
            }
        }

        private void AddGamepadLocked(Gamepad gamepad)
        {
            if (!_gamepads.Contains(gamepad))
            {
                _gamepads.Add(gamepad);
            }
        }

        private InputButtonState ReadGamepad(Gamepad gamepad)
        {
            var reading = gamepad.GetCurrentReading();
            GamepadInputNormalizerState normalizer;
            lock (_sync)
            {
                if (!_normalizers.TryGetValue(gamepad, out var existingNormalizer))
                {
                    existingNormalizer = new GamepadInputNormalizerState();
                    _normalizers.Add(gamepad, existingNormalizer);
                }

                normalizer = existingNormalizer;
            }

            return normalizer.Normalize(ToPhysicalState(reading));
        }

        private static void ReportFailure(GamepadInputFailure<Gamepad> failure)
        {
            string stage;
            switch (failure.Stage)
            {
                case GamepadInputFailureStage.DeviceReading:
                    stage = "controller reading";
                    break;
                case GamepadInputFailureStage.StateTransition:
                    stage = "input state transition";
                    break;
                case GamepadInputFailureStage.Consumer:
                    stage = "input consumer";
                    break;
                default:
                    stage = "input provider";
                    break;
            }

            InputDiagnosticsLog.Write(
                stage + " failed type=" + failure.Error.GetType().FullName +
                " hresult=0x" + failure.Error.HResult.ToString("X8"));
        }

        private static GamepadPhysicalState ToPhysicalState(GamepadReading reading) => new(
            reading.Buttons.HasFlag(GamepadButtons.DPadUp),
            reading.Buttons.HasFlag(GamepadButtons.DPadDown),
            reading.Buttons.HasFlag(GamepadButtons.DPadLeft),
            reading.Buttons.HasFlag(GamepadButtons.DPadRight),
            reading.Buttons.HasFlag(GamepadButtons.A),
            reading.Buttons.HasFlag(GamepadButtons.B),
            reading.Buttons.HasFlag(GamepadButtons.Menu),
            reading.Buttons.HasFlag(GamepadButtons.View),
            reading.LeftThumbstickX,
            reading.LeftThumbstickY);
    }
}
