#nullable enable
/*
  ZzFX - Zuper Zmall Zound Zynth v1.4.0 by Frank Force
  https://github.com/KilledByAPixel/ZzFX

  ZzFX MIT License

  Copyright (c) 2019 - Frank Force

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files (the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions:

  The above copyright notice and this permission notice shall be included in all
  copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
  SOFTWARE.
*/

// C# port of `ZZFX.buildSamples` of ZzFX 1.4.0 (MIT, (c) 2019 Frank Force; the header above is kept as the licence requires),
// through the web repo's TypeScript port src/services/audio/zzfxSynth.ts (paket V2, decision D-057: no dependency, only this
// pure function). Changes against the original, as in the web port:
//  - the sample rate is an argument (here 44 100 Hz, the rate of the Unity AudioClip; the web build renders 22 050 Hz);
//  - the parameter list is an array; a missing trailing entry takes the original default;
//  - `randomness` (index 1) is ignored: the original multiplies the frequency by 1 ± randomness · Math.random(). The tokens
//    always write 0, so dropping the term keeps the sound identical and the synth deterministic;
//  - the sample loop is resumable (`Run(maxSamples)`): a long sound is rendered over several frames (a sample budget per
//    frame, `SoundBank.Pump`), the arithmetic and its order unchanged (bit-identical to one run).
// Arithmetic is IEEE double precision exactly as in JavaScript (the buffer stores float32, like the Float32Array of the web
// port); Math.round is JS' round-half-up; `i * i` is a double product (an int product would overflow after 46 340 samples).
using System;
using System.Collections.Generic;

namespace MinikUsta.Presentation.Audio
{
    public static class ZzFx
    {
        /// <summary>Number of ZzFX parameters (tokens `audio._doc` order).</summary>
        public const int ParamCount = 21;

        /// <summary>Original ZzFX 1.4.0 default values, index = parameter position.</summary>
        public static readonly double[] Defaults =
        {
            1, // volume
            0.05, // randomness (ignored)
            220, // frequency
            0, // attack
            0, // sustain
            0.1, // release
            0, // shape
            1, // shapeCurve
            0, // slide
            0, // deltaSlide
            0, // pitchJump
            0, // pitchJumpTime
            0, // repeatTime
            0, // noise
            0, // modulation
            0, // bitCrush
            0, // delay
            1, // sustainVolume
            0, // decay
            0, // tremolo
            0, // filter
        };

        internal static double Param(IReadOnlyList<double> p, int index) => index < p.Count ? p[index] : Defaults[index];

        /// <summary>Buffer length in samples of one parameter list (attack + decay + sustain + release + delay, as `ZzFxRender`).</summary>
        public static int Length(IReadOnlyList<double> p, int sampleRate)
        {
            double attack = Param(p, 3) * sampleRate;
            if (attack == 0) attack = 9; // JS `|| 9`
            double sum = attack + Param(p, 18) * sampleRate + Param(p, 4) * sampleRate + Param(p, 5) * sampleRate + Param(p, 16) * sampleRate;
            int length = (int)sum; // JS `| 0`: truncation toward zero
            return length > 0 ? length : 0;
        }

        /// <summary>The mono sample buffer of one parameter list (one `ZzFxRender` run to the end). Pure and deterministic.</summary>
        public static float[] Build(IReadOnlyList<double> p, int sampleRate)
        {
            var r = new ZzFxRender(p, sampleRate);
            r.Run(int.MaxValue);
            return r.Samples;
        }

        /// <summary>JS `Math.round`: ties go toward +infinity (C# Math.Round is banker's rounding).</summary>
        internal static double JsRound(double x)
        {
            double f = Math.Floor(x);
            return x - f >= 0.5 ? f + 1 : f;
        }
    }

