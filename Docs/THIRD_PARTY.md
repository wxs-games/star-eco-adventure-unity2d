# 素材与第三方说明

## 项目代码

项目自有代码及文档的许可见仓库根目录 `LICENSE`（MIT）。第三方文件、软件包与字体仍遵循其原始许可。

## 字体

`Assets/Resources/Fonts/` 使用 Noto Sans SC；现有版权与 SIL Open Font License 1.1 声明保存在该目录的 `README_LICENSE.txt`。随游戏包附上该声明；字体的许可不因本项目采用 MIT 而改变。

## Unity 与软件包

Unity 引擎及 Unity 软件包按各自条款使用。依赖名称和锁定信息以 `Packages/manifest.json`、`Packages/packages-lock.json` 以及嵌入包的 `package.json` 为准。包括 TextMeshPro、Cinemachine、ProBuilder、Burst、Test Framework 等；清单中的依赖不表示所有包都参与游戏逻辑。

Unity MCP 来自 [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)，项目锁定 v10.3.0；用于开发期编辑器操作，遵循其上游许可。本仓库不重新许可第三方组件。

## 图像与音频

项目记录中的图像使用过 AI 图像服务及本地 ComfyUI / SDXL 处理流程；资源说明见 `Assets/Art/README_素材说明.md`，处理脚本见 `Tools/art-pipeline/`。音频制作记录见开发文档的 `AudioDesign.md`。

仓库提供学习与展示所需资源，但不因 MIT 标签而对 AI 输出的独占性、任何外部模型或服务条款作额外保证。复用具体资产时，应结合其来源及对应服务或模型条款核对适用条件。
