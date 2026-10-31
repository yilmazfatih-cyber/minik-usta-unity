#nullable enable
// Pointer input with the legacy Input Manager (D-096; ProjectSettings activeInputHandler 0): touches on phones, the left
// mouse button on desktop / in the editor. Screen px (origin bottom-left) → design px through the current viewport, then
// straight into `LevelPlayer` in the same frame (TECH §4.6 one-frame response). One finger drives the board (TECH §4.6:
// a second finger is ignored by DragMapper); `Canceled` (incoming call, edge gesture) aborts the drag.
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using UnityEngine;

namespace MinikUsta.Unity
{
    public sealed class PointerInput
    {
        /// <summary>Pointer id of the mouse (touch ids are the finger ids, ≥ 0).</summary>
        public const int MouseId = 1000;

        private bool mouseDown;

        public PointerInput()
        {
            // touches are read directly; a simulated mouse would press twice (uGUI reads touches on its own)
            Input.simulateMouseWithTouches = false;
        }

        public void Poll(LevelPlayer player, DesignViewport vp)
        {
            int n = Input.touchCount;
            if (n > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    var t = Input.GetTouch(i);
                    vp.ScreenToDesign(t.position.x, t.position.y, out double x, out double y);
                    switch (t.phase)
                    {
                        case TouchPhase.Began:
                            player.PointerDown(t.fingerId, x, y);
                            break;
                        case TouchPhase.Moved:
                        case TouchPhase.Stationary:
                            player.PointerMove(t.fingerId, x, y);
                            break;
                        case TouchPhase.Ended:
                            player.PointerMove(t.fingerId, x, y);
                            player.PointerUp(t.fingerId, false);
                            break;
                        case TouchPhase.Canceled:
                            player.PointerUp(t.fingerId, true);
                            break;
                    }
                }
                return;
            }
            var m = Input.mousePosition;
            vp.ScreenToDesign(m.x, m.y, out double mx, out double my);
            if (Input.GetMouseButtonDown(0))
            {
                mouseDown = true;
                player.PointerDown(MouseId, mx, my);
            }
            else if (mouseDown && Input.GetMouseButton(0)) player.PointerMove(MouseId, mx, my);
            if (mouseDown && Input.GetMouseButtonUp(0))
            {
                mouseDown = false;
                player.PointerMove(MouseId, mx, my);
                player.PointerUp(MouseId, false);
            }
        }
    }
}
