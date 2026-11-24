# 眨眼优化版

原 BlinkTrial 与 DCC~/Player_Blink_Trial.blend 保留不变。本版输出独立目录与 DCC~/Player_Blink_Optimized.blend。

改善：眼皮固定边缘前偏移从 12 mm 降至 0.25 mm；活动部分偏移最高约 1.45 mm。仅将脸部眼睛区域内收，为闭合运动腾出空间。肤色由眼周和脸颊采样插值，烘焙成独立 1024² 贴图，避免原版细窄 UV 色带被拉成长条。眼皮改为非金属、粗糙度 0.75，抑制强烈条状高光；Blender 预览中关闭眼皮投影，以减轻外缘接触阴影。

保持 Blink_L / Blink_R，48 帧动作。模型总计 5081 个四边面。导出 FBX、GLB；Comparison.gif 为旧版与新版相同正面相机下的动态对比，Closed_ThreeQuarter.png 为斜侧闭眼检查。

限制：仍是独立覆盖式眼皮，不是与脸部焊接的解剖拓扑；近看仍能看见眼皮外缘和部分色差。没有独立眼球和转眼控制。Blender 的眼皮投影开关不会自动转移为 Unity Renderer 设置；实际游戏中需将眼皮 Renderer 的 Cast Shadows 设为 Off 后另行检查。尚未替换游戏主角，也未宣称最终游戏画面已验证。
