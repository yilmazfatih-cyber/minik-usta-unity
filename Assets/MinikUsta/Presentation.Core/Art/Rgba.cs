#nullable enable
// A colour of the engine-independent presentation layer (Faz 3 Q1). The Unity layer converts it to Color / Color32 at its
// edge; nothing here knows the engine. Values are 8-bit sRGB like the tokens.json hex strings.
using System;
using System.Globalization;

namespace MinikUsta.Presentation.Art
{
    public readonly struct Rgba : IEquatable<Rgba>
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;
        public readonly byte A;

        public Rgba(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static readonly Rgba White = new Rgba(255, 255, 255);
        public static readonly Rgba Black = new Rgba(0, 0, 0);
        public static readonly Rgba Clear = new Rgba(0, 0, 0, 0);

        /// <summary>`#RRGGBB` (tokens.json) with an alpha 0 … 1.</summary>
        public static Rgba Hex(string hex, float alpha = 1f)
        {
            if (hex == null || hex.Length != 7 || hex[0] != '#') throw new FormatException("Rgba.Hex: expected #RRGGBB, got \"" + hex + "\"");
            int v = int.Parse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Rgba((byte)((v >> 16) & 0xff), (byte)((v >> 8) & 0xff), (byte)(v & 0xff), ToByte(alpha));
        }

        public float Rf => R / 255f;
        public float Gf => G / 255f;
        public float Bf => B / 255f;
        public float Af => A / 255f;

        public Rgba WithAlpha(float alpha) => new Rgba(R, G, B, ToByte(alpha));

        /// <summary>The RGB channels × `f` (tokens `*Factor` tones: outline = base × 0,42 …); alpha kept.</summary>
        public Rgba Scale(float f) => new Rgba(ToByte(Rf * f), ToByte(Gf * f), ToByte(Bf * f), A);

        /// <summary>`a + (b − a)·t` per channel, t clamped to 0 … 1.</summary>
        public static Rgba Lerp(Rgba a, Rgba b, float t)
        {
            if (t <= 0) return a;
            if (t >= 1) return b;
            return new Rgba(
                ToByte(a.Rf + (b.Rf - a.Rf) * t),
                ToByte(a.Gf + (b.Gf - a.Gf) * t),
                ToByte(a.Bf + (b.Bf - a.Bf) * t),
                ToByte(a.Af + (b.Af - a.Af) * t));
        }

        public string ToHex() => "#" + R.ToString("X2", CultureInfo.InvariantCulture) + G.ToString("X2", CultureInfo.InvariantCulture) + B.ToString("X2", CultureInfo.InvariantCulture);

        internal static byte ToByte(float v)
        {
            if (!(v > 0)) return 0;
            if (v >= 1) return 255;
            return (byte)Math.Round(v * 255f, MidpointRounding.AwayFromZero);
        }

        public bool Equals(Rgba o) => R == o.R && G == o.G && B == o.B && A == o.A;
        public override bool Equals(object? obj) => obj is Rgba c && Equals(c);
        public override int GetHashCode() => (R << 24) | (G << 16) | (B << 8) | A;
        public override string ToString() => ToHex() + (A < 255 ? "@" + A.ToString(CultureInfo.InvariantCulture) : "");
    }
}
