# 任务清单：03-Combat

- [x] T1 DamageInfo + IHittable 接口（Assets/Scripts/Combat/，M4 复用基座）
- [x] T2 CombatConfig.cs（ScriptableObject：HP/伤害/连段窗 0.15s/硬直 0.6s/三段时长/第三段击退）
- [x] T3 PlayerCombat.cs（实现 IHittable、HP、连段输入接收、受击/无敌判定入口）
- [x] T4 PlayerAttackState.cs（发起/锁移动/连段 buffer/AttackAir 分支/结束回落）
- [x] T5 改造 PlayerHurtState：吃 DamageInfo（击退方向来自伤害源），接 PlayerCombat 的 HP 扣减
- [x] T6 AttackHitbox 子物体 + 开窗判定（OnTriggerEnter2D + 本窗命中集合）
- [x] T7 为 Attack01/02/03/AttackAir 四条 clip 补 Animation Event（已确认存在 OnAttackWindowOpen/Close 事件帧）
- [x] T8 PlayerAnimatorDriver 补消费 4 个攻击 Trigger（Attack/Attack2/Attack3/AttackAir）
- [x] T9 测试靶：场景内占位 IHittable 对象（TestTarget.cs）
- [x] T10 Unity 编译零报错 + 控制台检查
- [ ] T11 运行验证：三段连击衔接、连段窗回落、空中攻击、开窗期外不命中、受击击退、无敌闪烁、HP 归零进 Death（需用户配合按 Play 目检手感）
- [ ] T12 git commit（M3: combat system — combo/HP/hit window/invincibility）

> 状态：M3 代码全部落地（Assets/Scripts/Combat/ 共 7 个文件），**尚未 git 提交**（T12 待做）；T7 事件帧已实装于 clip；T11 手感验证需用户 Play 配合。
