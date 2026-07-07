using System.Globalization;

namespace NextGenEmby.Core.PlaybackQuality
{
    public static class PlaybackQualityEvaluator
    {
        private const double FrameRateTolerance = 0.01;
        private const double TicksPerMillisecond = 10000.0;

        public static void Evaluate(PlaybackQualityReport report)
        {
            report.FailureReasons.Clear();
            report.Checks.Clear();
            report.Analysis = new PlaybackQualityAnalysis();

            if (report.Expected == null)
            {
                report.Result = "observed";
                report.Analysis.PrimaryFailureArea = "none";
                report.Analysis.SuggestedNextAction = "No thresholds supplied; inspect raw metrics only.";
                report.Analysis.IgnoredSignals.Add("expected.* thresholds");
                return;
            }

            var expected = report.Expected;
            CheckExpectedSourceMetadata(report, expected);
            CheckExpectedFrameRate(report, expected);
            CheckStartupDuration(report, expected);

            if (IsExpectedUnsupportedSource(expected))
            {
                AssignFailureClasses(report);
                if (report.FailureReasons.Count == 0)
                {
                    report.Result = "unsupported";
                    report.Analysis.PrimaryFailureArea = "unsupported-source";
                    report.Analysis.SuggestedNextAction =
                        "Source is expected to be unsupported; preserve source classification evidence and skip playback-quality thresholds.";
                    return;
                }

                report.Result = "fail";
                AssignFailureAnalysis(report);
                return;
            }

            CheckSeekPositionError(report, expected);
            CheckMin(
                report,
                "RenderedVideoFrames",
                (long)report.Timing.RenderedVideoFrames,
                expected.MinRenderedVideoFrames,
                "MinRenderedVideoFrames",
                "timing.renderedVideoFrames",
                "frame-pacing");
            CheckMax(
                report,
                "DroppedVideoFrames",
                (long)report.Timing.DroppedVideoFrames,
                expected.MaxDroppedFrames,
                "MaxDroppedFrames",
                "timing.droppedVideoFrames",
                "frame-pacing");
            CheckMeasuredMax(
                report,
                "MaxFrameGapMs",
                report.Timing.MaxFrameGapMs,
                expected.MaxFrameGapMs,
                "MaxFrameGapMs",
                "timing.maxFrameGapMs",
                "frame-pacing");
            CheckMeasuredMax(
                report,
                "RenderIntervalMsP95",
                report.Timing.RenderIntervalMsP95,
                expected.MaxRenderIntervalMsP95,
                "MaxRenderIntervalMsP95",
                "timing.renderIntervalMsP95",
                "frame-pacing");
            CheckMeasuredMax(
                report,
                "RenderIntervalMsP99",
                report.Timing.RenderIntervalMsP99,
                expected.MaxRenderIntervalMsP99,
                "MaxRenderIntervalMsP99",
                "timing.renderIntervalMsP99",
                "frame-pacing");
            CheckMeasuredMax(
                report,
                "AudioVideoDriftMsP95",
                report.Sync.AudioVideoDriftMsP95,
                expected.MaxAudioVideoDriftMsP95,
                "MaxAudioVideoDriftMsP95",
                "sync.audioVideoDriftMsP95",
                "av-sync");
            CheckMax(
                report,
                "VideoStarvedPasses",
                (long)report.Buffers.VideoStarvedPasses,
                expected.MaxVideoStarvedPasses,
                "MaxVideoStarvedPasses",
                "buffers.videoStarvedPasses",
                "buffering");
            CheckMax(
                report,
                "AudioStarvedPasses",
                (long)report.Buffers.AudioStarvedPasses,
                expected.MaxAudioStarvedPasses,
                "MaxAudioStarvedPasses",
                "buffers.audioStarvedPasses",
                "buffering");
            CheckRequiredEquals(
                report,
                "ActualHdrOutput",
                report.ColorPipeline.ActualHdrOutput,
                expected.HdrOutput,
                "colorPipeline.actualHdrOutput",
                "color-pipeline");
            CheckRequiredEquals(
                report,
                "DxgiInput",
                report.ColorPipeline.DxgiInput,
                expected.DxgiInput,
                "colorPipeline.dxgiInput",
                "color-pipeline");
            CheckRequiredEquals(
                report,
                "DxgiOutput",
                report.ColorPipeline.DxgiOutput,
                expected.DxgiOutput,
                "colorPipeline.dxgiOutput",
                "color-pipeline");
            CheckExpectedTenBitSwapChain(report, expected);
            CheckMatchedRefreshRate(report, expected);

            if (expected.RequireValidatedConversion &&
                string.IsNullOrWhiteSpace(report.ColorPipeline.ConversionStatus))
            {
                var message = "ConversionStatus is missing for color-pipeline validation.";
                report.FailureReasons.Add(message);
                report.Checks.Add(new PlaybackQualityCheck
                {
                    Name = "ConversionStatus",
                    Signal = "colorPipeline.conversionStatus",
                    Status = "fail",
                    FailureArea = "color-pipeline",
                    Expected = "validated",
                    Actual = "",
                    Message = message
                });
                AddRelevantSignal(report, "colorPipeline.conversionStatus");
            }
            else if (expected.RequireValidatedConversion &&
                report.ColorPipeline.ConversionStatus != "validated" &&
                report.ColorPipeline.ConversionStatus != "validated;tone-mapped-hable")
            {
                var message = "ConversionStatus " + report.ColorPipeline.ConversionStatus + " is not validated.";
                report.FailureReasons.Add(message);
                report.Checks.Add(new PlaybackQualityCheck
                {
                    Name = "ConversionStatus",
                    Signal = "colorPipeline.conversionStatus",
                    Status = "fail",
                    FailureArea = "color-pipeline",
                    Expected = "validated",
                    Actual = report.ColorPipeline.ConversionStatus,
                    Message = message
                });
                AddRelevantSignal(report, "colorPipeline.conversionStatus");
            }
            else if (expected.RequireValidatedConversion)
            {
                report.Checks.Add(new PlaybackQualityCheck
                {
                    Name = "ConversionStatus",
                    Signal = "colorPipeline.conversionStatus",
                    Status = "pass",
                    FailureArea = "color-pipeline",
                    Expected = "validated",
                    Actual = report.ColorPipeline.ConversionStatus,
                    Message = "ConversionStatus is validated."
                });
            }

            AssignFailureClasses(report);
            report.Result = report.FailureReasons.Count == 0 ? "pass" : "fail";
            if (report.Result == "pass")
            {
                report.Analysis.PrimaryFailureArea = "none";
                report.Analysis.SuggestedNextAction = "No failing thresholds.";
                return;
            }

            AssignFailureAnalysis(report);
        }

