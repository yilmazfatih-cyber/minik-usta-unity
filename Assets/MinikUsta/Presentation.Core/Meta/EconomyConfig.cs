#nullable enable
// config/economy.json values the Unity meta layer needs (paket V1; docs/META.md §2–§4, D-017: numbers never in code or texts).
// Read with the core JSON reader; a missing key keeps the default below (the same numbers as the web copy, version 3).
using System;
using System.Collections.Generic;
using MinikUsta.Core.Json;

namespace MinikUsta.Presentation.Meta
{
    public sealed class CoinPack
    {
        public readonly string Sku;
        public readonly int Coins;
        public readonly double Usd;
        public readonly double Try;

        public CoinPack(string sku, int coins, double usd, double tryPrice)
        {
            Sku = sku;
            Coins = coins;
            Usd = usd;
            Try = tryPrice;
        }
    }

    public sealed class BoosterInfo
    {
        public readonly string Id;
        public readonly int UnlockLevel;
        public readonly int FreeTrials;
        public readonly int Price;

        public BoosterInfo(string id, int unlockLevel, int freeTrials, int price)
        {
            Id = id;
            UnlockLevel = unlockLevel;
            FreeTrials = freeTrials;
            Price = price;
        }
    }

    public sealed class EconomyConfig
    {
        // startingWallet
        public int StartCoins = 500;
        public int StartLives = 5;
        // lives
        public int LivesMax = 5;
        // stars
        public int StarsPerWin = 1;
        // levelRewards
        public readonly Dictionary<string, int> WinCoins = new Dictionary<string, int> { { "easy", 20 }, { "normal", 30 }, { "hard", 50 }, { "superhard", 75 } };
        public int BonusCoinsPerMoveLeft = 3;
        public int BonusMaxMovesCounted = 10;
        public int CoinsPerLeftoverTrowel = 10;
        // outOfMoves
        public int ExtraMoves = 5;
        public int[] OfferCosts = { 900, 1350, 1800 };
        public int MaxOffers = 3;
        public bool FirstEverOfferFree = true;
        public int AdExtraMoves = 5;
        public int AdPerDay = 3;
        // priceDisplay + shop
        public string ReferenceSku = "coins_1000";
        public readonly List<CoinPack> CoinPacks = new List<CoinPack> { new CoinPack("coins_1000", 1000, 1.99, 89.99) };
        // boosters (in-level K-54 slots + pre-level)
        public readonly Dictionary<string, BoosterInfo> Boosters = new Dictionary<string, BoosterInfo>
        {
            { "hammer", new BoosterInfo("hammer", 8, 3, 600) },
            { "crane", new BoosterInfo("crane", 10, 2, 900) },
            { "undo", new BoosterInfo("undo", 13, 3, 300) },
            { "paintBrush", new BoosterInfo("paintBrush", 22, 2, 450) },
            { "thermos", new BoosterInfo("thermos", 12, 3, 450) },
            { "trowelStart", new BoosterInfo("trowelStart", 16, 2, 450) },
            { "openShutter", new BoosterInfo("openShutter", 20, 2, 600) },
        };

        /// <summary>The reference pack of the real-money line (`priceDisplay.referenceSku`).</summary>
        public CoinPack ReferencePack
        {
            get
            {
                foreach (var p in CoinPacks) if (p.Sku == ReferenceSku) return p;
                throw new InvalidOperationException("economy: priceDisplay.referenceSku " + ReferenceSku + " is not a coin pack");
            }
        }

        public int UnlockLevel(string booster) => Boosters.TryGetValue(booster, out var b) ? b.UnlockLevel : int.MaxValue;

        public static EconomyConfig FromText(string? json)
        {
            var o = new EconomyConfig();
            if (json == null) return o;
            var r = JsonReader.Parse(json);
            var wallet = r["startingWallet"];
            o.StartCoins = Int(wallet, "coins", o.StartCoins);
            o.StartLives = Int(wallet, "lives", o.StartLives);
            o.LivesMax = Int(r["lives"], "max", o.LivesMax);
            o.StarsPerWin = Int(r["stars"], "perWin", o.StarsPerWin);
            var rewards = r["levelRewards"];
            var win = rewards?["winCoins"];
            if (win != null && win.IsObject)
                foreach (var kv in win.Members)
                    if (kv.Value.IsInteger) o.WinCoins[kv.Key] = kv.Value.AsInt;
            o.BonusCoinsPerMoveLeft = Int(rewards, "bonusCoinsPerMoveLeft", o.BonusCoinsPerMoveLeft);
            o.BonusMaxMovesCounted = Int(rewards, "bonusMaxMovesCounted", o.BonusMaxMovesCounted);
            o.CoinsPerLeftoverTrowel = Int(rewards, "coinsPerLeftoverTrowel", o.CoinsPerLeftoverTrowel);
            var oom = r["outOfMoves"];
            o.ExtraMoves = Int(oom, "extraMoves", o.ExtraMoves);
            var costs = oom?["offerCosts"];
            if (costs != null && costs.IsArray && costs.Count > 0)
            {
                var list = new List<int>();
                foreach (var c in costs.Items) if (c.IsInteger) list.Add(c.AsInt);
                o.OfferCosts = list.ToArray();
            }
            o.MaxOffers = Int(oom, "maxOffersPerAttempt", o.MaxOffers);
            var free = oom?["firstEverOfferFree"];
            if (free != null && free.IsBool) o.FirstEverOfferFree = free.AsBool;
            var ad = oom?["rewardedAdOffer"];
            o.AdExtraMoves = Int(ad, "extraMoves", o.AdExtraMoves);
            o.AdPerDay = Int(ad, "perDay", o.AdPerDay);
            var pd = r["priceDisplay"]?["referenceSku"];
            if (pd != null && pd.IsString) o.ReferenceSku = pd.AsString;
            var packs = r["shop"]?["coinPacks"];
            if (packs != null && packs.IsArray)
            {
                o.CoinPacks.Clear();
                foreach (var p in packs.Items)
                {
                    var sku = p["sku"];
                    if (sku == null || !sku.IsString) continue;
                    o.CoinPacks.Add(new CoinPack(sku.AsString, Int(p, "coins", 0), Num(p, "usd"), Num(p, "try")));
                }
            }
            var boosters = r["boosters"];
            if (boosters != null && boosters.IsObject)
                foreach (var kv in boosters.Members)
                {
                    if (!kv.Value.IsObject) continue;
                    o.Boosters.TryGetValue(kv.Key, out var d);
                    o.Boosters[kv.Key] = new BoosterInfo(kv.Key, Int(kv.Value, "unlockLevel", d?.UnlockLevel ?? int.MaxValue), Int(kv.Value, "freeTrials", d?.FreeTrials ?? 0),
                        Int(kv.Value, "price", d?.Price ?? 0));
                }
            return o;
        }

        private static int Int(JsonValue? o, string key, int fallback)
        {
            var v = o != null && o.IsObject ? o[key] : null;
            return v != null && v.IsInteger ? v.AsInt : fallback;
        }

        private static double Num(JsonValue? o, string key)
        {
            var v = o != null && o.IsObject ? o[key] : null;
            return v != null && v.IsNumber ? v.AsDouble : 0;
        }
    }
}
