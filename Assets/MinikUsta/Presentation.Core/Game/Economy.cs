#nullable enable
// The economy values the level screen shows (Faz 3 Q2; docs/META.md §3, config/economy.json `levelRewards`): the gold a
// left-over move turns into (#56 Bonus İnşaat). The level screen only DISPLAYS them: the wallet and the real payout belong
// to the meta layer (Faz 4).
using MinikUsta.Core.Json;

namespace MinikUsta.Presentation.Game
{
    public sealed class EconomyInfo
    {
        /// <summary>economy.json `levelRewards.bonusCoinsPerMoveLeft` (META §3.1).</summary>
        public int BonusCoinsPerMoveLeft = 3;
        /// <summary>economy.json `levelRewards.bonusMaxMovesCounted`.</summary>
        public int BonusMaxMovesCounted = 10;

        /// <summary>The values of `config/economy.json`; a missing file or key keeps the defaults above (the same numbers).</summary>
        public static EconomyInfo FromText(string? json)
        {
            var o = new EconomyInfo();
            if (json == null) return o;
            var rewards = JsonReader.Parse(json)["levelRewards"];
            if (rewards == null || !rewards.IsObject) return o;
            var perMove = rewards["bonusCoinsPerMoveLeft"];
            if (perMove != null && perMove.IsInteger) o.BonusCoinsPerMoveLeft = perMove.AsInt;
            var max = rewards["bonusMaxMovesCounted"];
            if (max != null && max.IsInteger) o.BonusMaxMovesCounted = max.AsInt;
            return o;
        }
    }
}
