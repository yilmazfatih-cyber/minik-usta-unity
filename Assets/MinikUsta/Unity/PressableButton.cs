#nullable enable
// The press feel of a uGUI button (Faz 3 Q2 item 11; JUICE #69): pressed it sinks to 0,94, released it pops to 1,04 and settles
// with Back.easeOut. The pointer events come from the EventSystem (`IPointerDownHandler` …); the motion is the engine-independent
// `PressFeel`. A pointer that slides off the button cancels the press without a click (like the Button itself).
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Game;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MinikUsta.Unity
{
    public sealed class PressableButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private PressFeel? feel;

        /// <summary>Binds the button's RectTransform (the scale about its centre) and the feel values.</summary>
        public void Init(RectTransform rect, FeelSettings settings)
        {
            feel = new PressFeel(new UiTarget(rect), settings);
        }

        public void OnPointerDown(PointerEventData eventData) => feel?.Press();

        public void OnPointerUp(PointerEventData eventData) => feel?.Release();

        public void OnPointerExit(PointerEventData eventData) => feel?.Cancel();

        private void Update() => feel?.Tick(Time.unscaledDeltaTime * 1000f);

        private void OnDisable() => feel?.Reset();
    }
}
