#nullable enable
// Port of tests/core/types.test.ts
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class TypesTests
    {
        [Test]
        public void Colour_codes_keep_the_BRIEF_6_order_internal_index_D2_help_order_WYGROCBP()
        {
            Assert.AreEqual("WYGROCBP", string.Join("", ColorCodes.All));
        }

        [Test(Description = "K-44 thirteen shape kinds")]
        public void K44_thirteen_shape_kinds()
        {
            Assert.AreEqual(13, ShapeKinds.All.Length);
        }

        [Test(Description = "K-34 verdict reasons have the fixed GDD order")]
        public void K34_verdict_reasons_have_the_fixed_GDD_order()
        {
            CollectionAssert.AreEqual(new[] { "debris", "outside", "window", "color", "support" }, VerdictReasons.All);
        }

        [Test]
        public void Zones_and_piece_flags()
        {
            Assert.AreEqual(0, Zone.Yard);
            Assert.AreEqual(1, Zone.Site);
            Assert.AreEqual(2, Zone.Queue);
            Assert.AreEqual(3, Zone.Gone);
            Assert.AreEqual(4, Zone.Pending);
            CollectionAssert.AreEqual(new[] { "yard", "site", "queue", "gone", "pending" }, Zone.Names);
            CollectionAssert.AreEqual(new[] { "glass", "mortar", "balloon", "chained", "wet", "locked", "debris", "stuck", "wrapped", "scissors", "bonus", "slippery" }, PieceFlags.All);
            CollectionAssert.AreEqual(new[] { "glass", "mortar", "balloon", "chained", "wet", "wrapped", "scissors", "bonus", "slippery" }, PieceFlags.DataFlags);
        }

        [Test]
        public void Event_kinds_cover_the_44_TS_event_types_with_distinct_names()
        {
            Assert.AreEqual(44, EventTypes.All.Length);
            Assert.AreEqual(44, Enum.GetValues(typeof(EventKind)).Length);
            Assert.AreEqual(44, new HashSet<string>(EventTypes.All).Count);
            // every concrete event class reports the name of its kind
            GameEvent[] samples =
            {
                new MoveCancelledEvent(0, CancelReasons.SameSpot), new PieceMovedEvent(0, At.Yard(0, 0), At.Yard(1, 0), EntryKinds.Yard),
                new LevelWonEvent(3), new OutOfMovesEvent(), new TeardownEvent(1, new TeardownMove[0], TeardownCauses.Color),
                new ColorsSwappedEvent(0, 1, "W", "Y"), new PieceLiftedEvent(0, "crane", At.Yard(0, 0), At.Site(6, 0, 0), "B1_0"),
            };
            foreach (var e in samples) Assert.AreEqual(EventTypes.NameOf(e.T), e.TypeName);
        }

        [Test]
        public void Session_action_kinds_match_the_TS_literals()
        {
            Assert.AreEqual("drag", new DragMove(0, new DragNode(0, 0, DragMode.Free)).Kind);
            Assert.AreEqual("hammer", new HammerMove(HammerTarget.Piece(1)).Kind);
            Assert.AreEqual("crane", new CraneMove(0, At.ZoneSite, 6, 0, 90).Kind);
            Assert.AreEqual("paint", new PaintMove(0, 1).Kind);
            Assert.AreEqual("goldTrowel", new GoldTrowelMove(0, 6, 0).Kind);
            Assert.AreEqual("trowel", new TrowelMove(0, 1, 0).Kind);
            Assert.AreEqual("addMoves", new AddMovesMove(5, AddMovesSource.Thermos).Kind);
            Assert.AreEqual("undo", new UndoAction().Kind);
            Assert.AreEqual("start", new StartAction(new string[0], 0).Kind);
        }
    }
}
