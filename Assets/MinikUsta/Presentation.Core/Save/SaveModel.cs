#nullable enable
// The Unity save (paket V1; web src/services/save.ts reduced to what the Unity build has: progress, stars, coins, lives,
// boosters, settings, the K-29 gift flag and the K-43 in-level record). Plain fields; `SaveCodec` writes / reads JSON with
// the core JSON writer / reader. The engine stores the text (Unity: PlayerPrefs; tests: `MemorySaveStore`).
using System.Collections.Generic;
using MinikUsta.Core;

namespace MinikUsta.Presentation.Save
{
    public sealed class SaveSettings
    {
        public bool Sound = true;
        public bool Music = true;
        public bool Haptics = true;
        public string Lang = "tr";
        public bool ReduceMotion;
        public bool Colorblind;

        public SaveSettings Clone() => (SaveSettings)MemberwiseClone();
    }

    public sealed class LevelRecord
    {
        public bool Won;
        public int Attempts;
    }

    /// <summary>
    /// K-43 running attempt (TECH §11.1 `InLevel`): the move log (`start` first) + the level hash and rules version that the log
    /// was played on; offer counters; the open out-of-moves window. A hash / version mismatch at launch voids it penalty-free.
    /// </summary>
    public sealed class InLevelRecord
    {
        public int LevelId;
        public List<SessionAction> Actions = new List<SessionAction>();
        public string LevelHash = "";
        public int RulesVersion = GameSession.RulesVersion;
        public int OffersUsed;
        public int OfferSpendCoins;
        /// <summary>"none" | "outOfMoves" (the K-29 window was open).</summary>
        public string OutcomeWindow = "none";
        /// <summary>The attempt holds a reserved life (META §2: given back on a win or a free exit).</summary>
        public bool LifeReserved;
    }

    public sealed class SaveModel
    {
        /// <summary>Highest story level won (0 = none yet).</summary>
        public int HighestLevel;
        /// <summary>The level last started (the 1–10 loop continues after it once every level is won, UX §6).</summary>
        public int LastLevel;
        public readonly SortedDictionary<int, LevelRecord> Levels = new SortedDictionary<int, LevelRecord>();
        public int Stars;
        public int Coins;
        /// <summary>Lives not reserved by a running attempt.</summary>
        public int Lives;
        public int WinStreak;
        public readonly SortedDictionary<string, int> Boosters = new SortedDictionary<string, int>();
        /// <summary>Boosters whose free trials were granted at unlock (META §4).</summary>
        public readonly SortedSet<string> FreeTrialsGranted = new SortedSet<string>();
        public SaveSettings Settings = new SaveSettings();
        /// <summary>K-29 lifetime-first +5 gift used.</summary>
        public bool FirstOfferGiftUsed;
        public InLevelRecord? InLevel;

        public static SaveModel Default(int coins, int lives) => new SaveModel { Coins = coins, Lives = lives };

        public LevelRecord Level(int n)
        {
            if (!Levels.TryGetValue(n, out var r))
            {
                r = new LevelRecord();
                Levels[n] = r;
            }
            return r;
        }

        public bool Won(int n) => Levels.TryGetValue(n, out var r) && r.Won;

        /// <summary>Distinct levels won (the home progress "n/10", UX §3 Faz 2R fallback).</summary>
        public int WonCount
        {
            get
            {
                int n = 0;
                foreach (var r in Levels.Values) if (r.Won) n++;
                return n;
            }
        }

        public int BoosterCount(string id) => Boosters.TryGetValue(id, out int n) ? n : 0;
    }

    /// <summary>Where the save text lives (Unity: PlayerPrefs).</summary>
    public interface ISaveStore
    {
        string? Read(string key);
        void Write(string key, string text);
        void Delete(string key);
    }

    public sealed class MemorySaveStore : ISaveStore
    {
        public readonly Dictionary<string, string> Map = new Dictionary<string, string>();
        public int Writes;

        public string? Read(string key) => Map.TryGetValue(key, out var v) ? v : null;

        public void Write(string key, string text)
        {
            Map[key] = text;
            Writes++;
        }

        public void Delete(string key) => Map.Remove(key);
    }
}