    /// <summary>
    /// One ZzFX sound rendered in resumable slices: `Run(n)` renders the next `n` samples (or what is left) into `Samples`. The
    /// buffer is allocated up front (the delay term reads earlier samples of the same buffer).
    /// </summary>
    public sealed class ZzFxRender
    {
        private const double PI2 = Math.PI * 2;

        public readonly float[] Samples;

        // constants of the sound (scaled by the sample rate)
        private readonly double volume;
        private readonly double attack;
        private readonly double sustain;
        private readonly double release;
        private readonly double shape;
        private readonly double shapeCurve;
        private readonly double deltaSlide;
        private readonly double pitchJump;
        private readonly double pitchJumpTime;
        private readonly long repeatTime;
        private readonly double noise;
        private readonly double modulation;
        private readonly double bitCrush;
        private readonly double delay;
        private readonly double sustainVolume;
        private readonly double decay;
        private readonly double tremolo;
        private readonly double filter;
        private readonly double startSlide;
        private readonly int length;
        private readonly double a1, a2, b0, b1, b2;

        // loop state carried between `Run` calls
        private double frequency;
        private double slide;
        private double startFrequency;
        private double modOffset;
        private long repeat;
        private long crush;
        private double jump = 1;
        private double t;
        private int i;
        private double s;
        private double x2, x1, y2, y1;

        public ZzFxRender(IReadOnlyList<double> p, int sampleRate)
        {
            volume = ZzFx.Param(p, 0);
            double freq = ZzFx.Param(p, 2);
            double atk = ZzFx.Param(p, 3);
            double sus = ZzFx.Param(p, 4);
            double rel = ZzFx.Param(p, 5);
            shape = ZzFx.Param(p, 6);
            shapeCurve = ZzFx.Param(p, 7);
            double sld = ZzFx.Param(p, 8);
            double dSlide = ZzFx.Param(p, 9);
            double pJump = ZzFx.Param(p, 10);
            double pJumpTime = ZzFx.Param(p, 11);
            double rep = ZzFx.Param(p, 12);
            noise = ZzFx.Param(p, 13);
            double mod = ZzFx.Param(p, 14);
            bitCrush = ZzFx.Param(p, 15);
            double dly = ZzFx.Param(p, 16);
            sustainVolume = ZzFx.Param(p, 17);
            double dec = ZzFx.Param(p, 18);
            tremolo = ZzFx.Param(p, 19);
            double flt = ZzFx.Param(p, 20);
            filter = flt;

            // init parameters
            sld *= (500 * PI2) / sampleRate / sampleRate;
            startSlide = sld;
            slide = sld;
            freq *= PI2 / sampleRate;
            startFrequency = freq;
            frequency = freq;

            // biquad LP/HP filter
            const double quality = 2;
            double w = (PI2 * Math.Abs(flt) * 2) / sampleRate;
            double cos = Math.Cos(w);
            double alpha = Math.Sin(w) / 2 / quality;
            double a0 = 1 + alpha;
            double sign = flt < 0 ? -1 : 1;
            a1 = (-2 * cos) / a0;
            a2 = (1 - alpha) / a0;
            b0 = (1 + sign * cos) / 2 / a0;
            b1 = -(sign + cos) / a0;
            b2 = b0;

            // scale by sample rate
            const double minAttack = 9; // prevent pop if attack is 0
            atk = atk * sampleRate;
            if (atk == 0) atk = minAttack;
            attack = atk;
            decay = dec * sampleRate;
            sustain = sus * sampleRate;
            release = rel * sampleRate;
            delay = dly * sampleRate;
            double sr = sampleRate;
            deltaSlide = dSlide * ((500 * PI2) / (sr * sr * sr));
            modulation = mod * (PI2 / sampleRate);
            pitchJump = pJump * (PI2 / sampleRate);
            pitchJumpTime = pJumpTime * sampleRate;
            repeatTime = (long)(rep * sampleRate);

            // allocate the full sample buffer up front
            int len = (int)(attack + decay + sustain + release + delay);
            length = len;
            Samples = new float[len > 0 ? len : 0];
        }

