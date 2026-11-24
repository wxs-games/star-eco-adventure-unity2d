using System.Collections;
using StarEco.Player;
using UnityEngine;

namespace StarEco.Eco
{
    /// <summary>
    /// 拾取展示：物品跟随 3D 角色的双手，精灵角色沿用头顶升起效果。
    /// </summary>
    [DefaultExecutionOrder(40)]
    public class ItemRaiseFx : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Player3DGameplayVisual _visual;
        private float _displayScale;

        /// <summary>
        /// 在 <paramref name="anchor"/> 头顶播放物品升起展示。
        /// </summary>
        /// <param name="sprite">要展示的物品精灵（知识卡本体）</param>
        /// <param name="anchor">角色 SpriteRenderer，用于定位头顶与朝向</param>
        /// <param name="sortingOrder">渲染层（默认 30，保证压在角色之上）</param>
        public static ItemRaiseFx Play(Sprite sprite, SpriteRenderer anchor, int sortingOrder = 30)
        {
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject("ItemRaiseFx");
            var fx = go.AddComponent<ItemRaiseFx>();
            fx._sr = go.AddComponent<SpriteRenderer>();
            fx._visual = anchor != null ? anchor.GetComponentInParent<Player3DGameplayVisual>() : null;
            fx._sr.sprite = sprite;
            if (anchor != null) fx._sr.sortingLayerID = anchor.sortingLayerID;
            fx._sr.sortingOrder = sortingOrder;

            // 起点：头顶稍上方；有朝向时略微朝面向一侧偏移，避免正好被帽子挡住
            Vector3 basePos = anchor != null
                ? new Vector3(anchor.bounds.center.x, anchor.bounds.max.y, 0f)
                : Vector3.zero;
            float side = anchor != null && anchor.flipX ? -0.15f : 0.15f;
            fx.transform.position = basePos + new Vector3(side, 0.15f, 0f);

            fx.StartCoroutine(fx.Run());
            return fx;
        }

        private void LateUpdate()
        {
            Vector3 position = default;
            bool followingHands = _visual != null && _visual.TryGetItemHoldPoint(out position);
            // Keep a held item clear of the much larger head on the 3D character.
            float scale = followingHands ? _displayScale * .64f : _displayScale;
            transform.localScale = new Vector3(scale, scale, 1f);
            if (followingHands)
            {
                // Keep the bottom edge at the hands while the card scales in.
                float lowerEdge = _sr.sprite.bounds.min.y * scale;
                transform.position = position - Vector3.up * lowerEdge;
            }
        }

        private IEnumerator Run()
        {
            const float riseTime = 0.28f;
            const float holdTime = 0.9f;
            const float fadeTime = 0.35f;
            const float riseDistance = 0.55f;
            const float visualScale = 1.3f;   // 世界空间里稍微放大，避免太小被忽略

            Vector3 start = transform.position;
            Vector3 end = start + Vector3.up * riseDistance;
            Color baseColor = Color.white;

            // 升起（带一点回弹的缓出）
            float t = 0f;
            while (t < riseTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / riseTime);
                float ease = 1f - Mathf.Pow(1f - k, 3f);
                transform.position = Vector3.Lerp(start, end, ease);
                _displayScale = Mathf.Lerp(0.55f, 1f, ease) * visualScale;
                yield return null;
            }

            transform.position = end;
            _displayScale = visualScale;

            // 停留（轻微上下浮动，做出「举着晃」的感觉）
            t = 0f;
            while (t < holdTime)
            {
                t += Time.deltaTime;
                float bob = Mathf.Sin(t * 6f) * 0.04f;
                transform.position = end + Vector3.up * bob;
                yield return null;
            }

            // 淡出
            t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fadeTime);
                _sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - k);
                transform.position = end + Vector3.up * (k * 0.25f);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
