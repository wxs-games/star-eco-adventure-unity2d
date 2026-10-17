using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StarEco.UI
{
    /// <summary>
    /// 程序化 uGUI 公共工具（M7 R2）：Canvas/EventSystem 保活、字体、颜色常量与控件工厂。
    /// 从 M6 LevelResultUI 的同类私有代码抽离，行为保持一致。
    /// </summary>
    public static class UIUtility
    {
        /// <summary>面板底色（半透明深蓝紫）。</summary>
        public static readonly Color PanelBg = new Color(0.10f, 0.10f, 0.18f, 0.93f);

        /// <summary>全屏遮罩色。</summary>
        public static readonly Color Overlay = new Color(0f, 0f, 0f, 0.72f);

        /// <summary>主文字白。</summary>
        public static readonly Color TextMain = Color.white;

        /// <summary>次要文字（浅灰绿）。</summary>
        public static readonly Color TextBody = new Color(0.90f, 0.95f, 0.90f);

        /// <summary>强调绿。</summary>
        public static readonly Color AccentGreen = new Color(0.49f, 0.99f, 0.60f);

        /// <summary>警示红。</summary>
        public static readonly Color WarnRed = new Color(1f, 0.42f, 0.42f);

        /// <summary>禁用灰。</summary>
        public static readonly Color DisabledGray = new Color(0.53f, 0.53f, 0.53f);

        /// <summary>按钮常态色。</summary>
        public static readonly Color ButtonNormal = new Color(0.22f, 0.35f, 0.28f, 0.95f);

        /// <summary>按钮高亮色。</summary>
        public static readonly Color ButtonHighlight = new Color(0.32f, 0.50f, 0.38f, 1f);

        /// <summary>按钮按下色。</summary>
        public static readonly Color ButtonPressed = new Color(0.16f, 0.26f, 0.20f, 1f);

        /// <summary>按钮禁用色。</summary>
        public static readonly Color ButtonDisabled = new Color(0.25f, 0.25f, 0.27f, 0.9f);

        /// <summary>内置字体（LegacyRuntime，无美术资源依赖）。</summary>
        public static Font BuiltinFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null)
                    {
                        Debug.LogError("[UIUtility] 未取得内置字体 LegacyRuntime.ttf，UI 文字将不可见。");
                    }
                }

                return _font;
            }
        }

        private static Font _font;

        /// <summary>参考分辨率（16:9）。</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>确保场景中存在 EventSystem（否则按钮无响应）。幂等。</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(go);
            Debug.Log("[UIUtility] 场景缺少 EventSystem，已自动创建。");
        }

        /// <summary>创建 ScreenSpaceOverlay Canvas（含 Scaler 与 Raycaster）。</summary>
        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        /// <summary>创建 UI 空物体（RectTransform，UI 层）。</summary>
        public static GameObject CreateObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>全屏拉伸锚点。</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>居中锚点 + sizeDelta + 位置。</summary>
        public static RectTransform CenterAnchor(GameObject go, Vector2 size, Vector2 pos)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        /// <summary>左上锚点。</summary>
        public static RectTransform TopLeftAnchor(GameObject go, Vector2 size, Vector2 pos)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        /// <summary>底部居中锚点。</summary>
        public static RectTransform BottomCenterAnchor(GameObject go, Vector2 size, Vector2 pos)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        /// <summary>创建 Text。</summary>
        public static Text CreateText(string name, Transform parent, string content, int fontSize,
            TextAnchor alignment = TextAnchor.MiddleCenter, Color? color = null)
        {
            var go = CreateObject(name, parent);
            var text = go.AddComponent<Text>();
            text.font = BuiltinFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color ?? TextMain;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        /// <summary>创建 Image。</summary>
        public static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = CreateObject(name, parent);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>创建水平填充条（背景 + 填充层）。返回填充 Image。</summary>
        public static Image CreateBar(string name, Transform parent, Vector2 size, Vector2 pos,
            Color bgColor, Color fillColor, out GameObject barGo)
        {
            barGo = CreateObject(name, parent);
            CenterAnchor(barGo, size, pos);

            var bg = CreateImage("Background", barGo.transform, bgColor);
            Stretch(bg.GetComponent<RectTransform>());

            var fillGo = CreateObject("Fill", barGo.transform);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fill = fillGo.AddComponent<Image>();
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            return fill;
        }

        /// <summary>创建按钮（含文字标签），返回 Button。</summary>
        public static Button CreateButton(string name, Transform parent, string label, Vector2 size, Vector2 pos,
            int fontSize = 30, bool interactable = true)
        {
            var go = CreateObject(name, parent);
            CenterAnchor(go, size, pos);

            var image = go.AddComponent<Image>();
            image.color = ButtonNormal;

            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = ButtonNormal;
            colors.highlightedColor = ButtonHighlight;
            colors.pressedColor = ButtonPressed;
            colors.disabledColor = ButtonDisabled;
            button.colors = colors;
            button.interactable = interactable;

            var text = CreateText("Label", go.transform, label, fontSize);
            Stretch(text.GetComponent<RectTransform>());

            return button;
        }

        /// <summary>把秒格式化为 mm:ss。</summary>
        public static string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(Mathf.Max(0f, seconds) / 60f);
            int s = Mathf.FloorToInt(Mathf.Max(0f, seconds) % 60f);
            return string.Format("{0:00}:{1:00}", m, s);
        }

        /// <summary>播放 UI 音效（资源缺失时静默跳过，不报错）。</summary>
        public static void PlayClickSfx()
        {
            var audio = StarEco.Framework.AudioManager.HasInstance
                ? StarEco.Framework.AudioManager.Instance
                : null;
            if (audio == null)
            {
                return;
            }

            audio.PlaySfx(StarEco.Framework.SfxId.UIClick);
        }
    }
}
