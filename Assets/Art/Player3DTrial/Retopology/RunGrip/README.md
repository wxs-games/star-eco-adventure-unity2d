# 跑步半握拳试验版

原 MotionRefined 文件与 Player3DAnimationPreview 场景保留。新增 Player3DGripPreview.unity、DCC~/Player_RunGrip.blend 和 Player_RunGrip.fbx。

原始低模的指尖无法直接弯曲到握拳而不塌陷，因此在副本中以平面切开手腕末端，重建简化手掌、四指和拇指，分别绑定原有DEF-hand.L/R。新手部有Basis与RunGrip形态键。手腕边界对齐并统一绑定到手骨。

预览驱动在Run槽位将双手RunGrip设为100，其他四个槽位设为0；眨眼保持独立。运行验证记录 Run_check.json（双手100）、Idle_check.json（双手0），控制台复测无error。动画片段保持此前外部动作。脚本未修改正式玩家。

限制：这是简化手部与半握拳功能版本，手腕接缝、肤色和指节形状在近景仍不够自然，尚不能宣称美术终审完成；也没有独立手指骨骼。新手部为分离网格，没有与身体焊接为连续拓扑。其他动作仍用张手，但新场景使用重建后的张手外形；旧场景保留原外形。
