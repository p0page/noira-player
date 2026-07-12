using System.Globalization;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualityEvaluator
    {
        private const double FrameRateTolerance = 0.01;
        private const double MinimumRenderCadenceIntervalRatio = 0.75;
        private const double TicksPerMillisecond = 10000.0;

        public static void Evaluate(PlaybackQualityReport report)
        {
            report.FailureReasons.Clear();
            report.Checks.Clear();
            report.Analysis = new PlaybackQualityAnalysis();
            CheckFailedLifecycleOperations(report);

            if (report.Expected == null)
            {
                if (report.FailureReasons.Count != 0)
                {
                    AssignFailureClasses(report);
                    report.Result = "fail";
                    AssignFailureAnalysis(report);
                    return;
                }

                report.Result = "observed";
                report.Analysis.PrimaryFailureArea = "none";
                report.Analysis.SuggestedNextAction = "No thresholds supplied; inspect raw metrics only.";
                report.Analysis.IgnoredSignals.Add("expected.* thresholds");
                return;
            }

            var expected = report.Expected;
            CheckExpectedSourceMetadata(report, expected);
            CheckExpectedFrameRate(report, expected);

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

            CheckStartupDuration(report, expected);
            CheckInteractionRecoveryDuration(report, expected);
            CheckSeekEvidenceCompleteness(report);
            CheckSeekPositionError(report, expected);
            CheckSeekRecoveryDuration(report, expected);
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
            CheckMinimumRenderCadence(report, expected);
            if (!HasKnownVideoOnlyTrackLayout(report))
            {
                CheckMeasuredMax(
                    report,
                    "AudioVideoDriftMsP95",
                    report.Sync.AudioVideoDriftMsP95,
                    expected.MaxAudioVideoDriftMsP95,
                    "MaxAudioVideoDriftMsP95",
                    "sync.audioVideoDriftMsP95",
                    "av-sync");
            }
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
            var fallbackRequired = RequiresSdrDisplayFallback(report, expected);
            var fallback = fallbackRequired ? expected.SdrDisplayFallback : null;
            report.ColorPipeline.ExpectationProfile = fallbackRequired
                ? "sdr-display-fallback"
                : "primary";
            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = "ColorExpectationProfile",
                Signal = "colorPipeline.expectationProfile",
                Status = "observed",
                FailureArea = "color-pipeline",
                Expected = "environment-selected",
                Actual = report.ColorPipeline.ExpectationProfile,
                Message = "Color expectation profile selected from explicit display evidence."
            });

            if (fallbackRequired && fallback == null)
            {
                const string message =
                    "SDR display fallback is required but expected.sdrDisplayFallback is missing.";
                report.FailureReasons.Add(message);
                report.Checks.Add(new PlaybackQualityCheck
                {
                    Name = "SdrDisplayFallback",
                    Signal = "expected.sdrDisplayFallback",
                    Status = "fail",
                    FailureArea = "evidence-collection",
                    Expected = "declared",
                    Actual = "missing",
                    Message = message
                });
                AddRelevantSignal(report, "colorPipeline.expectationProfile");
            }

            var expectedHdrOutput = fallback?.HdrOutput ?? expected.HdrOutput;
            var expectedDxgiOutput = fallback?.DxgiOutput ?? expected.DxgiOutput;
            CheckRequiredEquals(
                report,
                "ActualHdrOutput",
                report.ColorPipeline.ActualHdrOutput,
                expectedHdrOutput,
                "colorPipeline.actualHdrOutput",
                "color-pipeline");
            if (fallback != null)
            {
                CheckRequiredAnyOf(
                    report,
                    "DxgiInput",
                    report.ColorPipeline.DxgiInput,
                    fallback.DxgiInputAnyOf,
                    "colorPipeline.dxgiInput",
                    "color-pipeline");
            }
            else
            {
                CheckRequiredEquals(
                    report,
                    "DxgiInput",
                    report.ColorPipeline.DxgiInput,
                    expected.DxgiInput,
                    "colorPipeline.dxgiInput",
                    "color-pipeline");
            }
            CheckRequiredEquals(
                report,
                "DxgiOutput",
                report.ColorPipeline.DxgiOutput,
                expectedDxgiOutput,
                "colorPipeline.dxgiOutput",
                "color-pipeline");
            CheckExpectedTenBitSwapChain(
                report,
                fallback?.IsTenBitSwapChain ??
                    (PlaybackQualityColorExpectationPolicy.RequiresTenBitSwapChain(expected) ? true : null));
            CheckMatchedRefreshRate(report, expected);

            var requireValidatedConversion =
                fallback?.RequireValidatedConversion ?? expected.RequireValidatedConversion;
            var requiredConversionToken = fallback?.RequiredConversionStatus ?? "";
            if (requireValidatedConversion &&
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
            else if (requireValidatedConversion &&
                !HasRequiredConversionStatus(
                    report.ColorPipeline.ConversionStatus,
                    requiredConversionToken))
            {
                var message = string.IsNullOrWhiteSpace(requiredConversionToken)
                    ? "ConversionStatus " + report.ColorPipeline.ConversionStatus + " is not validated."
                    : "ConversionStatus requires " + requiredConversionToken +
                        " token " + requiredConversionToken + ".";
                report.FailureReasons.Add(message);
                report.Checks.Add(new PlaybackQualityCheck
                {
                    Name = "ConversionStatus",
                    Signal = "colorPipeline.conversionStatus",
                    Status = "fail",
                    FailureArea = "color-pipeline",
                    Expected = string.IsNullOrWhiteSpace(requiredConversionToken)
                        ? "validated"
                        : requiredConversionToken,
                    Actual = report.ColorPipeline.ConversionStatus,
                    Message = message
                });
                AddRelevantSignal(report, "colorPipeline.conversionStatus");
            }
            else if (requireValidatedConversion)
            {
                report.Checks.Add(new PlaybackQualityCheck
                {
                    Name = "ConversionStatus",
                    Signal = "colorPipeline.conversionStatus",
                    Status = "pass",
                    FailureArea = "color-pipeline",
                    Expected = string.IsNullOrWhiteSpace(requiredConversionToken)
                        ? "validated"
                        : requiredConversionToken,
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

        private static void CheckFailedLifecycleOperations(PlaybackQualityReport report)
        {
            foreach (var lifecycleEvent in report.Lifecycle.Events)
            {
                if (!string.Equals(
                        lifecycleEvent.Status,
                        "failed",
                        System.StringComparison.Ordinal) &&
                    !string.Equals(
                        lifecycleEvent.Status,
                        "error",
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                var signal = "lifecycle." + lifecycleEvent.Operation;
                var message = string.IsNullOrWhiteSpace(lifecycleEvent.Message)
                    ? "Lifecycle operation " + lifecycleEvent.Operation +
                        " reported " + lifecycleEvent.Status + "."
                    : lifecycleEvent.Message;
                report.FailureReasons.Add(message);
                report.Checks.Add(new PlaybackQualityCheck
                {
                    Name = "LifecycleOperation",
                    Signal = signal,
                    Status = "fail",
                    FailureArea = GetLifecycleFailureArea(lifecycleEvent.Operation),
                    Expected = "completed",
                    Actual = lifecycleEvent.Status,
                    Message = message
                });
                AddRelevantSignal(report, signal);
            }
        }

        private static string GetLifecycleFailureArea(string operation) => operation switch
        {
            "audio-switch" => "tracks",
            "subtitle-switch" or "subtitle-off" => "subtitles",
            "seek" => "timeline",
            _ => "playback-lifecycle"
        };

        private static void CheckInteractionRecoveryDuration(
            PlaybackQualityReport report,
            PlaybackQualityExpected expected)
        {
            if (!expected.MaxInteractionRecoveryDurationMs.HasValue)
            {
                return;
            }

            var signal = "interaction.recoveryDurationMs";
            var failureArea = GetLifecycleFailureArea(report.Interaction.Scenario);
            if (!report.Interaction.Attempted ||
                !report.Interaction.RecoveryDurationMs.HasValue ||
                !double.IsFinite(report.Interaction.RecoveryDurationMs.Value) ||
                report.Interaction.RecoveryDurationMs.Value < 0)
            {
                const string message =
                    "InteractionRecoveryDurationMs is missing for interaction recovery validation.";
                report.FailureReasons.Add(message);
                report.Checks.Add(new PlaybackQualityCheck
                {
                    Name = "InteractionRecoveryDurationMs",
                    Signal = signal,
                    Status = "fail",
                    FailureArea = failureArea,
                    FailureClass = PlaybackQualityFailureClassification.InsufficientInstrumentation,
                    Expected = Format(expected.MaxInteractionRecoveryDurationMs.Value),
                    Actual = "",
                    Message = message
                });
                AddRelevantSignal(report, signal);
                return;
            }

            CheckMeasuredMax(
                report,
                "InteractionRecoveryDurationMs",
                report.Interaction.RecoveryDurationMs.Value,
                expected.MaxInteractionRecoveryDurationMs,
                "MaxInteractionRecoveryDurationMs",
                signal,
                failureArea);
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
                "ExpectedVideoRange",
                report.Source.VideoRange,
                expected.VideoRange,
                "source.videoRange",
                ignoreCase: false);
            CheckExpectedString(
                report,
                "ExpectedColorPrimaries",
                report.Source.ColorPrimaries,
                expected.ColorPrimaries,
                "source.colorPrimaries",
                ignoreCase: false);
            CheckExpectedString(
                report,
                "ExpectedColorTransfer",
                report.Source.ColorTransfer,
                expected.ColorTransfer,
                "source.colorTransfer",
                ignoreCase: false);
            CheckExpectedString(
                report,
                "ExpectedColorSpace",
                report.Source.ColorSpace,
                expected.ColorSpace,
                "source.colorSpace",
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

        private static void CheckMinimumRenderCadence(
            PlaybackQualityReport report,
            PlaybackQualityExpected expected)
        {
            if (!expected.RequireMatchedDisplayRefreshRate ||
                report.Timing.RenderedVideoFrames < 2 ||
                report.Timing.ExpectedFrameDurationMs <= 0 ||
                report.Timing.RenderIntervalMsP95 <= 0 ||
                !PlaybackRefreshRatePolicy.HasUsableVideoFrameRate(report.Source.FrameRate))
            {
                return;
            }

            var minimumIntervalMs =
                report.Timing.ExpectedFrameDurationMs * MinimumRenderCadenceIntervalRatio;
            var actual = Format(report.Timing.RenderIntervalMsP95);
            var expectedText = ">= " + Format(minimumIntervalMs);
            var failed = report.Timing.RenderIntervalMsP95 < minimumIntervalMs;
            var message = "RenderIntervalMsP95 " + actual +
                " was below minimum cadence interval " + Format(minimumIntervalMs) + ".";

            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = "RenderIntervalMsP95Cadence",
                Signal = "timing.renderIntervalMsP95",
                Status = failed ? "fail" : "pass",
                FailureArea = "frame-pacing",
                Expected = expectedText,
                Actual = actual,
                Message = failed ? message : "RenderIntervalMsP95 cadence matched source frame duration."
            });

            if (failed)
            {
                report.FailureReasons.Add(message);
                AddRelevantSignal(report, "timing.renderIntervalMsP95");
            }
        }

        private static void CheckExpectedTenBitSwapChain(
            PlaybackQualityReport report,
            bool? expected)
        {
            if (!expected.HasValue)
            {
                return;
            }

            var expectedText = expected.Value.ToString();
            var actualText = report.ColorPipeline.IsTenBitSwapChain.ToString();
            var failed = report.ColorPipeline.IsTenBitSwapChain != expected.Value;
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

        private static void CheckRequiredAnyOf(
            PlaybackQualityReport report,
            string name,
            string actual,
            System.Collections.Generic.IReadOnlyList<string> expected,
            string signal,
            string failureArea)
        {
            var expectedText = string.Join(" | ", expected);
            if (string.IsNullOrWhiteSpace(actual))
            {
                var missingMessage = name + " is missing for " + failureArea + " validation.";
                report.FailureReasons.Add(missingMessage);
                report.Checks.Add(new PlaybackQualityCheck
                {
                    Name = name,
                    Signal = signal,
                    Status = "fail",
                    FailureArea = failureArea,
                    Expected = expectedText,
                    Actual = "",
                    Message = missingMessage
                });
                AddRelevantSignal(report, signal);
                return;
            }

            var matched = false;
            foreach (var value in expected)
            {
                if (string.Equals(actual, value, System.StringComparison.Ordinal))
                {
                    matched = true;
                    break;
                }
            }

            var message = name + " " + actual + " did not match any expected value: " + expectedText + ".";
            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = name,
                Signal = signal,
                Status = matched ? "pass" : "fail",
                FailureArea = failureArea,
                Expected = expectedText,
                Actual = actual,
                Message = matched ? name + " matched an allowed expected value." : message
            });
            if (!matched)
            {
                report.FailureReasons.Add(message);
                AddRelevantSignal(report, signal);
            }
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
                "ExpectedVideoRange",
                "ExpectedColorPrimaries",
                "ExpectedColorTransfer",
                "ExpectedColorSpace",
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

            if (HasReason(report, "InteractionRecoveryDurationMs"))
            {
                report.Analysis.PrimaryFailureArea =
                    GetLifecycleFailureArea(report.Interaction.Scenario);
                report.Analysis.SuggestedNextAction =
                    report.Analysis.PrimaryFailureArea == "subtitles"
                        ? "Inspect subtitle switch operation and post-switch video recovery timing."
                        : "Inspect audio decoder replacement, renderer continuity, and post-switch recovery timing.";
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

            foreach (var check in report.Checks)
            {
                if (check.Status != "fail" ||
                    check.Name != "LifecycleOperation" ||
                    string.IsNullOrWhiteSpace(check.FailureArea))
                {
                    continue;
                }

                report.Analysis.PrimaryFailureArea = check.FailureArea;
                report.Analysis.SuggestedNextAction = check.FailureArea switch
                {
                    "tracks" => "Inspect audio track selection and decoder/renderer switch continuity.",
                    "subtitles" => "Inspect subtitle stream switching, cue decode timing, and overlay presentation.",
                    "timeline" => "Inspect seek/resume timeline state and playback position reporting.",
                    _ => "Inspect playback lifecycle state transitions and post-operation progress."
                };
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

        private static void CheckSeekEvidenceCompleteness(PlaybackQualityReport report)
        {
            if (!report.Position.SeekTargetPositionTicks.HasValue ||
                report.Execution == null ||
                !PlaybackQualityEvidenceLevel.MeetsMinimum(
                    report.Execution.EvidenceLevel,
                    PlaybackQualityEvidenceLevel.NativePlayback))
            {
                return;
            }

            RequireTimelineEvidence(report, report.Source.DurationTicks > 0, "source.durationTicks");
            RequireTimelineEvidence(report, report.Source.ContainerStartTimeTicks.HasValue, "source.containerStartTimeTicks");
            RequireTimelineEvidence(report, report.Source.VideoStreamStartTimeTicks.HasValue, "source.videoStreamStartTimeTicks");
            RequireTimelineEvidence(report, report.Position.SeekDemuxTargetTicks.HasValue, "position.seekDemuxTargetTicks");
            RequireTimelineEvidence(report, report.Position.FirstPresentedPositionTicks.HasValue, "position.firstPresentedPositionTicks");
            RequireTimelineEvidence(report, report.Position.PostSeekPositionTicks.HasValue, "position.postSeekPositionTicks");
            RequireTimelineEvidence(report, report.Position.PostSeekAdvanced.HasValue, "position.postSeekAdvanced");
            RequireTimelineEvidence(report, report.Position.SeekOperationDurationMs.HasValue, "position.seekOperationDurationMs");
            RequireTimelineEvidence(report, report.Position.SeekRecoveryDurationMs.HasValue, "position.seekRecoveryDurationMs");
            RequireTimelineEvidence(report, report.Position.SeekPacketCacheEnabled.HasValue, "position.seekPacketCacheEnabled");
            RequireTimelineEvidence(report, report.Position.SeekPacketCacheHit.HasValue, "position.seekPacketCacheHit");
            RequireTimelineEvidence(report, report.Position.SeekPacketCachePacketCount.HasValue, "position.seekPacketCachePacketCount");
            RequireTimelineEvidence(report, report.Position.SeekPacketCacheBytes.HasValue, "position.seekPacketCacheBytes");
            RequireTimelineEvidence(report, report.Position.SeekPacketCacheWindowDurationTicks.HasValue, "position.seekPacketCacheWindowDurationTicks");
            RequireTimelineEvidence(report, !string.IsNullOrWhiteSpace(report.Position.SeekFallbackReason), "position.seekFallbackReason");

            if (report.Source.ContainerStartTimeTicks.HasValue &&
                report.Position.SeekDemuxTargetTicks.HasValue &&
                report.Position.SeekPacketCacheHit != true)
            {
                var origin = System.Math.Max(0, report.Source.ContainerStartTimeTicks.Value);
                var target = System.Math.Max(0, report.Position.SeekTargetPositionTicks.Value);
                var expectedDemuxTarget = target >= long.MaxValue - origin
                    ? long.MaxValue
                    : origin + target;
                if (report.Position.SeekDemuxTargetTicks.Value != expectedDemuxTarget)
                {
                    AddTimelineEvidenceFailure(
                        report,
                        "position.seekDemuxTargetTicks",
                        expectedDemuxTarget.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        report.Position.SeekDemuxTargetTicks.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        PlaybackQualityFailureClassification.PlayerCoreBug);
                }
            }

            if (report.Position.FirstPresentedPositionTicks.HasValue &&
                report.Position.ActualPositionTicks.HasValue &&
                report.Position.FirstPresentedPositionTicks.Value != report.Position.ActualPositionTicks.Value)
            {
                AddTimelineEvidenceFailure(
                    report,
                    "position.firstPresentedPositionTicks",
                    report.Position.ActualPositionTicks.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    report.Position.FirstPresentedPositionTicks.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    PlaybackQualityFailureClassification.EvaluationHarnessBug);
            }

            if (report.Position.PostSeekAdvanced.HasValue &&
                report.Position.FirstPresentedPositionTicks.HasValue &&
                report.Position.PostSeekPositionTicks.HasValue &&
                (!report.Position.PostSeekAdvanced.Value ||
                    report.Position.PostSeekPositionTicks.Value <= report.Position.FirstPresentedPositionTicks.Value))
            {
                AddTimelineEvidenceFailure(
                    report,
                    "position.postSeekAdvanced",
                    "true with post-seek position after first presented frame",
                    report.Position.PostSeekAdvanced.Value.ToString(),
                    PlaybackQualityFailureClassification.PlayerCoreBug);
            }
        }

        private static bool RequiresSdrDisplayFallback(
            PlaybackQualityReport report,
            PlaybackQualityExpected expected)
        {
            var primaryRequiresHdr =
                string.Equals(expected.HdrOutput, "Hdr10", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(expected.HdrOutput, "Hdr", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(expected.HdrOutput, "Hlg", System.StringComparison.OrdinalIgnoreCase);

            return primaryRequiresHdr &&
                (report.ColorPipeline.ForceSdrOutput ||
                (!string.IsNullOrWhiteSpace(report.Display.HdrStatus) &&
                !report.Display.IsHdrDisplayAvailable &&
                !report.Display.IsHdrOutputActive));
        }

        private static bool HasRequiredConversionStatus(string actual, string requiredToken)
        {
            if (string.IsNullOrWhiteSpace(requiredToken))
            {
                return actual == "validated" || actual == "validated;tone-mapped-hable";
            }

            foreach (var token in actual.Split(';'))
            {
                if (string.Equals(token, requiredToken, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CheckSeekRecoveryDuration(
            PlaybackQualityReport report,
            PlaybackQualityExpected expected)
        {
            if (!expected.MaxSeekRecoveryDurationMs.HasValue)
            {
                return;
            }

            CheckMeasuredMax(
                report,
                "SeekRecoveryDurationMs",
                report.Position.SeekRecoveryDurationMs.GetValueOrDefault(),
                expected.MaxSeekRecoveryDurationMs,
                "MaxSeekRecoveryDurationMs",
                "position.seekRecoveryDurationMs",
                "timeline");
        }

        private static void RequireTimelineEvidence(
            PlaybackQualityReport report,
            bool present,
            string signal)
        {
            if (!present)
            {
                AddTimelineEvidenceFailure(
                    report,
                    signal,
                    "observed native timeline evidence",
                    "missing",
                    PlaybackQualityFailureClassification.InsufficientInstrumentation);
            }
        }

        private static void AddTimelineEvidenceFailure(
            PlaybackQualityReport report,
            string signal,
            string expected,
            string actual,
            string failureClass)
        {
            var message = "Native seek evidence is incomplete or inconsistent for " + signal + ".";
            report.FailureReasons.Add(message);
            report.Checks.Add(new PlaybackQualityCheck
            {
                Name = "SeekTimelineEvidence",
                Signal = signal,
                Status = "fail",
                FailureArea = "timeline",
                FailureClass = failureClass,
                Expected = expected,
                Actual = actual,
                Message = message
            });
            AddRelevantSignal(report, signal);
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

        private static bool HasKnownVideoOnlyTrackLayout(PlaybackQualityReport report)
        {
            var videoTrackCount = report.Tracks.VideoTrackCount > 0
                ? report.Tracks.VideoTrackCount
                : report.Tracks.Video.Count;
            var audioTrackCount = report.Tracks.AudioTrackCount > 0
                ? report.Tracks.AudioTrackCount
                : report.Tracks.Audio.Count;

            return videoTrackCount > 0 && audioTrackCount == 0;
        }

        private static string Format(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }
    }
}
