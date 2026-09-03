// R128Net-Unity
// A reduced, Unity-compatible implementation of the integrated-loudness portion
// of R128Net/libebur128.
//
// Original project: https://github.com/routersys/R128Net
// License: MIT. See the original repository for the full license text.
//
// This reduced implementation intentionally omits momentary/short-term APIs,
// loudness range, histogram mode, sample peak, true peak, source generators,
// SIMD intrinsics and NativeMemory. It retains the BS.1770 K-weighting filter,
// 400 ms gating blocks, -70 LUFS absolute gate and -10 LU relative gate.
//
// Input: interleaved floating-point PCM normalized to [-1, 1].
// Output: integrated programme loudness in LUFS.

using System;
using System.Collections.Generic;

namespace R128Net
{
    /// <summary>
    /// Calculates integrated programme loudness in LUFS according to
    /// ITU-R BS.1770 / EBU R128.
    ///
    /// Audio must be interleaved, normalized floating-point PCM in [-1, 1].
    /// Feed arbitrary-sized chunks with AddFrames(). The meter keeps its
    /// filtering state and 400 ms analysis window between calls.
    /// </summary>
    public sealed class IntegratedLoudnessMeter : IDisposable
    {
        private const double AbsoluteGateLufs = -70.0;
        private const double RelativeGateLufs = -10.0;
        private const double LufsOffset = -0.691;

        private readonly int channels;
        private readonly int sampleRate;
        private readonly int samplesIn100ms;
        private readonly int ringFrames;

        // Four hundred milliseconds of filtered, interleaved audio.
        private readonly double[] audioRing;

        // Four samples of IIR state per channel.
        private readonly double[] filterState;

        // Combined fourth-order K-weighting filter coefficients.
        private readonly double[] numerator = new double[5];
        private readonly double[] denominator = new double[5];

        private readonly ChannelPosition[] channelMap;

        // One energy value per 400 ms gating block.
        private readonly List<double> blockEnergies = new List<double>();

        private int ringFrameIndex;
        private int framesSinceBlock;
        private long framesProcessed;
        private bool disposed;

        public int Channels
        {
            get { return channels; }
        }

        public int SampleRate
        {
            get { return sampleRate; }
        }

        public long FramesProcessed
        {
            get { return framesProcessed; }
        }

        /// <summary>
        /// Creates an integrated-loudness meter.
        /// </summary>
        public IntegratedLoudnessMeter(int channels, int sampleRate)
        {
            if (channels < 1 || channels > 64)
                throw new ArgumentOutOfRangeException("channels");

            if (sampleRate < 16)
                throw new ArgumentOutOfRangeException("sampleRate");

            this.channels = channels;
            this.sampleRate = sampleRate;

            samplesIn100ms = (sampleRate + 5) / 10;
            ringFrames = checked(samplesIn100ms * 4);

            audioRing = new double[checked(ringFrames * channels)];
            filterState = new double[checked(channels * 4)];
            channelMap = new ChannelPosition[channels];

            InitializeChannelMap();
            CreateKWeighting(sampleRate);
        }

        /// <summary>
        /// Adds normalized, interleaved floating-point PCM.
        /// </summary>
        public void AddFrames(float[] interleaved)
        {
            if (interleaved == null)
                throw new ArgumentNullException("interleaved");

            AddFrames(interleaved, 0, interleaved.Length);
        }

