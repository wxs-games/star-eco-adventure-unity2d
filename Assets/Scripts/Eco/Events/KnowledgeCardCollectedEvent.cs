using UnityEngine;

namespace StarEco.Eco.Events
{
    /// <summary>
    /// 知识卡片在本次关卡运行中被实际拾取。
    /// 与 CodexService 的永久图鉴存档区分，供关卡机关使用。
    /// </summary>
    public struct KnowledgeCardCollectedEvent
    {
        public string CardId;
        public Vector2 Position;
    }
}
