#nullable enable
// Paket V1: the meta rules the windows show — the K-29 +5 offer (port of web src/ui/offer.ts), the K-28 win rewards (rewards.ts),
// the real-money line (price.ts, the same strings as Intl.NumberFormat tr-TR TRY / en-US USD) and economy.json reading.
using MinikUsta.Presentation.Meta;
using MinikUsta.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public class MetaTests
    {
        private static EconomyConfig Eco => AppRig.Economy();

        [Test]
        public void META_economy_json_values_are_read()
        {
            var e = Eco;
            Assert.AreEqual(500, e.StartCoins);
            Assert.AreEqual(5, e.StartLives);
            Assert.AreEqual(new[] { 900, 1350, 1800 }, e.OfferCosts);
            Assert.AreEqual(3, e.MaxOffers);
            Assert.AreEqual(5, e.ExtraMoves);
            Assert.AreEqual(30, e.WinCoins["normal"]);
            Assert.AreEqual(10, e.UnlockLevel("hammer"), "levels v3b: the Hammer opens with the first wrap level (CL-V3-07)");
            Assert.AreEqual(12, e.UnlockLevel("thermos"));
            Assert.AreEqual("coins_1000", e.ReferencePack.Sku);
            Assert.AreEqual(GameSessionOfferMoves, e.ExtraMoves, "outOfMoves.extraMoves = the core's OFFER_MOVES (web tests/ui/windows.test.ts)");
            var d = EconomyConfig.FromText(null);
            Assert.AreEqual(500, d.StartCoins);
        }

        private const int GameSessionOfferMoves = MinikUsta.Core.GameSession.OfferMoves;

        [Test]
        public void K29_lifetime_first_offer_is_a_gift_without_price_or_ad()
        {
            var m = Offers.Model(1, true, true, 0, AdAvailability.None, Eco);
            Assert.AreEqual(CoinOptionKind.Gift, m.Coin);
            Assert.AreEqual(0, m.Paid);
            Assert.IsNull(m.Ad, "META §3.2: no ad option on the gift");
            Assert.IsTrue(m.CoinEnabled);
            Assert.AreEqual(Offers.CountKey, m.CounterKey);
        }

        [Test]
        public void K29_offers_escalate_900_1350_1800_and_the_third_is_the_last()
        {
            var e = Eco;
            var o1 = Offers.Model(1, true, false, 5000, AdAvailability.None, e);
            var o2 = Offers.Model(2, false, false, 5000, AdAvailability.None, e);
            var o3 = Offers.Model(3, false, false, 5000, AdAvailability.None, e);
            Assert.AreEqual((CoinOptionKind.Buy, 900), (o1.Coin, o1.Price));
            Assert.AreEqual(1350, o2.Price);
            Assert.AreEqual(1800, o3.Price);
            Assert.AreEqual(Offers.LastKey, o3.CounterKey, "STORY §7.3: 'son teklif' only on offer 3");
            Assert.IsNotNull(o1.Ad, "the ad option exists on offer 1 (never hidden)");
            Assert.IsNull(o2.Ad);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Offers.Model(4, false, false, 0, AdAvailability.None, e));
        }

        [Test]
        public void K29_short_of_coins_the_coin_option_is_passive_and_the_ad_option_is_grey_without_a_provider()
        {
            var m = Offers.Model(1, true, false, 350, AdAvailability.None, Eco);
            Assert.AreEqual(CoinOptionKind.Short, m.Coin);
            Assert.AreEqual(550, m.Missing);
            Assert.IsFalse(m.CoinEnabled, "UX §7: 'Altın yetmiyor' grey and passive (no shop yet)");
            Assert.IsFalse(m.Ad!.Enabled);
            Assert.AreEqual("ads.none", m.Ad.LineKey);
            var today = Offers.Model(1, true, false, 5000, AdAvailability.Watched(1), Eco);
            Assert.IsTrue(today.Ad!.Enabled);
            Assert.AreEqual(("lose.adToday", 2, 3), (today.Ad.LineKey, today.Ad.TodayN, today.Ad.TodayMax));
            var capped = Offers.Model(1, true, false, 5000, AdAvailability.Watched(3), Eco);
            Assert.AreEqual("ads.tomorrow", capped.Ad!.LineKey);
        }

        [Test]
        public void K28_win_rewards_base_plus_bonus_moves_capped_plus_leftover_trowels_and_one_star()
        {
            var r = Rewards.Win("normal", 7, 1, true, Eco);
            Assert.AreEqual(30, r.BaseCoins);
            Assert.AreEqual(7, r.BonusMoves);
            Assert.AreEqual(21, r.BonusCoins);
            Assert.AreEqual(10, r.TrowelCoins);
            Assert.AreEqual(61, r.TotalCoins, "UX §6 example: 30 + 21 + 10");
            Assert.AreEqual(1, r.Stars);
            var capped = Rewards.Win("easy", 25, 0, true, Eco);
            Assert.AreEqual(10, capped.BonusMoves, "bonusMaxMovesCounted");
            Assert.AreEqual(20 + 30, capped.TotalCoins);
            Assert.AreEqual(0, Rewards.Win("hard", 3, 0, false, Eco).Stars, "META §1: a repeat win gives no star");
        }

        [TestCase(900, "tr", "≈ ₺81")]
        [TestCase(900, "en", "≈ $1.79")]
        [TestCase(1350, "tr", "≈ ₺121")]
        [TestCase(1350, "en", "≈ $2.69")]
        [TestCase(1800, "tr", "≈ ₺162")]
        [TestCase(1800, "en", "≈ $3.58")]
        [TestCase(350, "en", "≈ $0.70")]
        [TestCase(20000, "tr", "≈ ₺1.800")]
        [TestCase(20000, "en", "≈ $39.80")]
        public void UX03_price_label_second_line_matches_the_web_intl_format(int coins, string lang, string want)
        {
            Assert.AreEqual(want, Price.RealMoneyText(coins, lang, Eco.ReferencePack));
        }

        [Test]
        public void UX03_grouped_counters_tr_dot_en_comma()
        {
            Assert.AreEqual("1.250", Price.Grouped(1250, "tr"));
            Assert.AreEqual("1,250", Price.Grouped(1250, "en"));
            Assert.AreEqual("500", Price.Grouped(500, "tr"));
            Assert.AreEqual("1.234.567", Price.Grouped(1234567, "tr"));
        }
    }
}
