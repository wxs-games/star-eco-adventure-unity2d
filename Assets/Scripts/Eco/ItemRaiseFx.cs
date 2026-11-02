using System.Collections;
using UnityEngine;

namespace StarEco.Eco
{
    /// <summary>
    /// 拾取展示特效：把刚获得的物品精灵从角色头顶「弹出并升起」，短暂停留后淡出。
    ///
    /// 背景：玩家的 Interact 动画是通用交互姿势（握拳），并没有「举起物品」的帧，
    /// 因此用世界空间精灵补足塞尔达式「举高展示」的观感，且不需要新增美术资源。
    /// </summary>
    public class ItemRaiseFx : MonoBehaviour
    {
        private SpriteRenderer _sr;

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
            fx._sr.sprite = sprite;
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
                float pop = Mathf.Lerp(0.55f, 1f, ease) * visualScale;
                transform.localScale = new Vector3(pop, pop, 1f);
                yield return null;
            }

            transform.position = end;
            transform.localScale = new Vector3(visualScale, visualScale, 1f);

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
