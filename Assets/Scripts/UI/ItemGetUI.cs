using UnityEngine;
using UnityEngine.UI;
using StarEco.Framework;

namespace StarEco.UI
{
    /// <summary>
    /// 物品获得浮层（塞尔达传说风格）：拾取知识卡片时展示卡片图标 / 名称 / 说明，
    /// 任意键或超时自动关闭。
    ///
    /// 结构要点：本组件直接挂在 **根 Canvas** 上（RectTransform + Canvas + CanvasScaler），
    /// 而不是把 Canvas 建成子节点。原因：ScreenSpaceOverlay 的 Canvas 只有在作为根对象时
    /// 才会被自动撑到屏幕尺寸；挂到普通 Transform 下会让它的 Rect 变成 0×0，
    /// 表现为「全屏遮罩不显示、只有显式尺寸的元素可见」。
    /// </summary>
    public class ItemGetUI : MonoBehaviour
    {
        private Image _iconImage;
        private float _timer = 5f;
        private float _openTime;
        private KnowledgeCardData _card;
        private Sprite _iconSprite;

        private static ItemGetUI _active;

        /// <summary>当前物品获得面板是否正在显示。</summary>
        public static bool IsShowing => _active != null;

        /// <summary>显示物品获得面板。已有显示中的会先关闭。</summary>
        public static ItemGetUI Show(KnowledgeCardData card, float duration = 5f, Sprite icon = null)
        {
            if (card == null)
            {
                return null;
            }

            if (_active != null)
            {
                Destroy(_active.gameObject);
            }

            // 兜底清理：域重载会清空静态字段，残留在场景里的旧面板会让内容叠在一起
            foreach (var stale in FindObjectsOfType<ItemGetUI>(true))
            {
                if (stale != null && stale.gameObject != null)
                {
                    Destroy(stale.gameObject);
                }
            }

            var go = new GameObject("ItemGetUI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UIUtility.ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(go);   // 跨场景保留；编辑模式下不允许调用
            }

            var panel = go.AddComponent<ItemGetUI>();
            panel._timer = duration;
            panel._card = card;
            panel._iconSprite = icon;
            panel.BuildUI();
            _active = panel;
            return panel;
        }

        private void BuildUI()
        {
            UIUtility.EnsureEventSystem();
            _openTime = Time.unscaledTime;
            var root = transform;

            // —— 全屏遮罩（把画面压暗，让展示内容成为焦点） ——
            var bg = UIUtility.CreateImage("Background", root, new Color(0f, 0f, 0f, 0.62f));
            UIUtility.Stretch(bg.GetComponent<RectTransform>());

            // —— 内容卡片：衬底 + 卡片图标 + 标题 + 正文 ——
            var cardGo = UIUtility.CreateObject("CardPanel", root);
            UIUtility.CenterAnchor(cardGo, new Vector2(800f, 660f), Vector2.zero);
            var cardBg = cardGo.AddComponent<Image>();
            cardBg.color = new Color(0.09f, 0.13f, 0.11f, 0.96f);

            // 顶部装饰条（绿色强调）
            var accent = UIUtility.CreateImage("Accent", cardGo.transform, UIUtility.AccentGreen);
            var accentRt = accent.GetComponent<RectTransform>();
            accentRt.anchorMin = new Vector2(0f, 1f);
            accentRt.anchorMax = new Vector2(1f, 1f);
            accentRt.pivot = new Vector2(0.5f, 1f);
            accentRt.sizeDelta = new Vector2(0f, 6f);
            accentRt.anchoredPosition = Vector2.zero;

            // 「获得知识卡」小标题（距卡片顶 26px，字号要够大才不会被深色底吃掉）
            var kicker = UIUtility.CreateText("Kicker", cardGo.transform, "获得知识卡", 30,
                TextAnchor.MiddleCenter, UIUtility.AccentGreen);
            PlaceFromTop(kicker.gameObject, 600f, 48f, 26f);

            // 卡片图标（阴影衬底 + 主图，位于卡片上半部中线）
            var iconShadow = UIUtility.CreateImage("IconShadow", cardGo.transform, new Color(0f, 0f, 0f, 0.35f));
            UIUtility.CenterAnchor(iconShadow.gameObject, new Vector2(196f, 196f), new Vector2(0f, 120f));

            var iconGo = UIUtility.CreateObject("Icon", cardGo.transform);
            UIUtility.CenterAnchor(iconGo, new Vector2(180f, 180f), new Vector2(0f, 120f));
            _iconImage = iconGo.AddComponent<Image>();
            _iconImage.preserveAspect = true;

            var sprite = _iconSprite;
            if (sprite == null)
            {
                sprite = Resources.Load<Sprite>("Art/Art/Environment/Props/knowledge_card");
            }

            if (sprite != null)
            {
                _iconImage.sprite = sprite;
                _iconImage.color = Color.white;
            }
            else
            {
                // 素材缺失时用占位色块，保证面板不会空白
                _iconImage.sprite = UIUtility.WhiteSprite;
                _iconImage.color = new Color(0.9f, 0.9f, 0.4f, 1f);
            }

            // 卡片名称
            var title = UIUtility.CreateText("Title", cardGo.transform, _card.Title ?? string.Empty, 38,
                TextAnchor.MiddleCenter, UIUtility.TextMain);
            PlaceFromTop(title.gameObject, 720f, 56f, 350f);

            // 分隔线
            var divider = UIUtility.CreateImage("Divider", cardGo.transform, new Color(1f, 1f, 1f, 0.18f));
            PlaceFromTop(divider.gameObject, 640f, 2f, 414f);

            // 卡片说明正文（高度留足，超长时向下溢出而不是被截断）
            var body = UIUtility.CreateText("Body", cardGo.transform, _card.Body ?? string.Empty, 22,
                TextAnchor.UpperCenter, UIUtility.TextBody);
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.lineSpacing = 1.15f;
            PlaceFromTop(body.gameObject, 700f, 190f, 430f);

            // 底部提示
            var hint = UIUtility.CreateText("Hint", root, "按任意键继续", 20,
                TextAnchor.MiddleCenter, new Color(0.75f, 0.75f, 0.75f, 0.95f));
            var hintRt = hint.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0.5f, 0f);
            hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.sizeDelta = new Vector2(600f, 30f);
            hintRt.anchoredPosition = new Vector2(0f, 56f);
        }

        /// <summary>以父物体顶部为基准，按「上边距 + 宽高」摆放一个 UI 元素。</summary>
        private static void PlaceFromTop(GameObject go, float width, float height, float topOffset)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, -topOffset);
        }

        private void Update()
        {
            // 面板刚出现的极短窗口内忽略输入，避免拾取瞬间的按键把面板误关
            if (Time.unscaledTime - _openTime < 0.25f)
            {
                return;
            }

            if (Input.anyKeyDown)
            {
                Close();
                return;
            }

            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f)
            {
                Close();
            }
        }

        public void CloseNow()
        {
            Close();
        }

        private void Close()
        {
            if (_active == this)
            {
                _active = null;
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_active == this)
            {
                _active = null;
            }
        }
    }
}
