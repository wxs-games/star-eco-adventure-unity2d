# 任务清单：02-PlayerController

- [x] T1 PlayerConfig.cs（ScriptableObject 手感参数）
- [x] T2 状态机骨架：IPlayerState / PlayerStateMachine / PlayerStateBase（IPlayerState 定义于 PlayerStateMachine.cs）
- [x] T3 PlayerController.cs（输入/物理/土狼/缓冲/可变跳高/朝向）
- [x] T4 六个状态：Idle / Run / Air / Land / Hurt / Death（Jump+Fall 合并为 Air）
- [x] T5 PlayerAnimatorDriver.cs（参数写入 + 参数名常量类）
- [x] T6 Editor 工具 PlayerAnimationSetup.cs（生成 clips + AnimatorController，支持 22 个动作目录）
- [x] T7 Unity 编译零报错
- [x] T8 运行一键搭建工具生成 Animator；挂到测试场景验证跑跳
- [x] T9 git commit（M2: player controller + animation setup tool）

> 状态：M2 全部完成（提交 277d430，后续 c951cb1 将 clip/状态重命名为社区约定并扩展到 22 套动作）。
