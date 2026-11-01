#nullable enable
// Sound identifiers (paket V2): the names of audio.sfx / audio.seq in the design tokens (Resources/audio/sounds.json). A test
// checks that this list and the recipe file name exactly the same sounds, so a typo here cannot stay silent.
using System.Collections.Generic;

namespace MinikUsta.Presentation.Audio
{
    public static class Sounds
    {
        // audio.sfx — one ZzFX call each
        public const string Pick = "sfx_pick";
        public const string Blocked = "sfx_blocked";
        public const string Bump = "sfx_bump";
        public const string Whoosh = "sfx_whoosh";
        public const string GhostOk = "sfx_ghost_ok";
        public const string Cancel = "sfx_cancel";
        public const string SetYard = "sfx_set_yard";
        public const string Fall = "sfx_fall";
        public const string Land = "sfx_land";
        public const string PlaceOk = "sfx_place_ok";
        public const string PlaceBad = "sfx_place_bad";
        public const string Bounce = "sfx_bounce";
        public const string Queue = "sfx_queue";
        public const string Offer = "sfx_offer";
        public const string Coin = "sfx_coin";
        public const string LifeLost = "sfx_life_lost";
        public const string Button = "sfx_button";
        public const string Popup = "sfx_popup";
        public const string Close = "sfx_close";
        public const string Tick = "sfx_tick";
        public const string StreakPip = "sfx_streak_pip";
        public const string Trowel = "sfx_trowel";
        public const string Clamp = "sfx_clamp";
        public const string Teardown = "sfx_teardown";

        // audio.seq — mixed sequences
        public const string Segment = "sfx_segment";
        public const string Combo = "sfx_combo";
        public const string TruckHorn = "sfx_truck_horn";
        public const string LastMoves = "sfx_lastmoves";
        public const string MovesAdd = "sfx_moves_add";
        public const string GoalDone = "sfx_goal_done";
        public const string OutOfMoves = "sfx_out_of_moves";
        public const string Chest = "sfx_chest";
        public const string MusicWin = "music_win";
        public const string GapRail = "sfx_gap_rail";
        public const string Locked = "sfx_locked";
        public const string Hammer = "sfx_hammer";

        /// <summary>Every name above.</summary>
        public static readonly string[] All =
        {
            Pick, Blocked, Bump, Whoosh, GhostOk, Cancel, SetYard, Fall, Land, PlaceOk, PlaceBad, Bounce, Queue, Offer, Coin, LifeLost,
            Button, Popup, Close, Tick, StreakPip, Trowel, Clamp, Teardown,
            Segment, Combo, TruckHorn, LastMoves, MovesAdd, GoalDone, OutOfMoves, Chest, MusicWin, GapRail, Locked, Hammer,
        };

        public static IReadOnlyList<string> Names => All;
    }
}
