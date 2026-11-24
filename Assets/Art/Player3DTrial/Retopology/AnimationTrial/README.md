# 基础身体动画试验版

含 Idle / Run / Jump / Fall / Land 五个动作，每个独立 FBX；DCC~/Player_Animated_Basic.blend 保存骨架和五条 Action。原眨眼及静态模型文件保持不变。

依据 AnimationVFXSpec §2、§5：不使用 Root Motion；时长约 0.833 / 0.333 / 0.444 / 0.333 / 0.400 秒。3D 曲线以60fps采样，四舍五入后时长见 animation_check.json。

当前为程序关键帧首版与分区距离蒙皮，共20根骨骼，未加 IK。已检查五种姿势截图及有限值；这不代表关节蒙皮、接地和运动手感通过最终验收。跑步仍可能有脚滑，Jump/Fall/Land 之间未做游戏状态过渡。眨眼形态键保留，但未合成为独立面部动画层。

BasicActions.gif 是五个采样姿势拼成的对比预览，固定150ms每张，不代表各动作真实播放速度；实际时间以FBX曲线为准。

未替换现有主角、控制器或正式场景。
