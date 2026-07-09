using System;
using Windows.Storage;

namespace NoiraPlayer.App.Storage
{
    public sealed class ApplicationDataDeviceIdProvider
    {
        private const string DeviceIdKey = "Device.Id";
        private readonly ApplicationDataContainer _settings;

        public ApplicationDataDeviceIdProvider()
            : this(ApplicationData.Current.LocalSettings)
        {
        }

        public ApplicationDataDeviceIdProvider(ApplicationDataContainer settings)
        {
            _settings = settings;
        }

        public string GetOrCreate()
        {
            object? value;
            if (_settings.Values.TryGetValue(DeviceIdKey, out value) && value != null)
            {
                var existingDeviceId = value.ToString();
                if (!string.IsNullOrWhiteSpace(existingDeviceId))
                {
                    return existingDeviceId;
                }
            }

            var deviceId = Guid.NewGuid().ToString("N");
            _settings.Values[DeviceIdKey] = deviceId;
            return deviceId;
        }
    }
}
