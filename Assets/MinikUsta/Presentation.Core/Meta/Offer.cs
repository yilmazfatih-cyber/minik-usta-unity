#nullable enable
// "Hamleler bitti!" window 1 model — port of web src/ui/offer.ts (GDD K-29; UX_FLOWS §7; D-024 / R-15; META §3.2–3.3). Pure:
// the core's open offer (`GameSession.NextOffer()`: number n, ad allowed) + the wallet + the ad state → what the window shows.
// Prices, move counts and caps come from config/economy.json (D-017).
//
// - Equal options (R-15): coins (orange), ad (cream / grey, only on offer 1), "Hayır, teşekkürler".
// - Lifetime-first offer (META §3.2): the coin option is free (`lose.offer.gift`), no price, no ad.
// - Not enough coins: the coin option is `Short` — the window shows it grey and passive (`common.notEnoughCoins`, UX §7
//   "Mağaza kilitliyken"; the Unity build has no shop yet).
// - Ads (META §3.3): the option is never hidden; with no ad provider (the Unity build) it is grey with `ads.none`.
using System;

namespace MinikUsta.Presentation.Meta
{
    public enum CoinOptionKind
    {
        Gift,
        Buy,
        Short,
    }

    public enum AdLineKind
    {
        Today,
        None,
        Tomorrow,
    }

    public sealed class AdAvailability
    {
        public readonly bool Available;
        public readonly int WatchedToday;

        private AdAvailability(bool available, int watchedToday)
        {
            Available = available;
            WatchedToday = watchedToday;
        }

        /// <summary>No ad provider (web MVP, the Unity build).</summary>
        public static readonly AdAvailability None = new AdAvailability(false, 0);

        public static AdAvailability Watched(int today) => new AdAvailability(true, today);
    }

    public sealed class OfferModel
    {
        public readonly int N;
        public readonly int Max;
        /// <summary>`lose.offer.last` only when n = max (STORY §7.3), else `lose.offer.count`.</summary>
        public readonly string CounterKey;
        public readonly CoinOptionKind Coin;
        public readonly int Moves;
        /// <summary>Price of the coin option (0 for the gift).</summary>
        public readonly int Price;
        /// <summary>Coins missing when `Short`.</summary>
        public readonly int Missing;
        /// <summary>null: no ad option (offer 2–3, the lifetime-first gift).</summary>
        public readonly AdOption? Ad;

        public OfferModel(int n, int max, string counterKey, CoinOptionKind coin, int moves, int price, int missing, AdOption? ad)
        {
            N = n;
            Max = max;
            CounterKey = counterKey;
            Coin = coin;
            Moves = moves;
            Price = price;
            Missing = missing;
            Ad = ad;
        }

        /// <summary>Coins actually paid when the coin option is taken (0 for the gift; K-43/4 `offerSpendCoins`).</summary>
        public int Paid => Coin == CoinOptionKind.Gift ? 0 : Price;

        /// <summary>The coin option can be pressed.</summary>
        public bool CoinEnabled => Coin != CoinOptionKind.Short;
    }

    public sealed class AdOption
    {
        public readonly bool Enabled;
        public readonly int Moves;
        public readonly AdLineKind Line;
        /// <summary>`lose.adToday` {n}/{max} when `Line == Today`.</summary>
        public readonly int TodayN;
        public readonly int TodayMax;

        public AdOption(bool enabled, int moves, AdLineKind line, int todayN = 0, int todayMax = 0)
        {
            Enabled = enabled;
            Moves = moves;
            Line = line;
            TodayN = todayN;
            TodayMax = todayMax;
        }

        public string LineKey => Line == AdLineKind.Today ? "lose.adToday" : Line == AdLineKind.Tomorrow ? "ads.tomorrow" : "ads.none";
    }

    public static class Offers
    {
        public const string CountKey = "lose.offer.count";
        public const string LastKey = "lose.offer.last";

        /// <summary>web `offerModel`: offer `n` (1 … max) of the open window.</summary>
        public static OfferModel Model(int n, bool adAllowed, bool giftAvailable, int coins, AdAvailability ads, EconomyConfig eco)
        {
            if (n < 1 || n > eco.MaxOffers) throw new ArgumentOutOfRangeException(nameof(n), "offerModel: offer " + n);
            bool gift = eco.FirstEverOfferFree && giftAvailable && n == 1;
            int price = eco.OfferCosts.Length == 0 ? 0 : eco.OfferCosts[Math.Min(n - 1, eco.OfferCosts.Length - 1)];
            CoinOptionKind kind = gift ? CoinOptionKind.Gift : coins >= price ? CoinOptionKind.Buy : CoinOptionKind.Short;
            AdOption? ad = null;
            if (adAllowed && !gift)
            {
                if (!ads.Available) ad = new AdOption(false, eco.AdExtraMoves, AdLineKind.None);
                else if (ads.WatchedToday >= eco.AdPerDay) ad = new AdOption(false, eco.AdExtraMoves, AdLineKind.Tomorrow);
                else ad = new AdOption(true, eco.AdExtraMoves, AdLineKind.Today, ads.WatchedToday + 1, eco.AdPerDay);
            }
            return new OfferModel(n, eco.MaxOffers, n >= eco.MaxOffers ? LastKey : CountKey, kind, eco.ExtraMoves, gift ? 0 : price,
                kind == CoinOptionKind.Short ? price - coins : 0, ad);
        }
    }
}
