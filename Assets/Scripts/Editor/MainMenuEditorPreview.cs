#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using StarEco.UI;

/// <summary>
/// 主菜单编辑器预览：在编辑器模式下（不点 Play）自动生成主菜单 UI，
/// 方便美术与策划直接在场景里调整布局、字体、颜色。
///
/// 触发时机：
/// 1. 编辑器加载 Title 场景后，自动调用 MainMenuPanel.Build()。
/// 2. 菜单 "StarEco/Editor/生成主菜单 UI" 可手动触发。
///
/// 重复调用安全：Build() 内已对每个子对象做“已存在则复用”判断，不会重复创建。
/// </summary>
[InitializeOnLoad]
public static class MainMenuEditorPreview
{
    static MainMenuEditorPreview()
    {
        EditorSceneManager.sceneLoaded += OnSceneLoaded;
        // 编辑器启动时，如果 Title 场景已加载，也尝试生成
        EditorApplication.delayCall += TryGenerate;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryGenerate();
    }

    private static void TryGenerate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Title.unity");
        if (!scene.isLoaded)
        {
            return;
        }

        var panel = Object.FindObjectOfType<MainMenuPanel>();
        if (panel != null)
        {
            panel.Build();
            Debug.Log("[MainMenuEditorPreview] 编辑器模式下已生成主菜单 UI。");
        }
    }

    [MenuItem("StarEco/Editor/生成主菜单 UI")]
    private static void MenuGenerate()
    {
        TryGenerate();
    }
}
#endif
