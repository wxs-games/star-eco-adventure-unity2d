# 可播放的基础动画交付

入口场景：Assets/Scenes/Player3DAnimationPreview.unity。独立场景默认轮播 Idle / Run / Jump / Fall / Land，按钮或数字键1~5可切换；包括定时眨眼。该预览使用 AnimationClip.SampleAnimation 直接播放导入曲线，Animator禁用，避免把未完全验证的状态机接入作为交付。没有替换正式游戏角色。

模型源：DCC~/Player_Playable.blend。保留Rigify源控制骨架，并创建独立PlayerExport变形骨架；从保留驱动器的Rigify逐帧读取最终变形矩阵，再转换为无约束骨架的局部曲线。FBX中不要求加载Rigify脚本。原有试验文件全部保留。

已验证：屈膝诊断在Unity采样有763个导入顶点移动超过1mm，最大约0.64m；五个基础动作各在多个时间点验证了网格位移（unity_check.json）。运行场景播放后跨帧截图产生明确不同的跑步姿势，见RunFrame_0..11.png和Unity_Run.gif。此前同一execute_code调用中连续Camera.Render可能复用了皮肤渲染缓存，不能把那些旧截图当作最终验收。新截图分开设置姿势与捕获。控制台无error，停止Play后场景和资源已保存。

动作时长：Idle 0.833s，Run 0.667s完整双脚周期，Jump 0.45s，Fall 0.333s，Land 0.4s。Run采用AnimationVFXSpec §5记载的0.67s两步周期，而非将完整两步压到0.33s。3D动画为60fps曲线采样。Root不驱动游戏位移。

实际播放验证不等于美术终审：跑步与游戏水平速度尚未匹配；Jump/Fall/Land没有接入真实物理状态，手指没有独立骨骼，袖口和裤腿极端姿势仍需精修，眨眼仍为覆盖式眼睑。该交付是可播放的角色基础动画模块，不是整款游戏的最终Windows安装包，也未声称已完成正式玩家替换。
