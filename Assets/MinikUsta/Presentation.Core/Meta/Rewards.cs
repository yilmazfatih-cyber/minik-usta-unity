#nullable enable
// Win rewards — port of web src/ui/rewards.ts (GDD K-28; UX_FLOWS §6; META §3.1; JUICE #56). Pure: amounts from
// config/economy.json `levelRewards` and `stars` (D-017).
//   base = winCoins[difficulty] · Bonus İnşaat = min(movesLeft, bonusMaxMovesCounted) × bonusCoinsPerMoveLeft ·
//   leftover Golden Trowels × coinsPerLeftoverTrowel · stars = stars.perWin (a repeat win of a won level: no star, META §1).
using System;

namespace MinikUsta.Presentation.Meta
{
    public sealed class WinRewards
    {
        public readonly int BaseCoins;
        /// <summary>Moves that turn into coins (`win.bonus {n}`).</summary>
        public readonly int BonusMoves;
        public readonly int BonusCoins;
        /// <summary>`win.trowel {n}` (the line only when &gt; 0).</summary>
        public readonly int Trowels;
        public readonly int TrowelCoins;
        public readonly int Stars;

        public WinRewards(int baseCoins, int bonusMoves, int bonusCoins, int trowels, int trowelCoins, int stars)
        {
            BaseCoins = baseCoins;
            BonusMoves = bonusMoves;
            BonusCoins = bonusCoins;
            Trowels = trowels;
            TrowelCoins = trowelCoins;
            Stars = stars;
        }

        public int TotalCoins => BaseCoins + BonusCoins + TrowelCoins;
    }

    public static class Rewards
    {
        /// <summary>web `winRewards`; `firstWin` false (a won level played again) gives no star (META §1).</summary>
        public static WinRewards Win(string difficulty, int movesLeft, int trowels, bool firstWin, EconomyConfig eco)
        {
            int baseCoins = eco.WinCoins.TryGetValue(difficulty, out int b) ? b : 0;
            int bonusMoves = Math.Max(0, Math.Min(movesLeft, eco.BonusMaxMovesCounted));
            int t = Math.Max(0, trowels);
            return new WinRewards(baseCoins, bonusMoves, bonusMoves * eco.BonusCoinsPerMoveLeft, t, t * eco.CoinsPerLeftoverTrowel, firstWin ? eco.StarsPerWin : 0);
        }
    }
}
