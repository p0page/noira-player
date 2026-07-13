using System;
using System.Collections.Generic;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualityStartupTransportCallEvidence
    {
        public const string StageName = "native.open";
        public const string BuiltinProvider = "ffmpeg-builtin";
        public const string InstrumentedProvider = "instrumented-ffmpeg-avio";
        public const string UnavailableStatus = "unavailable";
        public const string MeasuredStatus = "measured";

        public static readonly IReadOnlyList<string> ComponentNames = new[]
        {
            "ffmpeg.open-input",
            "ffmpeg.find-stream-info",
            "native.startup-seek",
            "native.first-frame.demux-read"
        };

        public static readonly IReadOnlyList<string> FieldNames = new[]
        {
            "transportProvider",
            "transportCallEvidenceStatus",
            "transportReadCalls",
            "transportSeekCalls",
            "transportReadWaitMs",
            "transportSeekWaitMs",
            "transportSeekDistanceBytes"
        };

        public static string CreateSignal(string componentName, string fieldName)
        {
            return "startup.stage." + StageName + ".component." + componentName + "." + fieldName;
        }

        public static bool TryParseSignal(
            string signal,
            out string componentName,
            out string fieldName)
        {
            foreach (var candidateComponent in ComponentNames)
            {
                foreach (var candidateField in FieldNames)
                {
                    if (string.Equals(
                        signal,
                        CreateSignal(candidateComponent, candidateField),
                        StringComparison.Ordinal))
                    {
                        componentName = candidateComponent;
                        fieldName = candidateField;
                        return true;
                    }
                }
            }

            componentName = "";
            fieldName = "";
            return false;
        }

        public static PlaybackQualityStartupComponent? FindComponent(
            PlaybackQualityReport report,
            string componentName)
        {
            if (report == null)
            {
                return null;
            }

            foreach (var stage in report.Startup.Stages)
            {
                if (!string.Equals(stage.Name, StageName, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var component in stage.Components)
                {
                    if (string.Equals(component.Name, componentName, StringComparison.Ordinal))
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        public static bool HasValidSignalValue(PlaybackQualityReport report, string signal)
        {
            if (!TryParseSignal(signal, out var componentName, out var fieldName))
            {
                return false;
            }

            var component = FindComponent(report, componentName);
            if (component == null)
            {
                return false;
            }

            switch (fieldName)
            {
                case "transportProvider":
                    return IsKnownProvider(component.TransportProvider);
                case "transportCallEvidenceStatus":
                    return IsKnownStatus(component.TransportCallEvidenceStatus);
                case "transportReadCalls":
                    return HasExpectedValue(component, component.TransportReadCalls.HasValue);
                case "transportSeekCalls":
                    return HasExpectedValue(component, component.TransportSeekCalls.HasValue);
                case "transportReadWaitMs":
                    return HasExpectedValue(component, IsFiniteNonNegative(component.TransportReadWaitMs));
                case "transportSeekWaitMs":
                    return HasExpectedValue(component, IsFiniteNonNegative(component.TransportSeekWaitMs));
                case "transportSeekDistanceBytes":
                    return HasExpectedValue(component, component.TransportSeekDistanceBytes.HasValue);
                default:
                    return false;
            }
        }

        public static bool HasConsistentContract(PlaybackQualityStartupComponent component)
        {
            if (component == null ||
                !IsKnownProvider(component.TransportProvider) ||
                !IsKnownStatus(component.TransportCallEvidenceStatus))
            {
                return false;
            }

            if (string.Equals(component.TransportProvider, BuiltinProvider, StringComparison.Ordinal))
            {
                return string.Equals(
                        component.TransportCallEvidenceStatus,
                        UnavailableStatus,
                        StringComparison.Ordinal) &&
                    !component.TransportReadCalls.HasValue &&
                    !component.TransportSeekCalls.HasValue &&
                    !component.TransportReadWaitMs.HasValue &&
                    !component.TransportSeekWaitMs.HasValue &&
                    !component.TransportSeekDistanceBytes.HasValue;
            }

            return string.Equals(
                    component.TransportCallEvidenceStatus,
                    MeasuredStatus,
                    StringComparison.Ordinal) &&
                component.TransportReadCalls.HasValue &&
                component.TransportSeekCalls.HasValue &&
                IsFiniteNonNegative(component.TransportReadWaitMs) &&
                IsFiniteNonNegative(component.TransportSeekWaitMs) &&
                component.TransportSeekDistanceBytes.HasValue;
        }

        public static bool IsKnownProvider(string provider)
        {
            return string.Equals(provider, BuiltinProvider, StringComparison.Ordinal) ||
                string.Equals(provider, InstrumentedProvider, StringComparison.Ordinal);
        }

        public static bool IsKnownStatus(string status)
        {
            return string.Equals(status, UnavailableStatus, StringComparison.Ordinal) ||
                string.Equals(status, MeasuredStatus, StringComparison.Ordinal);
        }

        private static bool HasExpectedValue(
            PlaybackQualityStartupComponent component,
            bool hasValidValue)
        {
            return string.Equals(
                    component.TransportCallEvidenceStatus,
                    MeasuredStatus,
                    StringComparison.Ordinal)
                ? hasValidValue
                : string.Equals(
                    component.TransportCallEvidenceStatus,
                    UnavailableStatus,
                    StringComparison.Ordinal) && !hasValidValue;
        }

        private static bool IsFiniteNonNegative(double? value)
        {
            return value.HasValue && double.IsFinite(value.Value) && value.Value >= 0;
        }
    }
}
