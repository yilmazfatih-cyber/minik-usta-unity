#nullable enable
// Save JSON (paket V1; web src/services/save.ts `{ v, data }` document, schema reduced to `SaveModel`). Written with the core
// JSON writer (sorted keys: the same model gives the same text), read with the core reader. Robust by rule:
//   - unreadable text (not JSON, not an object, no `data`) → `Corrupt` + the defaults;
//   - a NEWER version than this build → `Corrupt` + the defaults (never guess a future shape);
//   - a missing / wrong-typed field → that field's default (one bad value never throws the whole save away);
//   - an in-level record that does not read (unknown action, bad log) → dropped (`InLevelDropped`), the rest kept.
// Every write also copies the text to `BackupKey`; a main text that does not read is kept under `CorruptKey`.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Core.Json;

namespace MinikUsta.Presentation.Save
{
    public enum SaveLoadStatus
    {
        /// <summary>No save yet (first launch).</summary>
        Fresh,
        Ok,
        /// <summary>The main text did not read; the backup did.</summary>
        FromBackup,
        /// <summary>Neither read: defaults.</summary>
        Corrupt,
    }

    public sealed class SaveLoad
    {
        public readonly SaveModel Data;
        public readonly SaveLoadStatus Status;
        public readonly bool InLevelDropped;
        public readonly string? Error;

        public SaveLoad(SaveModel data, SaveLoadStatus status, bool inLevelDropped, string? error)
        {
            Data = data;
            Status = status;
            InLevelDropped = inLevelDropped;
            Error = error;
        }
    }

    public static class SaveCodec
    {
        public const string Key = "minikusta.save";
        public const string BackupKey = "minikusta.save.bak";
        public const string CorruptKey = "minikusta.save.corrupt";
        /// <summary>Version of the Unity save document (independent of the web save's v2).</summary>
        public const int Version = 1;

        // --- write ------------------------------------------------------------------------------------------------------

        public static JsonValue ToJson(SaveModel m)
        {
            var levels = JsonValue.Object();
            foreach (var kv in m.Levels)
                levels.Set(kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    JsonValue.Object().Set("won", JsonValue.Of(kv.Value.Won)).Set("attempts", JsonValue.Of(kv.Value.Attempts)));
            var boosters = JsonValue.Object();
            foreach (var kv in m.Boosters) boosters.Set(kv.Key, JsonValue.Of(kv.Value));
            var granted = JsonValue.Array();
            foreach (var g in m.FreeTrialsGranted) granted.Add(JsonValue.Of(g));
            var s = m.Settings;
            var settings = JsonValue.Object()
                .Set("sound", JsonValue.Of(s.Sound)).Set("music", JsonValue.Of(s.Music)).Set("haptics", JsonValue.Of(s.Haptics))
                .Set("lang", JsonValue.Of(s.Lang)).Set("reduceMotion", JsonValue.Of(s.ReduceMotion)).Set("colorblind", JsonValue.Of(s.Colorblind));
            var data = JsonValue.Object()
                .Set("progress", JsonValue.Object().Set("highestLevel", JsonValue.Of(m.HighestLevel)).Set("levels", levels))
                .Set("stars", JsonValue.Of(m.Stars))
                .Set("coins", JsonValue.Of(m.Coins))
                .Set("lives", JsonValue.Of(m.Lives))
                .Set("winStreak", JsonValue.Of(m.WinStreak))
                .Set("boosters", JsonValue.Object().Set("inventory", boosters).Set("freeTrialsGranted", granted))
                .Set("settings", settings)
                .Set("firstOfferGiftUsed", JsonValue.Of(m.FirstOfferGiftUsed))
                .Set("inLevel", m.InLevel == null ? JsonValue.Null : InLevelJson(m.InLevel));
            return JsonValue.Object().Set("v", JsonValue.Of(Version)).Set("data", data);
        }

        private static JsonValue InLevelJson(InLevelRecord r) => JsonValue.Object()
            .Set("levelId", JsonValue.Of(r.LevelId))
            .Set("actions", SessionActionJson.ToJson(r.Actions))
            .Set("levelHash", JsonValue.Of(r.LevelHash))
            .Set("rulesVersion", JsonValue.Of(r.RulesVersion))
            .Set("offersUsed", JsonValue.Of(r.OffersUsed))
            .Set("offerSpendCoins", JsonValue.Of(r.OfferSpendCoins))
            .Set("outcomeWindow", JsonValue.Of(r.OutcomeWindow))
            .Set("lifeReserved", JsonValue.Of(r.LifeReserved));

        public static string Encode(SaveModel m) => JsonWriter.Canonical(ToJson(m));

        // --- read -------------------------------------------------------------------------------------------------------