        /// <summary>
        /// Adds normalized, interleaved floating-point PCM.
        /// sampleCount is the number of samples, not frames.
        /// </summary>
        public void AddFrames(float[] interleaved, int offset, int sampleCount)
        {
            ThrowIfDisposed();

            if (interleaved == null)
                throw new ArgumentNullException("interleaved");

            if (offset < 0 || sampleCount < 0 ||
                offset > interleaved.Length - sampleCount)
            {
                throw new ArgumentOutOfRangeException();
            }

            if ((sampleCount % channels) != 0)
            {
                throw new ArgumentException(
                    "The interleaved buffer must contain a whole number of frames.",
                    "sampleCount");
            }

            int frames = sampleCount / channels;
            int sampleIndex = offset;

            for (int frame = 0; frame < frames; ++frame)
            {
                int ringOffset = ringFrameIndex * channels;

                for (int channel = 0; channel < channels; ++channel)
                {
                    double x = FlushDenormal(interleaved[sampleIndex++]);

                    double s1 = filterState[channel];
                    double s2 = filterState[channels + channel];
                    double s3 = filterState[(channels * 2) + channel];
                    double s4 = filterState[(channels * 3) + channel];

                    // This is the same direct-form recurrence used by
                    // R128Net's scalar KWeightingFilter path.
                    double accumulator = FlushDenormal(
                        x - FlushDenormal(denominator[1] * s1));

                    accumulator = FlushDenormal(
                        accumulator - FlushDenormal(denominator[2] * s2));

                    accumulator = FlushDenormal(
                        accumulator - FlushDenormal(denominator[3] * s3));

                    double v0 = FlushDenormal(
                        accumulator - FlushDenormal(denominator[4] * s4));

                    double output = FlushDenormal(
                        FlushDenormal(numerator[0] * v0) +
                        FlushDenormal(numerator[1] * s1));

                    output = FlushDenormal(
                        output + FlushDenormal(numerator[2] * s2));

                    output = FlushDenormal(
                        output + FlushDenormal(numerator[3] * s3));

                    output = FlushDenormal(
                        output + FlushDenormal(numerator[4] * s4));

                    audioRing[ringOffset + channel] = output;

                    // Shift the IIR delay line.
                    filterState[channel] = v0;
                    filterState[channels + channel] = s1;
                    filterState[(channels * 2) + channel] = s2;
                    filterState[(channels * 3) + channel] = s3;
                }

                ++ringFrameIndex;
                if (ringFrameIndex == ringFrames)
                    ringFrameIndex = 0;

                ++framesSinceBlock;
                ++framesProcessed;

                // libebur128/R128Net evaluates a 400 ms block every 100 ms.
                if (framesSinceBlock == samplesIn100ms)
                {
                    framesSinceBlock = 0;

                    if (framesProcessed >= ringFrames)
                        AddGatingBlock();
                }
            }
        }

        /// <summary>
        /// Integrated programme loudness in LUFS.
        ///
        /// Returns NegativeInfinity when no 400 ms block survives
        /// the absolute -70 LUFS gate.
        /// </summary>
        public double IntegratedLoudness
        {
            get
            {
                ThrowIfDisposed();

                if (blockEnergies.Count == 0)
                    return double.NegativeInfinity;

                // First pass: mean energy after the absolute -70 LUFS gate.
                double meanEnergy = 0.0;

                for (int i = 0; i < blockEnergies.Count; ++i)
                    meanEnergy += blockEnergies[i];

                meanEnergy /= blockEnergies.Count;

                // Relative gate is 10 LU below the ungated mean.
                double relativeThreshold =
                    meanEnergy * Math.Pow(10.0, RelativeGateLufs / 10.0);

                // Second pass: retain blocks above the relative threshold.
                double gatedEnergy = 0.0;
                int gatedCount = 0;

                for (int i = 0; i < blockEnergies.Count; ++i)
                {
                    double energy = blockEnergies[i];

                    if (energy >= relativeThreshold)
                    {
                        gatedEnergy += energy;
                        ++gatedCount;
                    }
                }

                if (gatedCount == 0)
                    return double.NegativeInfinity;

                gatedEnergy /= gatedCount;

                return EnergyToLoudness(gatedEnergy);
            }
        }

        /// <summary>
        /// Clears the measurement while retaining the filter configuration.
        /// </summary>
        public void Reset()
        {
            ThrowIfDisposed();

            Array.Clear(audioRing, 0, audioRing.Length);
            Array.Clear(filterState, 0, filterState.Length);
            blockEnergies.Clear();

            ringFrameIndex = 0;
            framesSinceBlock = 0;
            framesProcessed = 0;
        }