        /// <summary>Every sample is rendered.</summary>
        public bool Done => i >= length;

        /// <summary>Samples rendered so far.</summary>
        public int Rendered => Math.Min(i, Samples.Length);

        /// <summary>Renders up to `maxSamples` more samples; returns how many this call rendered.</summary>
        public int Run(int maxSamples)
        {
            var b = Samples;
            int from = i;
            int stop = (int)Math.Min((long)length, (long)i + Math.Max(0, maxSamples));
            long crushEvery = (long)(bitCrush * 100); // JS `(bitCrush * 100) | 0`
            double sv = s;
            int idx = i;

            // generate waveform
            for (; idx < stop; b[idx++] = (float)(sv * volume))
            {
                crush++;
                if (crushEvery == 0 || crush % crushEvery == 0)
                {
                    // wave shape: 0 sin, 1 triangle, 2 saw, 3 tan, 4 noise, 5 square duty
                    if (shape != 0)
                    {
                        if (shape > 1)
                        {
                            if (shape > 2)
                            {
                                if (shape > 3)
                                {
                                    if (shape > 4) sv = (t / PI2) % 1 < shapeCurve / 2 ? 1 : -1;
                                    else sv = Math.Sin(Math.Pow(t, 3));
                                }
                                else sv = Math.Max(Math.Min(Math.Tan(t), 1), -1);
                            }
                            else sv = 1 - (((((2 * t) / PI2) % 2) + 2) % 2);
                        }
                        else sv = 1 - 4 * Math.Abs(ZzFx.JsRound(t / PI2) - t / PI2);
                    }
                    else sv = Math.Sin(t);

                    double trem = repeatTime != 0 ? 1 - tremolo + tremolo * Math.Sin((PI2 * idx) / repeatTime) : 1;
                    double curved = shape > 4 ? sv : (sv < 0 ? -1 : 1) * Math.Pow(Math.Abs(sv), shapeCurve);
                    double env = idx < attack
                        ? idx / attack // attack
                        : idx < attack + decay // decay
                            ? 1 - ((idx - attack) / decay) * (1 - sustainVolume) // decay falloff
                            : idx < attack + decay + sustain // sustain
                                ? sustainVolume // sustain volume
                                : idx < length - delay // release
                                    ? ((length - idx - delay) / release) * sustainVolume // release falloff
                                    : 0; // post release
                    sv = trem * curved * env;

                    if (delay != 0)
                    {
                        // sample delay
                        double echo = delay > idx
                            ? 0
                            : ((idx < length - delay ? 1 : (length - idx) / delay) * (double)b[(int)(idx - delay)]) / 2 / volume;
                        sv = sv / 2 + echo;
                    }

                    if (filter != 0)
                    {
                        // apply filter
                        double y = b2 * x2 + b1 * x1 + b0 * sv - a2 * y2 - a1 * y1;
                        x2 = x1;
                        x1 = sv;
                        y2 = y1;
                        y1 = y;
                        sv = y;
                    }
                }

                slide += deltaSlide;
                frequency += slide;
                double f = frequency * Math.Cos(modulation * modOffset); // frequency, modulation
                modOffset++;
                t += f + f * noise * ((((double)idx * idx * PI2) % 2) - 1); // noise

                if (jump != 0)
                {
                    jump += 1;
                    if (jump > pitchJumpTime)
                    {
                        // pitch jump
                        frequency += pitchJump;
                        startFrequency += pitchJump;
                        jump = 0;
                    }
                }

                if (repeatTime != 0)
                {
                    repeat++;
                    if (repeat % repeatTime == 0)
                    {
                        // repeat
                        frequency = startFrequency;
                        slide = startSlide;
                        if (jump == 0) jump = 1;
                    }
                }
            }

            s = sv;
            i = idx;
            return idx - from;
        }
    }
}
