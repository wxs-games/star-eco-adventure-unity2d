# 基础动画第二版

旧版 AnimationTrial 与 Player_Animated_Basic.blend 保留。第二版：DCC~/Player_Animated_Basic_V2.blend，五个独立 FBX 及 unity_check.json。

本轮修正：按膝盖与脚踝高度进行平滑权重过渡，脚部主要跟随 Foot；手臂沿肩腕轴分配权重，背包区域限制在胸部。通过二骨骼解析解确定髋/膝姿势，脚掌保持目标方向；逐级刷新父骨骼变换，修复此前子骨骼姿态错误。Root 不位移，脚踝支撑相向后运动、摆动相抬脚回收。Idle、Run、Fall 循环，Jump、Land 不循环。

验证：motion_audit.json 记录每帧 Root 和脚踝位置，Root 固定、所有身体顶点有权重。五条 FBX 在 Unity 导入且时长/循环已验证，控制台无 error。五个动作各检查5个姿势，跑步另检查10个斜侧姿势。以上不等于穿插、鞋底接地和玩法衔接全部通过。

RunComparison.gif 是5个采样姿势的慢速比较，并非原速度。RunSide.gif 约按0.33秒循环输出。

仍需打磨：肩袖局部变形、手指独立控制、鞋底实际接地、跑速与世界位移匹配，以及 Jump/Fall/Land 状态衔接。本轮未重新手工重拓扑，没有宣称所有关节布线已完成。未替换正式玩家和场景。
