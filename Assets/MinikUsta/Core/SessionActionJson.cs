#nullable enable
// JSON form of the move log entries (TECH_DESIGN §6.1, §11.1 `InLevel.actions`): the TS object literals of `Move` /
// `SessionAction` (types.ts) written and read without any serialiser dependency. `GameSession.log` copies actions through
// it (TS `copyAction` = `JSON.parse(JSON.stringify(a))`), the golden replays (tests/golden/*.hand.json `log`) and the save
// service read logs with `FromJson` (the lenient half of src/services/save.ts `asSessionActions`).
using System;
using MinikUsta.Core.Json;

namespace MinikUsta.Core
{
    public static class SessionActionJson
    {
        /// <summary>The action as its TS object literal (optional `via` / `steer` only when present).</summary>
        public static JsonValue ToJson(SessionAction a)
        {
            var o = JsonValue.Object().Set("kind", JsonValue.Of(a.Kind));
            switch (a)
            {
                case DragMove d:
                    o.Set("pieceId", JsonValue.Of(d.PieceId));
                    o.Set("to", JsonValue.Object().Set("ix", JsonValue.Of(d.To.Ix)).Set("iy", JsonValue.Of(d.To.Iy)).Set("mode", JsonValue.Of(d.To.Mode)));
                    if (d.Via.HasValue) o.Set("via", JsonValue.Of(d.Via.Value));
                    if (d.Steer.HasValue) o.Set("steer", JsonValue.Object().Set("dir", JsonValue.Of(d.Steer.Value.Dir)).Set("atRow", JsonValue.Of(d.Steer.Value.AtRow)));
                    break;
                case HammerMove h:
                    {
                        var t = JsonValue.Object();
                        if (h.Target.PieceId.HasValue) t.Set("pieceId", JsonValue.Of(h.Target.PieceId.Value));
                        if (h.Target.Obstacle.HasValue) t.Set("obstacle", JsonValue.Of(h.Target.Obstacle.Value));
                        o.Set("target", t);
                        break;
                    }
                case CraneMove c:
                    o.Set("pieceId", JsonValue.Of(c.PieceId));
                    o.Set("to", JsonValue.Object().Set("zone", JsonValue.Of(c.ToZone)).Set("x", JsonValue.Of(c.ToX)).Set("y", JsonValue.Of(c.ToY)));
                    o.Set("rotation", JsonValue.Of(c.Rotation));
                    break;
                case PaintMove p:
                    o.Set("a", JsonValue.Of(p.A)).Set("b", JsonValue.Of(p.B));
                    break;
                case GoldTrowelMove g:
                    o.Set("pieceId", JsonValue.Of(g.PieceId)).Set("x", JsonValue.Of(g.X)).Set("y", JsonValue.Of(g.Y));
                    break;
                case TrowelMove t:
                    o.Set("seg", JsonValue.Of(t.Seg)).Set("x", JsonValue.Of(t.X)).Set("y", JsonValue.Of(t.Y));
                    break;
                case AddMovesMove m:
                    o.Set("amount", JsonValue.Of(m.Amount)).Set("source", JsonValue.Of(m.Source));
                    break;
                case UndoAction _:
                    break;
                case StartAction st:
                    {
                        var pre = JsonValue.Array();
                        foreach (string b in st.PreBoosters) pre.Add(JsonValue.Of(b));
                        o.Set("preBoosters", pre).Set("streakTier", JsonValue.Of(st.StreakTier));
                        break;
                    }
                default:
                    throw new InvalidOperationException("SessionActionJson: unknown action " + a.Kind);
            }
            return o;
        }

        /// <summary>A log as a JSON array.</summary>
        public static JsonValue ToJson(System.Collections.Generic.IEnumerable<SessionAction> log)
        {
            var a = JsonValue.Array();
            foreach (var x in log) a.Add(ToJson(x));
            return a;
        }

        /// <summary>Reads one TS action literal; throws `FormatException` on an unknown kind or a missing field.</summary>
        public static SessionAction FromJson(JsonValue j)
        {
            if (!j.IsObject) throw new FormatException("action is not an object");
            string kind = Str(j, "kind");
            switch (kind)
            {
                case "drag":
                    {
                        var to = j["to"] ?? throw new FormatException("drag.to missing");
                        int? via = j["via"] is JsonValue v && v.IsNumber ? v.AsInt : (int?)null;
                        Steer? steer = null;
                        var st = j["steer"];
                        if (st != null && st.IsObject) steer = new Steer(Int(st, "dir"), Int(st, "atRow"));
                        return new DragMove(Int(j, "pieceId"), new DragNode(Int(to, "ix"), Int(to, "iy"), Int(to, "mode")), via, steer);
                    }
                case "hammer":
                    {
                        var t = j["target"] ?? throw new FormatException("hammer.target missing");
                        if (t["pieceId"] is JsonValue p && p.IsNumber) return new HammerMove(HammerTarget.Piece(p.AsInt));
                        return new HammerMove(HammerTarget.ObstacleAt(Int(t, "obstacle")));
                    }
                case "crane":
                    {
                        var to = j["to"] ?? throw new FormatException("crane.to missing");
                        return new CraneMove(Int(j, "pieceId"), Str(to, "zone"), Int(to, "x"), Int(to, "y"), Int(j, "rotation"));
                    }
                case "paint":
                    return new PaintMove(Int(j, "a"), Int(j, "b"));
                case "goldTrowel":
                    return new GoldTrowelMove(Int(j, "pieceId"), Int(j, "x"), Int(j, "y"));
                case "trowel":
                    return new TrowelMove(Int(j, "seg"), Int(j, "x"), Int(j, "y"));
                case "addMoves":
                    return new AddMovesMove(Int(j, "amount"), Str(j, "source"));
                case "undo":
                    return new UndoAction();
                case "start":
                    {
                        var pre = j["preBoosters"];
                        var list = new System.Collections.Generic.List<string>();
                        if (pre != null && pre.IsArray) foreach (var b in pre.Items) list.Add(b.AsString);
                        return new StartAction(list.ToArray(), Int(j, "streakTier"));
                    }
                default:
                    throw new FormatException("unknown action kind " + kind);
            }
        }

        /// <summary>Reads a JSON array of actions (a save or golden `log`).</summary>
        public static SessionAction[] LogFromJson(JsonValue array)
        {
            var o = new SessionAction[array.Count];
            for (int i = 0; i < o.Length; i++) o[i] = FromJson(array[i]);
            return o;
        }

        /// <summary>TS `copyAction`: a structural copy (`JSON.parse(JSON.stringify(a))`).</summary>
        public static SessionAction Clone(SessionAction a) => FromJson(ToJson(a));

        /// <summary>TS `toEqual` on actions: the same JSON text (canonical).</summary>
        public static bool SameAs(SessionAction a, SessionAction b) => JsonWriter.Canonical(ToJson(a)) == JsonWriter.Canonical(ToJson(b));

        public static bool SameLog(System.Collections.Generic.IReadOnlyList<SessionAction> a, System.Collections.Generic.IReadOnlyList<SessionAction> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (!SameAs(a[i], b[i])) return false;
            return true;
        }

        private static int Int(JsonValue o, string key)
        {
            var v = o[key];
            if (v == null || !v.IsNumber) throw new FormatException(key + " missing or not a number");
            return v.AsInt;
        }

        private static string Str(JsonValue o, string key)
        {
            var v = o[key];
            if (v == null || !v.IsString) throw new FormatException(key + " missing or not a string");
            return v.AsString;
        }
    }
}