        private static void CheckExpectedSourceMetadata(
            PlaybackQualityReport report,
            PlaybackQualityExpected expected)
        {
            CheckExpectedString(
                report,
                "ExpectedCodec",
                report.Source.Codec,
                expected.Codec,
                "source.codec",
                ignoreCase: true);
            CheckExpectedInt(
                report,
                "ExpectedWidth",
                report.Source.Width,
                expected.Width,
                "source.width");
            CheckExpectedInt(
                report,
                "ExpectedHeight",
                report.Source.Height,
                expected.Height,
                "source.height");
            CheckExpectedString(
                report,
                "ExpectedHdrKind",
                report.Source.HdrKind,
                expected.HdrKind,
                "source.hdrKind",
                ignoreCase: false);
            CheckExpectedString(
                report,
                "ExpectedHdrPlaybackStrategy",
                report.Source.HdrPlaybackStrategy,
                expected.HdrPlaybackStrategy,
                "source.hdrPlaybackStrategy",
                ignoreCase: false);
            CheckExpectedBool(
                report,
                "ExpectedIsHdr",
                report.Source.IsHdr,
                expected.IsHdr,
                "source.isHdr");
            CheckExpectedBool(
                report,
                "ExpectedIsDirectPlayable",
                report.Source.IsDirectPlayable,
                expected.IsDirectPlayable,
                "source.isDirectPlayable");
            CheckExpectedBool(
                report,
                "ExpectedIsDolbyVision",
                report.Source.IsDolbyVision,
                expected.IsDolbyVision,
                "source.isDolbyVision");
            CheckExpectedNullableInt(
                report,
                "ExpectedDolbyVisionProfile",
                report.Source.DolbyVisionProfile,
                expected.DolbyVisionProfile,
                "source.dolbyVisionProfile");
            CheckExpectedNullableInt(
                report,
                "ExpectedDolbyVisionCompatibilityId",
                report.Source.DolbyVisionCompatibilityId,
                expected.DolbyVisionCompatibilityId,
                "source.dolbyVisionCompatibilityId");
            CheckExpectedBool(
                report,
                "ExpectedHasHdr10BaseLayer",
                report.Source.HasHdr10BaseLayer,
                expected.HasHdr10BaseLayer,
                "source.hasHdr10BaseLayer");
            CheckExpectedBool(
                report,
                "ExpectedHasHlgBaseLayer",
                report.Source.HasHlgBaseLayer,
                expected.HasHlgBaseLayer,
                "source.hasHlgBaseLayer");
        }

