# 外部动作肩肘适配修正版

保留 ImportedMotion 与 DCC~/Player_ImportedMotion.blend。新版本为 DCC~/Player_MotionRefined.blend、Player_ImportedMotion.fbx；独立预览场景 Assets/Scenes/Player3DAnimationPreview.unity 的Run槽位已切换到本版。

问题定位：源模型较瘦、手臂较长，按比例直接重定向后，主角手腕位于厚背心内部。基于原动作每帧手腕轨迹增加向前0.13m、向外0.045m的体型偏移，并限制贴身范围，再用双段解析IK求出肘部；同时限制伸展距离，避免超出主角自身臂长。上臂/前臂各两个变形段连续分配，保留源动作节奏、腿部动作和骨盆起伏，不改变蒙皮或拓扑。

验证：100帧双臂位置与伸展比例见arm_check.json；16个正面姿势，以及第24、72帧的侧面/背面截图已检查；ArmFixComparison.gif为Unity同时间点跨帧采样的新旧对比。Unity片段4.125秒、Generic导入、直接曲线预览播放。原始动画文件未改。

范围限制：这是针对当前一条动作和当前主角体型的修正，不是通用人体碰撞解算；侧背抽查没有代替逐顶点穿插证明。袖口、手掌朝向和手指仍可能需要美术精修。没有新增手指骨骼，脚底接地与世界跑速仍需游戏中调节。其他四条基础动作未替换为外部动作，正式玩家未替换。
