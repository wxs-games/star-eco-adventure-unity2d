using UnityEditor;
using StarEco.Framework;

namespace StarEco.EditorTools
{
    /// <summary>
    /// 框架自检手动入口（FIND P1 修复 2026-10-31）：原 RuntimeInitializeOnLoadMethod
    /// 会在每次进场景自动跑自检——TestAudioManager 重置玩家音量、TestSaveSystem 故意
    /// 损坏存档产生 error 污染控制台。改为菜单手动触发，构建版与日常调试零副作用。
    /// 菜单：StarEco → Tools → 框架自检
    /// </summary>
    public static class FrameworkSelfTestMenu
    {
        [MenuItem("StarEco/Tools/框架自检")]
        public static void Run()
        {
            StarEco.Framework.FrameworkSelfTest.Run();
        }
    }
}
