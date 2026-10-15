using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TankTown
{
    public sealed class TankTownMenuHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        internal RectTransform Visual { get; private set; }
        private Button button;
        private Vector2 restPosition;
        private bool hovered, focused, pressed;
        private float highlight, compression, highlightVelocity, compressionVelocity, submitUntil;

        internal void Initialize(Button owner, RectTransform visual)
        {
            button = owner; Visual = visual; restPosition = visual.anchoredPosition;
        }

        private void LateUpdate()
        {
            if (Visual == null) return;
            bool available = button.isActiveAndEnabled && button.IsInteractable();
            float target = available && (hovered || focused) ? 1 : 0;
            float push = available && (pressed || Time.unscaledTime < submitUntil) ? 1 : 0;
            float delta = Mathf.Min(Time.unscaledDeltaTime, .05f);
            highlight = Mathf.SmoothDamp(highlight, target, ref highlightVelocity, target > 0 ? .045f : .065f, Mathf.Infinity, delta);
            compression = Mathf.SmoothDamp(compression, push, ref compressionVelocity, .025f, Mathf.Infinity, delta);
            if (target == 0 && highlight < .001f) highlight = highlightVelocity = 0;
            if (push == 0 && compression < .001f) compression = compressionVelocity = 0;
            Visual.localScale = Vector3.one * (1 + .025f * highlight - .04f * compression);
            Visual.anchoredPosition = restPosition + Vector2.up * (4 * highlight - 6 * compression);
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) => hovered = false;
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) pressed = true;
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) pressed = false;
        }
        // Programmatic initial focus and mouse clicks should not leave a button raised.
        public void OnSelect(BaseEventData eventData) => focused = eventData is AxisEventData;
        public void OnDeselect(BaseEventData eventData) => focused = false;
        public void OnSubmit(BaseEventData eventData) => submitUntil = Time.unscaledTime + .075f;

        private void OnDisable()
        {
            hovered = focused = pressed = false;
            highlight = compression = highlightVelocity = compressionVelocity = submitUntil = 0;
            if (Visual == null) return;
            Visual.localScale = Vector3.one; Visual.anchoredPosition = restPosition;
        }
    }
}