        /// <summary>
        /// Changes the BS.1770 channel position for a channel.
        /// </summary>
        public void SetChannel(int channel, ChannelPosition position)
        {
            ThrowIfDisposed();

            if (channel < 0 || channel >= channels)
                throw new ArgumentOutOfRangeException("channel");

            if (position == ChannelPosition.DualMono &&
                (channels != 1 || channel != 0))
            {
                throw new ArgumentException(
                    "DualMono only applies to the single channel of a mono meter.",
                    "position");
            }

            channelMap[channel] = position;
        }

        public ChannelPosition GetChannel(int channel)
        {
            ThrowIfDisposed();

            if (channel < 0 || channel >= channels)
                throw new ArgumentOutOfRangeException("channel");

            return channelMap[channel];
        }

        private void AddGatingBlock()
        {
            double energy = BlockEnergy();

            // Energy corresponding to -70 LUFS:
            //
            // LUFS = 10*log10(energy) - 0.691
            //
            // Therefore:
            // energy = 10^((-70 + 0.691) / 10)
            double absoluteThreshold =
                Math.Pow(10.0, (AbsoluteGateLufs - LufsOffset) / 10.0);

            if (energy >= absoluteThreshold)
                blockEnergies.Add(energy);
        }

        private double BlockEnergy()
        {
            double sum = 0.0;

            for (int channel = 0; channel < channels; ++channel)
            {
                ChannelPosition position = channelMap[channel];

                if (position == ChannelPosition.Unused)
                    continue;

                double channelSum = 0.0;

                // ringFrameIndex points at the frame that will be written
                // next. Therefore the next ringFrames frames from that
                // position are exactly the complete 400 ms history.
                for (int frame = 0; frame < ringFrames; ++frame)
                {
                    int ringFrame = ringFrameIndex + frame;

                    if (ringFrame >= ringFrames)
                        ringFrame -= ringFrames;

                    double value =
                        audioRing[(ringFrame * channels) + channel];

                    channelSum += value * value;
                }

                channelSum *= ChannelWeight(position);
                sum += channelSum;
            }

            return sum / ringFrames;
        }

        private void InitializeChannelMap()
        {
            if (channels == 4)
            {
                channelMap[0] = ChannelPosition.Left;
                channelMap[1] = ChannelPosition.Right;
                channelMap[2] = ChannelPosition.LeftSurround;
                channelMap[3] = ChannelPosition.RightSurround;
                return;
            }

            if (channels == 5)
            {
                channelMap[0] = ChannelPosition.Left;
                channelMap[1] = ChannelPosition.Right;
                channelMap[2] = ChannelPosition.Center;
                channelMap[3] = ChannelPosition.LeftSurround;
                channelMap[4] = ChannelPosition.RightSurround;
                return;
            }

            for (int i = 0; i < channels; ++i)
            {
                switch (i)
                {
                    case 0:
                        channelMap[i] = ChannelPosition.Left;
                        break;

                    case 1:
                        channelMap[i] = ChannelPosition.Right;
                        break;

                    case 2:
                        channelMap[i] = ChannelPosition.Center;
                        break;

                    case 4:
                        channelMap[i] = ChannelPosition.LeftSurround;
                        break;

                    case 5:
                        channelMap[i] = ChannelPosition.RightSurround;
                        break;

                    default:
                        channelMap[i] = ChannelPosition.Unused;
                        break;
                }
            }
        }

        private static double ChannelWeight(ChannelPosition position)
        {
            switch (position)
            {
                case ChannelPosition.Mp110:
                case ChannelPosition.Mm110:
                case ChannelPosition.Mp060:
                case ChannelPosition.Mm060:
                case ChannelPosition.Mp090:
                case ChannelPosition.Mm090:
                    return 1.41;

                case ChannelPosition.DualMono:
                    return 2.0;

                default:
                    return 1.0;
            }
        }