        private static void CheckExpectedString(
            PlaybackQualityReport report,
            string name,
            string actual,
            string expected,
            string signal,
            bool ignoreCase)
        {
            if (string.IsNullOrWhiteSpace(expected))
            {
                return;
            }

            var comparison = ignoreCase
                ? System.StringComparison.OrdinalIgnoreCase
                : System.StringComparison.Ordinal;
            var failed = string.IsNullOrWhiteSpace(actual) ||
                !string.Equals(actual, expected, comparison);
            var message = name + " " + expected + " did not match " + signal + " " + actual + ".";
            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = name,
                Signal = signal,
                Status = failed ? "fail" : "pass",
                FailureArea = "unsupported-source",
                Expected = expected,
                Actual = actual,
                Message = failed ? message : signal + " matched expected " + expected + "."
            });

            if (failed)
            {
                report.FailureReasons.Add(message);
                AddRelevantSignal(report, signal);
            }
        }

        private static void CheckExpectedInt(
            PlaybackQualityReport report,
            string name,
            int actual,
            int expected,
            string signal)
        {
            if (expected <= 0)
            {
                return;
            }

            var failed = actual != expected;
            var message = name + " " + expected + " did not match " + signal + " " + actual + ".";
            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = name,
                Signal = signal,
                Status = failed ? "fail" : "pass",
                FailureArea = "unsupported-source",
                Expected = expected.ToString(CultureInfo.InvariantCulture),
                Actual = actual.ToString(CultureInfo.InvariantCulture),
                Message = failed ? message : signal + " matched expected " + expected + "."
            });

            if (failed)
            {
                report.FailureReasons.Add(message);
                AddRelevantSignal(report, signal);
            }
        }

        private static void CheckExpectedNullableInt(
            PlaybackQualityReport report,
            string name,
            int? actual,
            int? expected,
            string signal)
        {
            if (!expected.HasValue)
            {
                return;
            }

            var actualText = actual.HasValue
                ? actual.Value.ToString(CultureInfo.InvariantCulture)
                : "";
            var expectedText = expected.Value.ToString(CultureInfo.InvariantCulture);
            var failed = !actual.HasValue || actual.Value != expected.Value;
            var message = name + " " + expectedText + " did not match " + signal + " " + actualText + ".";
            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = name,
                Signal = signal,
                Status = failed ? "fail" : "pass",
                FailureArea = "unsupported-source",
                Expected = expectedText,
                Actual = actualText,
                Message = failed ? message : signal + " matched expected " + expectedText + "."
            });

            if (failed)
            {
                report.FailureReasons.Add(message);
                AddRelevantSignal(report, signal);
            }
        }

        private static void CheckExpectedBool(
            PlaybackQualityReport report,
            string name,
            bool actual,
            bool? expected,
            string signal)
        {
            if (!expected.HasValue)
            {
                return;
            }

            var expectedText = expected.Value.ToString();
            var actualText = actual.ToString();
            var failed = actual != expected.Value;
            var message = name + " " + expectedText + " did not match " + signal + " " + actualText + ".";
            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = name,
                Signal = signal,
                Status = failed ? "fail" : "pass",
                FailureArea = "unsupported-source",
                Expected = expectedText,
                Actual = actualText,
                Message = failed ? message : signal + " matched expected " + expectedText + "."
            });

            if (failed)
            {
                report.FailureReasons.Add(message);
                AddRelevantSignal(report, signal);
            }
        }

        private static void CheckMatchedRefreshRate(
            PlaybackQualityReport report,
            PlaybackQualityExpected expected)
        {
            if (!expected.RequireMatchedDisplayRefreshRate)
            {
                return;
            }

            if (!PlaybackRefreshRatePolicy.HasUsableVideoFrameRate(report.Source.FrameRate))
            {
                var sourceFrameRateMessage = "SourceFrameRate " + Format(report.Source.FrameRate) + " is not usable for display refresh validation.";
                report.FailureReasons.Add(sourceFrameRateMessage);
                report.Checks.Add(new PlaybackQualityCheck
                {
                    Name = "SourceFrameRate",
                    Signal = "source.frameRate",
                    Status = "fail",
                    FailureArea = "frame-pacing",
                    Expected = "usable source.frameRate",
                    Actual = Format(report.Source.FrameRate),
                    Message = sourceFrameRateMessage
                });
                AddRelevantSignal(report, "source.frameRate");
                return;
            }

            if (report.Display.RefreshRateHz <= 0)
            {
                var missingRefreshMessage = "DisplayRefreshRateHz is missing for display refresh validation.";
                report.FailureReasons.Add(missingRefreshMessage);
                report.Checks.Add(new PlaybackQualityCheck
                {
                    Name = "DisplayRefreshRateHz",
                    Signal = "display.refreshRateHz",
                    Status = "fail",
                    FailureArea = "frame-pacing",
                    Expected = "matched to source.frameRate " + Format(report.Source.FrameRate),
                    Actual = "",
                    Message = missingRefreshMessage
                });
                AddRelevantSignal(report, "display.refreshRateHz");
                return;
            }

            var failed = !PlaybackRefreshRatePolicy.MatchesVideoFrameRate(
                report.Display.RefreshRateHz,
                report.Source.FrameRate);
            var expectedMessage = "matched to source.frameRate " + Format(report.Source.FrameRate);
            var actual = Format(report.Display.RefreshRateHz);
            var mismatchMessage = "DisplayRefreshRateHz " + actual + " does not match source frame rate " + Format(report.Source.FrameRate) + ".";
            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = "DisplayRefreshRateHz",
                Signal = "display.refreshRateHz",
                Status = failed ? "fail" : "pass",
                FailureArea = "frame-pacing",
                Expected = expectedMessage,
                Actual = actual,
                Message = failed ? mismatchMessage : "DisplayRefreshRateHz matches source frame rate."
            });

            if (failed)
            {
                report.FailureReasons.Add(mismatchMessage);
                AddRelevantSignal(report, "display.refreshRateHz");
            }
        }

        private static void CheckExpectedTenBitSwapChain(
            PlaybackQualityReport report,
            PlaybackQualityExpected expected)
        {
            if (!PlaybackQualityColorExpectationPolicy.RequiresTenBitSwapChain(expected))
            {
                return;
            }

            const string expectedText = "True";
            var actualText = report.ColorPipeline.IsTenBitSwapChain.ToString();
            var failed = !report.ColorPipeline.IsTenBitSwapChain;
            var message = "IsTenBitSwapChain " + actualText + " did not match expected " + expectedText + ".";
            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = "IsTenBitSwapChain",
                Signal = "colorPipeline.isTenBitSwapChain",
                Status = failed ? "fail" : "pass",
                FailureArea = "color-pipeline",
                Expected = expectedText,
                Actual = actualText,
                Message = failed
                    ? message
                    : "IsTenBitSwapChain matched expected " + expectedText + "."
            });

            if (failed)
            {
                report.FailureReasons.Add(message);
                AddRelevantSignal(report, "colorPipeline.isTenBitSwapChain");
            }
        }

        private static void CheckStartupDuration(
            PlaybackQualityReport report,
            PlaybackQualityExpected expected)
        {
            if (!expected.MaxStartupDurationMs.HasValue)
            {
                return;
            }

            if (report.Startup.StartupDurationMs <= 0)
            {
                var message = "StartupDurationMs is missing for startup validation.";
                report.FailureReasons.Add(message);
                report.Checks.Add(new PlaybackQualityCheck
                {
                    Name = "StartupDurationMs",
                    Signal = "startup.startupDurationMs",
                    Status = "fail",
                    FailureArea = "startup",
                    Expected = Format(expected.MaxStartupDurationMs.Value),
                    Actual = "",
                    Message = message
                });
                AddRelevantSignal(report, "startup.startupDurationMs");
                return;
            }

            CheckMax(
                report,
                "StartupDurationMs",
                report.Startup.StartupDurationMs,
                expected.MaxStartupDurationMs,
                "MaxStartupDurationMs",
                "startup.startupDurationMs",
                "startup");
        }

        private static void CheckExpectedFrameRate(
            PlaybackQualityReport report,
            PlaybackQualityExpected expected)
        {
            if (expected.FrameRate <= 0)
            {
                return;
            }

            var actual = report.Source.FrameRate;
            var expectedText = Format(expected.FrameRate);
            var actualText = Format(actual);
            var failed = actual <= 0 ||
                System.Math.Abs(actual - expected.FrameRate) > FrameRateTolerance;
            var message = "ExpectedFrameRate " + expectedText +
                " did not match source frame rate " + actualText + ".";

            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = "ExpectedFrameRate",
                Signal = "source.frameRate",
                Status = failed ? "fail" : "pass",
                FailureArea = "unsupported-source",
                Expected = expectedText,
                Actual = actualText,
                Message = failed
                    ? message
                    : "Source frame rate matched expected FrameRate " + expectedText + "."
            });

            if (failed)
            {
                report.FailureReasons.Add(message);
                AddRelevantSignal(report, "source.frameRate");
            }
        }

        private static void CheckMeasuredMax(
            PlaybackQualityReport report,
            string metricName,
            double actual,
            double? max,
            string thresholdName,
            string signal,
            string failureArea)
        {
            if (!max.HasValue)
            {
                return;
            }

            if (actual <= 0)
            {
                var message = metricName + " is missing for " + failureArea + " validation.";
                report.FailureReasons.Add(message);
                report.Checks.Add(new PlaybackQualityCheck
                {
                    Name = metricName,
                    Signal = signal,
                    Status = "fail",
                    FailureArea = failureArea,
                    Expected = Format(max.Value),
                    Actual = "",
                    Message = message
                });
                AddRelevantSignal(report, signal);
                return;
            }

            CheckMax(
                report,
                metricName,
                actual,
                max,
                thresholdName,
                signal,
                failureArea);
        }

        private static void CheckMax(
            PlaybackQualityReport report,
            string metricName,
            long actual,
            long? max,
            string thresholdName,
            string signal,
            string failureArea)
        {
            if (!max.HasValue)
            {
                return;
            }

            var message = metricName + " " + actual + " exceeded " + thresholdName + " " + max.Value + ".";
            var failed = actual > max.Value;
            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = metricName,
                Signal = signal,
                Status = failed ? "fail" : "pass",
                FailureArea = failureArea,
                Expected = max.Value.ToString(CultureInfo.InvariantCulture),
                Actual = actual.ToString(CultureInfo.InvariantCulture),
                Message = failed ? message : metricName + " is within " + thresholdName + "."
            });

            if (failed)
            {
                report.FailureReasons.Add(message);
                AddRelevantSignal(report, signal);
            }
        }

        private static void CheckMin(
            PlaybackQualityReport report,
            string metricName,
            long actual,
            long? min,
            string thresholdName,
            string signal,
            string failureArea)
        {
            if (!min.HasValue)
            {
                return;
            }

            var message = metricName + " " + actual + " was below " + thresholdName + " " + min.Value + ".";
            var failed = actual < min.Value;
            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = metricName,
                Signal = signal,
                Status = failed ? "fail" : "pass",
                FailureArea = failureArea,
                Expected = min.Value.ToString(CultureInfo.InvariantCulture),
                Actual = actual.ToString(CultureInfo.InvariantCulture),
                Message = failed ? message : metricName + " met " + thresholdName + "."
            });

            if (failed)
            {
                report.FailureReasons.Add(message);
                AddRelevantSignal(report, signal);
            }
        }

        private static void CheckMax(
            PlaybackQualityReport report,
            string metricName,
            double actual,
            double? max,
            string thresholdName,
            string signal,
            string failureArea)
        {
            if (!max.HasValue)
            {
                return;
            }

            var formattedActual = Format(actual);
            var formattedMax = Format(max.Value);
            var message = metricName + " " + formattedActual + " exceeded " + thresholdName + " " + formattedMax + ".";
            var failed = actual > max.Value;
            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = metricName,
                Signal = signal,
                Status = failed ? "fail" : "pass",
                FailureArea = failureArea,
                Expected = formattedMax,
                Actual = formattedActual,
                Message = failed ? message : metricName + " is within " + thresholdName + "."
            });

            if (failed)
            {
                report.FailureReasons.Add(message);
                AddRelevantSignal(report, signal);
            }
        }

        private static void CheckRequiredEquals(
            PlaybackQualityReport report,
            string name,
            string actual,
            string expected,
            string signal,
            string failureArea)
        {
            if (string.IsNullOrWhiteSpace(expected))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(actual))
            {
                var message = name + " is missing for " + failureArea + " validation.";
                report.FailureReasons.Add(message);
                report.Checks.Add(new PlaybackQualityCheck
                {
                    Name = name,
                    Signal = signal,
                    Status = "fail",
                    FailureArea = failureArea,
                    Expected = expected,
                    Actual = "",
                    Message = message
                });
                AddRelevantSignal(report, signal);
                return;
            }

            CheckEquals(report, name, actual, expected, signal, failureArea);
        }

        private static void CheckEquals(
            PlaybackQualityReport report,
            string name,
            string actual,
            string expected,
            string signal,
            string failureArea)
        {
            if (string.IsNullOrWhiteSpace(expected))
            {
                return;
            }

            var failed = !string.Equals(actual, expected, System.StringComparison.Ordinal);
            var message = name + " " + actual + " did not match expected " + expected + ".";
            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = name,
                Signal = signal,
                Status = failed ? "fail" : "pass",
                FailureArea = failureArea,
                Expected = expected,
                Actual = actual,
                Message = failed ? message : name + " matched expected " + expected + "."
            });

            if (failed)
            {
                report.FailureReasons.Add(message);
                AddRelevantSignal(report, signal);
            }
        }

        private static void AssignFailureAnalysis(PlaybackQualityReport report)
        {
            if (HasReason(
                report,
                "unsupported",
                "ExpectedFrameRate",
                "ExpectedCodec",
                "ExpectedWidth",
                "ExpectedHeight",
                "ExpectedHdrKind",
                "ExpectedHdrPlaybackStrategy",
                "ExpectedIsHdr",
                "ExpectedIsDirectPlayable",
                "ExpectedIsDolbyVision",
                "ExpectedDolbyVisionProfile",
                "ExpectedDolbyVisionCompatibilityId",
                "ExpectedHasHdr10BaseLayer",
                "ExpectedHasHlgBaseLayer"))
            {
                report.Analysis.PrimaryFailureArea = "unsupported-source";
                report.Analysis.SuggestedNextAction = "Inspect container, codec, Dolby Vision profile, and media source selection.";
                return;
            }

            if (HasReason(report, "ActualHdrOutput", "DxgiInput", "DxgiOutput", "IsTenBitSwapChain", "ConversionStatus"))
            {
                report.Analysis.PrimaryFailureArea = "color-pipeline";
                report.Analysis.SuggestedNextAction = "Inspect HDR display switch and DXGI color-space mapping.";
                return;
            }

            if (HasReason(report, "StartupDurationMs"))
            {
                report.Analysis.PrimaryFailureArea = "startup";
                report.Analysis.SuggestedNextAction = "Inspect Emby request, source open, demux initialization, and first-frame readiness.";
                return;
            }

            if (HasReason(report, "SeekPositionErrorMs"))
            {
                report.Analysis.PrimaryFailureArea = "timeline";
                report.Analysis.SuggestedNextAction = "Inspect seek/resume timeline state, demux seek completion, and playback position reporting.";
                return;
            }

            if (HasReason(report, "VideoStarvedPasses", "AudioStarvedPasses"))
            {
                report.Analysis.PrimaryFailureArea = "buffering";
                report.Analysis.SuggestedNextAction = "Inspect demux/network stalls before changing render pacing.";
                return;
            }

            if (HasReason(report, "AudioVideoDriftMsP95"))
            {
                report.Analysis.PrimaryFailureArea = "av-sync";
                report.Analysis.SuggestedNextAction = "Inspect audio renderer clock and queued buffer depth.";
                return;
            }

            if (HasReason(report, "DroppedVideoFrames", "MaxFrameGapMs", "RenderIntervalMs", "RenderedVideoFrames", "DisplayRefreshRateHz", "SourceFrameRate"))
            {
                report.Analysis.PrimaryFailureArea = "frame-pacing";
                report.Analysis.SuggestedNextAction = "Inspect frame pacing wait/drop thresholds around PlaybackFramePacing.";
                return;
            }

            report.Analysis.PrimaryFailureArea = "unknown";
            report.Analysis.SuggestedNextAction = "Inspect raw metrics and failure reasons.";
        }

        private static bool IsExpectedUnsupportedSource(PlaybackQualityExpected expected)
        {
            return expected != null &&
                ((expected.IsDirectPlayable.HasValue && !expected.IsDirectPlayable.Value) ||
                string.Equals(
                    expected.HdrKind,
                    "DolbyVisionUnsupported",
                    System.StringComparison.Ordinal));
        }

        private static void AssignFailureClasses(PlaybackQualityReport report)
        {
            foreach (var check in report.Checks)
            {
                if (check.Status == "fail" &&
                    string.IsNullOrWhiteSpace(check.FailureClass))
                {
                    check.FailureClass =
                        PlaybackQualityFailureClassification.Classify(check);
                }
            }
        }

        private static void CheckSeekPositionError(
            PlaybackQualityReport report,
            PlaybackQualityExpected expected)
        {
            if (!expected.MaxSeekPositionErrorMs.HasValue)
            {
                return;
            }

            var errorMs = ResolveSeekPositionErrorMs(report.Position);
            if (!errorMs.HasValue)
            {
                var message = "SeekPositionErrorMs is missing for timeline validation.";
                report.FailureReasons.Add(message);
                report.Checks.Add(new PlaybackQualityCheck
                {
                    Name = "SeekPositionErrorMs",
                    Signal = "position.seekPositionErrorMs",
                    Status = "fail",
                    FailureArea = "timeline",
                    Expected = Format(expected.MaxSeekPositionErrorMs.Value),
                    Actual = "",
                    Message = message
                });
                AddRelevantSignal(report, "position.seekPositionErrorMs");
                return;
            }

            report.Position.SeekPositionErrorMs = errorMs.Value;
            CheckMax(
                report,
                "SeekPositionErrorMs",
                errorMs.Value,
                expected.MaxSeekPositionErrorMs,
                "MaxSeekPositionErrorMs",
                "position.seekPositionErrorMs",
                "timeline");
        }

        private static double? ResolveSeekPositionErrorMs(PlaybackQualityPosition position)
        {
            if (position.SeekPositionErrorMs.HasValue)
            {
                return System.Math.Abs(position.SeekPositionErrorMs.Value);
            }

            if (position.SeekTargetPositionTicks.HasValue &&
                position.ActualPositionTicks.HasValue)
            {
                return System.Math.Abs(
                    position.ActualPositionTicks.Value - position.SeekTargetPositionTicks.Value) /
                    TicksPerMillisecond;
            }

            return null;
        }

        private static bool HasReason(PlaybackQualityReport report, params string[] fragments)
        {
            foreach (var reason in report.FailureReasons)
            {
                foreach (var fragment in fragments)
                {
                    if (reason.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AddRelevantSignal(PlaybackQualityReport report, string signal)
        {
            if (!report.Analysis.RelevantSignals.Contains(signal))
            {
                report.Analysis.RelevantSignals.Add(signal);
            }
        }

        private static string Format(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }
    }
}
