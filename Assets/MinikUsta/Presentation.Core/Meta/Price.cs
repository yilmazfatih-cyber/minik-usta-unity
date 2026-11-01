#nullable enable
// `PriceLabel` second line — port of web src/ui/price.ts (UX_FLOWS §0.3; BUSINESS E2; D-024): every coin price shows its
// real-money equivalent from the reference pack (`priceDisplay.referenceSku`): TR in lira rounded to whole lira ("≈ ₺81"),
// EN in dollars with cents ("≈ $1.79"). The web formats with Intl.NumberFormat (tr-TR TRY 0 digits, en-US USD 2 digits);
// this writes the same strings without a culture (WebGL players may run with the invariant culture). Also the grouped
// counter text of the HUD capsules (TR "1.250", EN "1,250").
using System;
using System.Globalization;
using System.Text;

namespace MinikUsta.Presentation.Meta
{
    public static class Price
    {
        /// <summary>Shown before the real-money amount (UX §0.3 "≈ 81 TL").</summary>
        public const string ApproxSign = "≈";

        /// <summary>Real-money value of `coins` in the language's currency (not rounded).</summary>
        public static double RealMoneyValue(int coins, string lang, CoinPack pack) => coins * (lang == "tr" ? pack.Try : pack.Usd) / pack.Coins;

        /// <summary>"≈ ₺81" (tr) / "≈ $1.79" (en).</summary>
        public static string RealMoneyText(int coins, string lang, CoinPack pack)
        {
            // decimal: the prices are decimal literals (1.99, 89.99); Intl rounds the exact value half away from zero
            decimal v = coins * (decimal)(lang == "tr" ? pack.Try : pack.Usd) / pack.Coins;
            if (lang == "tr") return ApproxSign + " ₺" + Grouped((long)Math.Round(v, 0, MidpointRounding.AwayFromZero), lang);
            long cents = (long)Math.Round(v * 100, 0, MidpointRounding.AwayFromZero);
            return ApproxSign + " $" + Grouped(cents / 100, lang) + "." + (cents % 100).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>Thousands grouping: tr "1.250", en "1,250".</summary>
        public static string Grouped(long n, string lang)
        {
            string digits = Math.Abs(n).ToString(CultureInfo.InvariantCulture);
            char sep = lang == "tr" ? '.' : ',';
            var b = new StringBuilder();
            if (n < 0) b.Append('-');
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) b.Append(sep);
                b.Append(digits[i]);
            }
            return b.ToString();
        }
    }
}
