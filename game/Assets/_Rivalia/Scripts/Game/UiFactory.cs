using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rivalia.Game
{
    /// <summary>Criação de UI por código (sem prefabs), para o MVP abrir numa cena vazia.</summary>
    public static class UiFactory
    {
        private static Font _font;
        private static Sprite _circle;

        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Sprite CircleSprite
        {
            get
            {
                if (_circle != null) return _circle;
                const int s = 128;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
                var c = new Vector2(s / 2f, s / 2f);
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = Mathf.Clamp01(s / 2f - d);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
                tex.Apply();
                _circle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
                return _circle;
            }
        }

        private static Sprite _square;
        public static Sprite SquareSprite
        {
            get
            {
                if (_square != null) return _square;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) tex.SetPixel(x, y, Color.white);
                tex.Apply();
                _square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
                return _square;
            }
        }

        public static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        public static Text Text(Transform parent, string text, int size, Color color, TextAnchor align)
        {
            var go = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.6f);
            return t;
        }

        public static Button Button(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color, string label, Action onClick, bool round = false)
        {
            var rt = Panel(parent, name, anchor, pos, size, color);
            var img = rt.GetComponent<Image>();
            img.raycastTarget = true;
            if (round) img.sprite = CircleSprite;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            Text(rt, label, 24, Color.white, TextAnchor.MiddleCenter);
            return b;
        }

        public static Image Fill(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(3, 3); rt.offsetMax = new Vector2(-3, -3);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            img.sprite = SquareSprite; // sprite necessário para Image.Type.Filled
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            return img;
        }
    }
}
