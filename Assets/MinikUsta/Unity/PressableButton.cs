#nullable enable
// The press feel of a uGUI button (Faz 3 Q2 item 11; JUICE #69): pressed it sinks to 0,94, released it pops to 1,04 and settles
// with Back.easeOut. The pointer events come from the EventSystem (`IPointerDownHandler` …); the motion is the engine-independent
// `PressFeel`. A pointer that slides off the button cancels the press without a click (like the Button itself). B2 (JUICE #69
// "dudak 12→4 px"): while held the Image shows the kit's pressed drawing (`btn_primary_pressed` …), released it gets its own back.
using System;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MinikUsta.Unity
{
    public sealed class PressableButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private PressFeel? feel;
        private Action? onPress;
        private Image? image;
        private Sprite? pressedSprite;
        private Sprite? normalSprite;

        /// <summary>
        /// Binds the button's RectTransform (the scale about its centre) and the feel values; `pressed` is the V2 press sound (JUICE #69);
        /// `target` + `down` (B2) = the Image and the kit's pressed drawing shown while held (null = the scale feel only).
        /// </summary>
        public void Init(RectTransform rect, FeelSettings settings, Action? pressed = null, Image? target = null, Sprite? down = null)
        {
            feel = new PressFeel(new UiTarget(rect), settings);
            onPress = pressed;
            image = target;
            pressedSprite = down;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            feel?.Press();
            if (image != null && pressedSprite != null && image.sprite != pressedSprite)
            {
                normalSprite = image.sprite;
                image.sprite = pressedSprite;
            }
            onPress?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            feel?.Release();
            Restore();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            feel?.Cancel();
            Restore();
        }

        private void Restore()
        {
            if (image != null && normalSprite != null && image.sprite == pressedSprite) image.sprite = normalSprite;
            normalSprite = null;
        }

        private void Update() => feel?.Tick(Time.unscaledDeltaTime * 1000f);

        private void OnDisable()
        {
            feel?.Reset();
            Restore();
        }
    }
}