        /// <summary>Decodes one text; null + `error` when it cannot be read at all (not JSON, wrong shape, newer version).</summary>
        public static SaveModel? Decode(string text, SaveModel defaults, out bool inLevelDropped, out string? error)
        {
            inLevelDropped = false;
            error = null;
            JsonValue root;
            try
            {
                root = JsonReader.Parse(text);
            }
            catch (Exception e)
            {
                error = "parse: " + e.Message;
                return null;
            }
            if (!root.IsObject || root["data"] == null || !root["data"]!.IsObject)
            {
                error = "shape: no data object";
                return null;
            }
            var vv = root["v"];
            int v = vv != null && vv.IsInteger ? vv.AsInt : 0;
            if (v < 1 || v > Version)
            {
                error = "version " + v + " (this build reads 1…" + Version + ")";
                return null;
            }
            var d = root["data"]!;
            var m = new SaveModel();
            var progress = d["progress"];
            m.HighestLevel = NonNeg(progress, "highestLevel", defaults.HighestLevel);
            var levels = progress != null && progress.IsObject ? progress["levels"] : null;
            if (levels != null && levels.IsObject)
                foreach (var kv in levels.Members)
                {
                    if (!int.TryParse(kv.Key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n) || n < 1) continue;
                    if (!kv.Value.IsObject) continue;
                    var won = kv.Value["won"];
                    m.Levels[n] = new LevelRecord { Won = won != null && won.IsBool && won.AsBool, Attempts = NonNeg(kv.Value, "attempts", 0) };
                }
            m.Stars = NonNeg(d, "stars", defaults.Stars);
            m.Coins = NonNeg(d, "coins", defaults.Coins);
            m.Lives = NonNeg(d, "lives", defaults.Lives);
            m.WinStreak = NonNeg(d, "winStreak", defaults.WinStreak);
            var boosters = d["boosters"];
            var inv = boosters != null && boosters.IsObject ? boosters["inventory"] : null;
            if (inv != null && inv.IsObject)
                foreach (var kv in inv.Members)
                    if (kv.Value.IsInteger && kv.Value.AsInt >= 0) m.Boosters[kv.Key] = kv.Value.AsInt;
            var granted = boosters != null && boosters.IsObject ? boosters["freeTrialsGranted"] : null;
            if (granted != null && granted.IsArray)
                foreach (var g in granted.Items)
                    if (g.IsString) m.FreeTrialsGranted.Add(g.AsString);
            var st = d["settings"];
            var ds = defaults.Settings;
            m.Settings = new SaveSettings
            {
                Sound = Bool(st, "sound", ds.Sound),
                Music = Bool(st, "music", ds.Music),
                Haptics = Bool(st, "haptics", ds.Haptics),
                Lang = Lang(st, ds.Lang),
                ReduceMotion = Bool(st, "reduceMotion", ds.ReduceMotion),
                Colorblind = Bool(st, "colorblind", ds.Colorblind),
            };
            m.FirstOfferGiftUsed = Bool(d, "firstOfferGiftUsed", false);
            var il = d["inLevel"];
            if (il != null && !il.IsNull)
            {
                m.InLevel = ReadInLevel(il);
                inLevelDropped = m.InLevel == null;
            }
            return m;
        }

        private static InLevelRecord? ReadInLevel(JsonValue il)
        {
            try
            {
                if (!il.IsObject) return null;
                int level = NonNeg(il, "levelId", 0);
                var actions = il["actions"];
                var hash = il["levelHash"];
                if (level < 1 || actions == null || !actions.IsArray || hash == null || !hash.IsString) return null;
                var log = SessionActionJson.LogFromJson(actions);
                if (log.Length == 0 || !(log[0] is StartAction)) return null;
                var window = il["outcomeWindow"];
                var res = new InLevelRecord
                {
                    LevelId = level,
                    LevelHash = hash.AsString,
                    RulesVersion = NonNeg(il, "rulesVersion", 0),
                    OffersUsed = NonNeg(il, "offersUsed", 0),
                    OfferSpendCoins = NonNeg(il, "offerSpendCoins", 0),
                    OutcomeWindow = window != null && window.IsString && window.AsString == "outOfMoves" ? "outOfMoves" : "none",
                    LifeReserved = Bool(il, "lifeReserved", false),
                };
                res.Actions.AddRange(log);
                return res;
            }
            catch (Exception)
            {
                return null; // an action this build cannot read: the attempt is dropped, the save is kept
            }
        }

        private static int NonNeg(JsonValue? o, string key, int fallback)
        {
            var v = o != null && o.IsObject ? o[key] : null;
            return v != null && v.IsInteger && v.AsInt >= 0 ? v.AsInt : fallback;
        }

        private static bool Bool(JsonValue? o, string key, bool fallback)
        {
            var v = o != null && o.IsObject ? o[key] : null;
            return v != null && v.IsBool ? v.AsBool : fallback;
        }

        private static string Lang(JsonValue? o, string fallback)
        {
            var v = o != null && o.IsObject ? o["lang"] : null;
            return v != null && v.IsString && (v.AsString == "tr" || v.AsString == "en") ? v.AsString : fallback;
        }

        // --- store ------------------------------------------------------------------------------------------------------

        /// <summary>Main text → backup → defaults; a corrupt main text is kept under `CorruptKey` (local only).</summary>
        public static SaveLoad Load(ISaveStore store, SaveModel defaults)
        {
            string? main = store.Read(Key);
            if (main == null) return new SaveLoad(defaults, SaveLoadStatus.Fresh, false, null);
            var m = Decode(main, defaults, out bool dropped, out string? error);
            if (m != null) return new SaveLoad(m, SaveLoadStatus.Ok, dropped, null);
            store.Write(CorruptKey, main);
            string? bak = store.Read(BackupKey);
            if (bak != null)
            {
                var b = Decode(bak, defaults, out bool dropped2, out _);
                if (b != null) return new SaveLoad(b, SaveLoadStatus.FromBackup, dropped2, error);
            }
            return new SaveLoad(defaults, SaveLoadStatus.Corrupt, false, error);
        }

        /// <summary>Writes the save, then the same text as the backup (web: "a copy of the last successful write").</summary>
        public static void Write(ISaveStore store, SaveModel m)
        {
            string text = Encode(m);
            store.Write(Key, text);
            store.Write(BackupKey, text);
        }
    }
}
