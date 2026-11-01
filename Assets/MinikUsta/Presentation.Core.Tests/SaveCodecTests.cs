#nullable enable
// Paket V1: the Unity save (web src/services/save.ts reduced): JSON round trip, version gate, corrupt text → backup → defaults,
// a bad field never throws the save away, and the K-43 in-level record replays the attempt exactly.
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Presentation.Save;
using MinikUsta.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public class SaveCodecTests
    {
        private static SaveModel Defaults => SaveModel.Default(500, 5);

        private static SaveModel Sample()
        {
            var m = SaveModel.Default(1234, 3);
            m.HighestLevel = 4;
            m.LastLevel = 5;
            m.Level(1).Won = true;
            m.Level(1).Attempts = 2;
            m.Level(5).Attempts = 1;
            m.Stars = 4;
            m.WinStreak = 2;
            m.Boosters["hammer"] = 3;
            m.FreeTrialsGranted.Add("hammer");
            m.Settings.Lang = "en";
            m.Settings.ReduceMotion = true;
            m.Settings.Haptics = false;
            m.FirstOfferGiftUsed = true;
            var lvl = Play.LevelFile(3);
            var session = GameSession.Start(lvl);
            foreach (var mv in Play.SolverGolden(3).Drags.Take(2)) session.Commit(mv);
            m.InLevel = new InLevelRecord { LevelId = 3, LevelHash = GameSession.LevelHash(lvl.Data), OffersUsed = 0, OfferSpendCoins = 0, LifeReserved = true };
            m.InLevel.Actions.AddRange(session.Log);
            return m;
        }

        [Test]
        public void K43_save_round_trips_every_field_and_the_text_is_canonical()
        {
            var m = Sample();
            string text = SaveCodec.Encode(m);
            var back = SaveCodec.Decode(text, Defaults, out bool dropped, out string? error);
            Assert.IsNotNull(back, error);
            Assert.IsFalse(dropped);
            Assert.AreEqual(text, SaveCodec.Encode(back!), "decode ∘ encode is the identity on the text");
            Assert.AreEqual((4, 5, 4, 1234, 3, 2), (back!.HighestLevel, back.LastLevel, back.Stars, back.Coins, back.Lives, back.WinStreak));
            Assert.IsTrue(back.Won(1));
            Assert.AreEqual(2, back.Level(1).Attempts);
            Assert.AreEqual(3, back.BoosterCount("hammer"));
            Assert.AreEqual(("en", true, false), (back.Settings.Lang, back.Settings.ReduceMotion, back.Settings.Haptics));
            Assert.IsTrue(back.FirstOfferGiftUsed);
            Assert.AreEqual(3, back.InLevel!.Actions.Count, "start + 2 moves");
            Assert.IsTrue(SessionActionJson.SameLog(m.InLevel!.Actions, back.InLevel.Actions));
            Assert.IsTrue(text.StartsWith("{\"data\":"), "canonical: sorted keys");
        }

        [Test]
        public void K43_the_saved_log_replays_to_the_same_state()
        {
            var m = Sample();
            var back = SaveCodec.Decode(SaveCodec.Encode(m), Defaults, out _, out _)!;
            var lvl = Play.LevelFile(3);
            var live = GameSession.Start(lvl);
            foreach (var mv in Play.SolverGolden(3).Drags.Take(2)) live.Commit(mv);
            var replay = GameSession.Replay(lvl, back.InLevel!.Actions);
            Assert.AreEqual(live.State.Buf, replay.State.Buf, "the resumed attempt is bit-identical");
            Assert.AreEqual(back.InLevel.LevelHash, GameSession.LevelHash(lvl.Data));
        }

        [Test]
        public void SAVE_unreadable_text_falls_back_to_the_backup_then_to_the_defaults()
        {
            var store = new MemorySaveStore();
            Assert.AreEqual(SaveLoadStatus.Fresh, SaveCodec.Load(store, Defaults).Status);
            var m = Sample();
            SaveCodec.Write(store, m);
            Assert.AreEqual(store.Map[SaveCodec.Key], store.Map[SaveCodec.BackupKey], "every write copies the backup");
            store.Map[SaveCodec.Key] = "{not json";
            var load = SaveCodec.Load(store, Defaults);
            Assert.AreEqual(SaveLoadStatus.FromBackup, load.Status);
            Assert.AreEqual(1234, load.Data.Coins);
            Assert.AreEqual("{not json", store.Map[SaveCodec.CorruptKey], "the corrupt text is kept (local only)");
            store.Map[SaveCodec.BackupKey] = "[]";
            var none = SaveCodec.Load(store, Defaults);
            Assert.AreEqual(SaveLoadStatus.Corrupt, none.Status);
            Assert.AreEqual(500, none.Data.Coins, "defaults");
        }

        [Test]
        public void SAVE_a_newer_version_is_not_guessed_a_bad_field_keeps_its_default_a_bad_log_drops_only_the_attempt()
        {
            Assert.IsNull(SaveCodec.Decode("{\"v\":2,\"data\":{}}", Defaults, out _, out string? err), "newer than this build");
            StringAssert.Contains("version", err);
            var m = SaveCodec.Decode("{\"v\":1,\"data\":{\"coins\":\"many\",\"lives\":-3,\"stars\":7,\"settings\":{\"lang\":\"de\"}}}", Defaults, out _, out _)!;
            Assert.AreEqual((500, 5, 7, "tr"), (m.Coins, m.Lives, m.Stars, m.Settings.Lang), "wrong-typed / negative / unknown values keep the defaults");
            var bad = SaveCodec.Decode("{\"v\":1,\"data\":{\"coins\":9,\"inLevel\":{\"levelId\":3,\"levelHash\":\"x\",\"actions\":[{\"kind\":\"teleport\"}]}}}", Defaults, out bool dropped, out _)!;
            Assert.IsTrue(dropped);
            Assert.IsNull(bad.InLevel);
            Assert.AreEqual(9, bad.Coins, "the rest of the save is kept");
        }
    }
}
