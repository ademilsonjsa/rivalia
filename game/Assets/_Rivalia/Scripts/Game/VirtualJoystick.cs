using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rivalia.Game
{
    /// <summary>
    /// Joystick virtual multitoque (cada joystick acompanha o seu próprio dedo pelo pointerId).
    /// Flutuante: nasce onde o dedo toca dentro da área. Fixo: fica parado (usado para mira/botões).
    /// Ao soltar, informa se foi um toque rápido (sem arrastar) para a assistência de mira.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public bool Floating;
        public float DeadZone = 0.15f;
        public float FullSpeedZone = 0.9f;
        public float RadiusPixels = 110f;
        public float Sensitivity = 1f;

        public RectTransform Base, Handle;
        public Vector2 Value { get; private set; }          // após zona morta, 0..1
        public Vector2 RawDirection { get; private set; }   // direção crua (para a mira)
        public bool IsHeld { get; private set; }

        /// <summary>(direção, foiToqueRapido)</summary>
        public event Action<Vector2, bool> Released;

        private int _pointerId = int.MinValue;
        private float _downTime, _maxDrag;
        private Vector2 _homePos;

        public static VirtualJoystick Create(Transform canvas, string name, Vector2 anchor, Vector2 pos, Vector2 areaSize, float radius, bool floating, Color color, string label = null)
        {
            var area = new GameObject(name, typeof(RectTransform), typeof(Image));
            area.transform.SetParent(canvas, false);
            var rt = (RectTransform)area.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = areaSize;
            area.GetComponent<Image>().color = new Color(1, 1, 1, floating ? 0f : 0.001f); // área tocável invisível

            var js = area.AddComponent<VirtualJoystick>();
            js.Floating = floating;
            js.RadiusPixels = radius;
            js.Base = Circle(area.transform, "Base", radius * 2f, new Color(color.r, color.g, color.b, 0.25f));
            js.Handle = Circle(js.Base, "Alca", radius * 0.9f, new Color(color.r, color.g, color.b, 0.75f));
            if (label != null) UiFactory.Text(js.Handle, label, 26, Color.white, TextAnchor.MiddleCenter);
            js._homePos = js.Base.anchoredPosition;
            return js;
        }

        private static RectTransform Circle(Transform parent, string name, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = UiFactory.CircleSprite;
            img.color = color;
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(size, size);
            return rt;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointerId != int.MinValue) return; // já há um dedo neste joystick
            _pointerId = e.pointerId;
            IsHeld = true;
            _downTime = Time.unscaledTime;
            _maxDrag = 0f;
            if (Floating)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var local);
                Base.anchoredPosition = local;
            }
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Base, e.position, e.pressEventCamera, out var local);
            Vector2 v = local / RadiusPixels;
            if (v.magnitude > 1f) v = v.normalized;
            Handle.anchoredPosition = v * RadiusPixels;
            _maxDrag = Mathf.Max(_maxDrag, v.magnitude);
            RawDirection = v;

            float m = v.magnitude;
            if (m < DeadZone) Value = Vector2.zero;
            else
            {
                float k = Mathf.Clamp01((m - DeadZone) / (FullSpeedZone - DeadZone));
                Value = v.normalized * Mathf.Clamp01(k * Sensitivity);
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            bool tap = _maxDrag < 0.25f && Time.unscaledTime - _downTime < 0.3f;
            var dir = RawDirection;
            _pointerId = int.MinValue;
            IsHeld = false;
            Value = Vector2.zero;
            RawDirection = Vector2.zero;
            Handle.anchoredPosition = Vector2.zero;
            if (Floating) Base.anchoredPosition = _homePos;
            Released?.Invoke(dir, tap);
        }
    }
}
