using System;
using FellowOakDicom.Imaging.Codec;

namespace FellowOakDicom.PureCodecs.JpegLs.Internal
{
    public sealed class JpegLsScanCodec
    {
        private readonly int _width;
        private readonly int _height;
        private readonly int _componentCount;
        private readonly int _bitsPerSample;
        private readonly int _nearLossless;
        private readonly int _maximumSampleValue;
        private readonly JpegLsInterleaveMode _interleaveMode;

        public JpegLsScanCodec(int width, int height, int componentCount, int bitsPerSample, int nearLossless)
            : this(width, height, componentCount, bitsPerSample, nearLossless, JpegLsInterleaveMode.None)
        {
        }

        public JpegLsScanCodec(int width, int height, int componentCount, int bitsPerSample, int nearLossless, JpegLsInterleaveMode interleaveMode)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "JPEG-LS dimensions must be positive.");
            }

            if (componentCount != 1 && componentCount != 3)
            {
                throw new DicomCodecException("JPEG-LS supports only 1 or 3 components.");
            }

            if (bitsPerSample < 2 || bitsPerSample > 16)
            {
                throw new DicomCodecException($"JPEG-LS bits per sample {bitsPerSample} is not supported.");
            }

            if (nearLossless < 0 || nearLossless > 255)
            {
                throw new DicomCodecException($"JPEG-LS NEAR value {nearLossless} is not supported.");
            }

            _width = width;
            _height = height;
            _componentCount = componentCount;
            _bitsPerSample = bitsPerSample;
            _nearLossless = nearLossless;
            _maximumSampleValue = (1 << bitsPerSample) - 1;
            _interleaveMode = interleaveMode;
        }

        public byte[] Encode(int[] samples)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            if (samples.Length != _width * _height * _componentCount)
            {
                throw new DicomCodecException("JPEG-LS scan sample count does not match frame dimensions.");
            }

            var reconstructed = new int[samples.Length];
            Array.Copy(samples, reconstructed, samples.Length);
            var writer = new JpegLsGolombCodeWriter();

            var states = CreateComponentStates();
            foreach (var state in CreateProcessingOrder(states))
            {
                EncodeComponent(writer, samples, reconstructed, state);
            }

            return writer.ToArray();
        }

        public int[] Decode(byte[] scanData)
        {
            if (scanData == null)
            {
                throw new ArgumentNullException(nameof(scanData));
            }

            var reader = new JpegLsGolombCodeReader(scanData);
            var samples = new int[_width * _height * _componentCount];

            var states = CreateComponentStates();
            foreach (var state in CreateProcessingOrder(states))
            {
                DecodeComponent(reader, samples, state);
            }

            return samples;
        }

        private void EncodeComponent(JpegLsGolombCodeWriter writer, int[] original, int[] reconstructed, ProcessingState state)
        {
            for (var y = state.StartLine; y < state.EndLine; y++)
            {
                var x = 0;
                while (x < _width)
                {
                    GetNeighbors(reconstructed, state, x, y, out var left, out var above, out var aboveLeft, out var aboveRight);
                    var context = state.Model.GetContext(left, above, aboveLeft, aboveRight, out var sign, out var predicted);
                    if (IsRunMode(left, above, aboveLeft, aboveRight, state.Model.Traits))
                    {
                        x += EncodeRunMode(writer, state.Scanner, original, reconstructed, state.Component, x, y, left);
                        continue;
                    }

                    var index = GetSampleIndex(x, y, state.Component);
                    var parameter = context.GetGolombParameter();
                    var correctedPrediction = state.Model.Traits.CorrectPrediction(predicted + ApplySign(context.C, sign));
                    var errorValue = state.Model.Traits.ComputeErrorValue(ApplySign(original[index] - correctedPrediction, sign));
                    var mappedError = MapErrorValue(context.GetErrorCorrection(parameter, _nearLossless) ^ errorValue);

                    writer.EncodeMappedValue(parameter, mappedError, state.Model.Traits.Limit, state.Model.Traits.QuantizedBitsPerPixel);
                    context.Update(errorValue, _nearLossless);
                    reconstructed[index] = state.Model.Traits.ComputeReconstructedSample(correctedPrediction, ApplySign(errorValue, sign));
                    x++;
                }

                state.UpdateLineEdge(reconstructed[GetSampleIndex(0, y, state.Component)]);
            }
        }

        private void DecodeComponent(JpegLsGolombCodeReader reader, int[] samples, ProcessingState state)
        {
            for (var y = state.StartLine; y < state.EndLine; y++)
            {
                var x = 0;
                while (x < _width)
                {
                    GetNeighbors(samples, state, x, y, out var left, out var above, out var aboveLeft, out var aboveRight);
                    var context = state.Model.GetContext(left, above, aboveLeft, aboveRight, out var sign, out var predicted);
                    if (IsRunMode(left, above, aboveLeft, aboveRight, state.Model.Traits))
                    {
                        try
                        {
                            x += DecodeRunMode(reader, state.Scanner, samples, state.Component, x, y, left);
                        }
                        catch (Exception exception)
                        {
                            throw new DicomCodecException($"JPEG-LS run mode decode failed at component {state.Component}, x {x}, y {y}, run index {state.Scanner.RunIndex}.", exception);
                        }

                        continue;
                    }

                    var parameter = context.GetGolombParameter();
                    var correctedPrediction = state.Model.Traits.CorrectPrediction(predicted + ApplySign(context.C, sign));
                    var mappedError = reader.DecodeMappedValue(parameter, state.Model.Traits.Limit, state.Model.Traits.QuantizedBitsPerPixel);
                    var errorValue = UnmapErrorValue(mappedError);
                    if (parameter == 0)
                    {
                        errorValue ^= context.GetErrorCorrection(parameter, _nearLossless);
                    }

                    context.Update(errorValue, _nearLossless);

                    var sample = state.Model.Traits.ComputeReconstructedSample(correctedPrediction, ApplySign(errorValue, sign));
                    samples[GetSampleIndex(x, y, state.Component)] = sample;
                    x++;
                }

                state.UpdateLineEdge(samples[GetSampleIndex(0, y, state.Component)]);
            }
        }

        private int EncodeRunMode(
            JpegLsGolombCodeWriter writer,
            JpegLsRunModeScanner scanner,
            int[] original,
            int[] reconstructed,
            int component,
            int x,
            int y,
            int left)
        {
            var runLength = 0;
            while (x + runLength < _width)
            {
                var index = GetSampleIndex(x + runLength, y, component);
                if (Math.Abs(original[index] - left) > _nearLossless)
                {
                    break;
                }

                reconstructed[index] = left;
                runLength++;
            }

            var endOfLine = x + runLength == _width;
            scanner.EncodeRunLength(writer, runLength, endOfLine);
            if (endOfLine)
            {
                return runLength;
            }

            var interruptionX = x + runLength;
            var interruptionIndex = GetSampleIndex(interruptionX, y, component);
            var above = y > 0 ? reconstructed[GetSampleIndex(interruptionX, y - 1, component)] : 0;
            var context = Math.Abs(left - above) <= _nearLossless
                ? scanner.RunInterruptionContexts[1]
                : scanner.RunInterruptionContexts[0];
            var sign = context == scanner.RunInterruptionContexts[1] ? 1 : Sign(above - left);
            var reference = context == scanner.RunInterruptionContexts[1] ? left : above;
            var errorValue = new JpegLsTraitsWrapper(_maximumSampleValue, _nearLossless).ComputeErrorValue((original[interruptionIndex] - reference) * sign);

            scanner.EncodeRunInterruption(writer, context, errorValue);
            reconstructed[interruptionIndex] = new JpegLsTraitsWrapper(_maximumSampleValue, _nearLossless).ComputeReconstructedSample(reference, errorValue * sign);
            scanner.DecrementRunIndex();
            return runLength + 1;
        }

        private int DecodeRunMode(
            JpegLsGolombCodeReader reader,
            JpegLsRunModeScanner scanner,
            int[] samples,
            int component,
            int x,
            int y,
            int left)
        {
            var runLength = scanner.DecodeRunLength(reader, _width - x);
            for (var index = 0; index < runLength; index++)
            {
                samples[GetSampleIndex(x + index, y, component)] = left;
            }

            if (x + runLength >= _width)
            {
                return runLength;
            }

            var interruptionX = x + runLength;
            var above = y > 0 ? samples[GetSampleIndex(interruptionX, y - 1, component)] : 0;
            var context = Math.Abs(left - above) <= _nearLossless
                ? scanner.RunInterruptionContexts[1]
                : scanner.RunInterruptionContexts[0];
            var sign = context == scanner.RunInterruptionContexts[1] ? 1 : Sign(above - left);
            var reference = context == scanner.RunInterruptionContexts[1] ? left : above;
            var errorValue = scanner.DecodeRunInterruption(reader, context);
            samples[GetSampleIndex(interruptionX, y, component)] = new JpegLsTraitsWrapper(_maximumSampleValue, _nearLossless).ComputeReconstructedSample(reference, errorValue * sign);
            scanner.DecrementRunIndex();
            return runLength + 1;
        }

        private static bool IsRunMode(int left, int above, int aboveLeft, int aboveRight, JpegLsTraits traits)
        {
            return traits.IsNear(aboveRight, above)
                && traits.IsNear(above, aboveLeft)
                && traits.IsNear(aboveLeft, left);
        }

        private void GetNeighbors(int[] samples, ProcessingState state, int x, int y, out int left, out int above, out int aboveLeft, out int aboveRight)
        {
            var component = state.Component;
            if (x == 0)
            {
                left = state.PreviousLineFirstSample;
                above = y > 0 ? state.PreviousLineFirstSample : 0;
                aboveLeft = state.PreviousPreviousLineFirstSample;
                aboveRight = y > 0 && _width > 1
                    ? samples[GetSampleIndex(1, y - 1, component)]
                    : above;
                return;
            }

            above = y > 0 ? samples[GetSampleIndex(x, y - 1, component)] : 0;
            left = samples[GetSampleIndex(x - 1, y, component)];
            aboveLeft = y > 0 ? samples[GetSampleIndex(x - 1, y - 1, component)] : 0;
            aboveRight = y > 0
                ? samples[GetSampleIndex(Math.Min(x + 1, _width - 1), y - 1, component)]
                : above;
        }

        private int GetSampleIndex(int x, int y, int component)
        {
            return (y * _width + x) * _componentCount + component;
        }

        private ProcessingState[] CreateComponentStates()
        {
            var states = new ProcessingState[_componentCount];
            var sharedModel = _interleaveMode == JpegLsInterleaveMode.Line || _interleaveMode == JpegLsInterleaveMode.Sample
                ? new JpegLsContextModel(_maximumSampleValue, _nearLossless, resetThreshold: 64)
                : null;
            var sharedRunContexts = sharedModel == null
                ? null
                : new[]
                {
                    new JpegLsRunModeContext(0, sharedModel.Traits.Range),
                    new JpegLsRunModeContext(1, sharedModel.Traits.Range),
                };
            for (var component = 0; component < _componentCount; component++)
            {
                var model = sharedModel ?? new JpegLsContextModel(_maximumSampleValue, _nearLossless, resetThreshold: 64);
                states[component] = new ProcessingState(
                    component,
                    0,
                    _height,
                    model,
                    new JpegLsRunModeScanner(model.Traits, sharedRunContexts),
                    edgeState: null);
            }

            return states;
        }

        private ProcessingState[] CreateProcessingOrder(ProcessingState[] states)
        {
            if (_interleaveMode == JpegLsInterleaveMode.None || _componentCount == 1)
            {
                var order = new ProcessingState[_componentCount];
                for (var component = 0; component < _componentCount; component++)
                {
                    order[component] = states[component].ForLines(0, _height);
                }

                return order;
            }

            if (_interleaveMode == JpegLsInterleaveMode.Line)
            {
                var order = new ProcessingState[_height * _componentCount];
                var index = 0;
                for (var y = 0; y < _height; y++)
                {
                    for (var component = 0; component < _componentCount; component++)
                    {
                        order[index++] = states[component].ForLines(y, y + 1);
                    }
                }

                return order;
            }

            if (_interleaveMode == JpegLsInterleaveMode.Sample)
            {
                throw new DicomCodecException("JPEG-LS sample interleave is not supported by this scan codec.");
            }

            throw new DicomCodecException($"JPEG-LS interleave mode {_interleaveMode} is not supported.");
        }

        private static int MapErrorValue(int errorValue)
        {
            return (errorValue << 1) ^ (errorValue >> 31);
        }

        private static int UnmapErrorValue(int mappedValue)
        {
            return (mappedValue >> 1) ^ -(mappedValue & 1);
        }

        private static int ApplySign(int value, int sign)
        {
            return (sign ^ value) - sign;
        }

        private static int Sign(int value)
        {
            return value < 0 ? -1 : 1;
        }

        private sealed class JpegLsTraitsWrapper
        {
            private readonly JpegLsTraits _traits;

            public JpegLsTraitsWrapper(int maximumSampleValue, int nearLossless)
            {
                _traits = JpegLsTraits.CreateDefault(maximumSampleValue, nearLossless, resetThreshold: 64);
            }

            public int ComputeErrorValue(int errorValue)
            {
                return _traits.ComputeErrorValue(errorValue);
            }

            public int ComputeReconstructedSample(int prediction, int errorValue)
            {
                return _traits.ComputeReconstructedSample(prediction, errorValue);
            }
        }

        private sealed class ProcessingState
        {
            private readonly EdgeState _edgeState;

            public ProcessingState(int component, int startLine, int endLine, JpegLsContextModel model, JpegLsRunModeScanner? scanner, EdgeState? edgeState)
            {
                Component = component;
                StartLine = startLine;
                EndLine = endLine;
                Model = model;
                Scanner = scanner ?? new JpegLsRunModeScanner(model.Traits);
                _edgeState = edgeState ?? new EdgeState();
            }

            public int Component { get; }

            public int StartLine { get; }

            public int EndLine { get; }

            public JpegLsContextModel Model { get; }

            public JpegLsRunModeScanner Scanner { get; }

            public int PreviousLineFirstSample
            {
                get { return _edgeState.PreviousLineFirstSample; }
            }

            public int PreviousPreviousLineFirstSample
            {
                get { return _edgeState.PreviousPreviousLineFirstSample; }
            }

            public ProcessingState ForLines(int startLine, int endLine)
            {
                return new ProcessingState(Component, startLine, endLine, Model, Scanner, _edgeState);
            }

            public void UpdateLineEdge(int firstSample)
            {
                _edgeState.PreviousPreviousLineFirstSample = _edgeState.PreviousLineFirstSample;
                _edgeState.PreviousLineFirstSample = firstSample;
            }
        }

        private sealed class EdgeState
        {
            public int PreviousLineFirstSample { get; set; }

            public int PreviousPreviousLineFirstSample { get; set; }
        }
    }
}
