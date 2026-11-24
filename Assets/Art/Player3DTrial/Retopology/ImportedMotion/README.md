# 用户提供GLB的动作重定向

源文件：Downloads/879bb9d8-f8bf-4965-ae26-67f4d731b523.glb。65根Mixamo命名骨骼，一条名为Armature的动画，24fps下1~100帧，4.125秒。未改动原始文件。使用源文件已存在的骨骼动画，不是从MP4重新动捕，也未通过Mixamo服务重新生成。

DCC~/Animation_Source.blend 保存导入的源模型与动画；DCC~/Player_ImportedMotion.blend 保存目标主角、源模型和烘焙后的动作。Player_ImportedMotion.fbx 为目标骨架动画导出。retarget_check.json 记录映射、比例和源骨盆运动；RetargetComparison.gif 并排源动作和目标预览；Unity_ImportedMotion.gif 为Unity跨帧采样截图。动作原名不能证明来自哪一套动捕数据，本次不附加来源或许可推断。

进行了T/A姿势方向适配和腿长缩放；保留源骨盆的垂直起伏，去掉水平位移作为原地动作。预览场景 Assets/Scenes/Player3DAnimationPreview.unity 的Run槽位已替换成本片段，其他片段仍为此前版本。本轮只得到源文件的一条动作，不代表五个基础动作都变成动捕动作。

现状：动画已转移、Unity时长校正4.125秒、可驱动当前主角；短臂、背包和体型差异使摆臂仍需补偿，手部接近或穿入身体/背包的风险尚未完全消除。鞋底接触和世界移动速度未接入正式游戏验证。当前使用肢体方向匹配，轴向扭转保留有限，不能宣称专业重定向终审通过。正式主角尚未替换。