        private void CreateKWeighting(int rate)
        {
            // These constants and the coefficient construction are taken
            // from R128Net's KWeighting.Create() implementation.

            double f0 = 1681.974450955533;
            double g = 3.999843853973347;
            double q = 0.7071752369554196;

            double k = Math.Tan(Math.PI * f0 / rate);
            double vh = Math.Pow(10.0, g / 20.0);
            double vb = Math.Pow(vh, 0.4996667741545416);

            double[] pb = new double[3];
            double[] pa = new double[3];

            pa[0] = 1.0;

            double a0 = 1.0 + (k / q) + (k * k);

            pb[0] = (vh + (vb * k / q) + (k * k)) / a0;
            pb[1] = 2.0 * ((k * k) - vh) / a0;
            pb[2] = (vh - (vb * k / q) + (k * k)) / a0;

            pa[1] = 2.0 * ((k * k) - 1.0) / a0;
            pa[2] = (1.0 - (k / q) + (k * k)) / a0;

            f0 = 38.13547087602444;
            q = 0.5003270373238773;

            k = Math.Tan(Math.PI * f0 / rate);

            double a1 = 1.0 + (k / q) + (k * k);

            double[] ra = new double[3];
            ra[0] = 1.0;
            ra[1] = 2.0 * ((k * k) - 1.0) / a1;
            ra[2] = (1.0 - (k / q) + (k * k)) / a1;

            // Combine the two second-order sections into the same
            // fourth-order numerator/denominator used by R128Net.
            numerator[0] = pb[0];
            numerator[1] = (-2.0 * pb[0]) + pb[1];
            numerator[2] = pb[0] + (-2.0 * pb[1]) + pb[2];
            numerator[3] = pb[1] + (-2.0 * pb[2]);
            numerator[4] = pb[2];

            denominator[0] = 1.0;
            denominator[1] = ra[1] + pa[1];
            denominator[2] = ra[2] + (pa[1] * ra[1]) + pa[2];
            denominator[3] = (pa[1] * ra[2]) + (pa[2] * ra[1]);
            denominator[4] = pa[2] * ra[2];

            if (!IsStable(pa[1], pa[2]) ||
                !IsStable(ra[1], ra[2]))
            {
                throw new ArgumentOutOfRangeException(
                    "sampleRate",
                    "The BS.1770 K-weighting filter is unstable at this sample rate.");
            }
        }

        private static bool IsStable(double c1, double c2)
        {
            return 1.0 + c1 + c2 > 0.0 &&
                   1.0 - c1 + c2 > 0.0 &&
                   c2 < 1.0;
        }

        private static double EnergyToLoudness(double energy)
        {
            if (energy <= 0.0)
                return double.NegativeInfinity;

            return (10.0 * (Math.Log(energy) / Math.Log(10.0))) +
                   LufsOffset;
        }

        private static double FlushDenormal(double value)
        {
            // R128Net emulates the original libebur128 denormal handling
            // because .NET does not expose an equivalent MXCSR operation.
            double absolute = Math.Abs(value);

            if (absolute != 0.0 && absolute < 1.0e-300)
                return 0.0;

            return value;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException("IntegratedLoudnessMeter");
        }

        public void Dispose()
        {
            disposed = true;
        }
    }

    /// <summary>
    /// BS.1770 channel positions. The aliases match the positions used
    /// by the original R128Net project.
    /// </summary>
    public enum ChannelPosition
    {
        Unused = 0,
        Left = 1,
        Mp030 = 1,
        Right = 2,
        Mm030 = 2,
        Center = 3,
        Mp000 = 3,
        LeftSurround = 4,
        Mp110 = 4,
        RightSurround = 5,
        Mm110 = 5,
        DualMono = 6,
        MpSC = 7,
        MmSC = 8,
        Mp060 = 9,
        Mm060 = 10,
        Mp090 = 11,
        Mm090 = 12,
        Mp135 = 13,
        Mm135 = 14,
        Mp180 = 15,
        Up000 = 16,
        Up030 = 17,
        Um030 = 18,
        Up045 = 19,
        Um045 = 20,
        Up090 = 21,
        Um090 = 22,
        Up110 = 23,
        Um110 = 24,
        Up135 = 25,
        Um135 = 26,
        Up180 = 27,
        Tp000 = 28,
        Bp000 = 29,
        Bp045 = 30,
        Bm045 = 31
    }
}
