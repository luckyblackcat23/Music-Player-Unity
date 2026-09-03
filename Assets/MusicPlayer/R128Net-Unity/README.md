# R128Net-Unity

Reduced Unity-compatible port of the integrated-loudness portion of R128Net.

## What is included

- BS.1770 K-weighting filter
- 400 ms gating blocks
- 100 ms block advancement
- -70 LUFS absolute gate
- -10 LU relative gate
- Integrated loudness in LUFS
- Interleaved float PCM input
- Stereo/mono and multichannel channel weighting
- Incremental processing; the complete audio file does not need to be loaded

## What was removed

- Momentary loudness API
- Short-term loudness API
- Loudness range
- Histogram mode
- Sample peak
- True peak
- SIMD / `System.Runtime.Intrinsics`
- `NativeMemory`
- source generator
- static abstract interfaces
- modern `record struct`/collection-expression requirements

The implementation intentionally uses ordinary arrays and `List<double>` rather than R128Net's single native state allocation. This makes it substantially easier to use from Unity, at the cost of some managed allocations when the gating history grows.

## Unity usage

Put `IntegratedLoudnessMeter.cs` somewhere under `Assets/`.

For a stereo 48 kHz float stream:

```csharp
using R128Net;

var meter = new IntegratedLoudnessMeter(2, 48000);

meter.AddFrames(samples);

double lufs = meter.IntegratedLoudness;
```

`AddFrames()` accepts arbitrary-sized chunks as long as the chunk contains a whole number of interleaved frames.

For a stereo file:

```text
L R L R L R L R ...
```

For mono:

```text
L L L L ...
```

## ManagedBass

This is intended to work particularly well with the ManagedBass decoder you are already using.

Decode the file to a float buffer in chunks and feed each decoded chunk to:

```csharp
meter.AddFrames(buffer, 0, sampleCount);
```

Do not create a new meter for every buffer. Create one meter per song, feed all decoded buffers into it, read `IntegratedLoudness`, then dispose/reset it.

## Normalisation

If you want a target of -14 LUFS, for example:

```csharp
double gainDb = -14.0 - song.LUFS;
float gain = Mathf.Pow(10f, (float)(gainDb / 20.0));
```

For example:

- song = -8 LUFS -> gain = -6 dB
- song = -20 LUFS -> gain = +6 dB

You may want to cap the positive gain to avoid boosting very quiet material excessively.

## Important

This file is a reduced port derived from R128Net's implementation. Keep the MIT attribution/license when distributing it.

The original R128Net project is:
https://github.com/routersys/R128Net
