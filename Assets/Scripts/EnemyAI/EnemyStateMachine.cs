using System;
using System.Collections.Generic;

namespace StarEco.EnemyAI
{
    /// <summary>敌人状态接口：所有敌人状态的统一契约（与 M2 IPlayerState 同四段式，命名独立）。</summary>
    public interface IEnemyState
    {
        void Enter();
        void Exit();
        /// <summary>每帧逻辑（感知、转移检查）。</summary>
        void Tick();
        /// <summary>每物理帧逻辑（位移/施力）。</summary>
        void FixedTick();
    }

    /// <summary>
    /// 敌人状态机：字典注册 + 当前态轮询。切换时保证 Exit → Enter 顺序。
    /// 独立于 StarEco.Player，不引用 IPlayerState/PlayerStateMachine。
    /// </summary>
    public class EnemyStateMachine
    {
        private readonly Dictionary<Type, IEnemyState> _states = new Dictionary<Type, IEnemyState>();

        public IEnemyState Current { get; private set; }
        public Type CurrentType => Current?.GetType();

        /// <summary>注册状态（重复注册同名状态会覆盖）。</summary>
        public void Register(IEnemyState state)
        {
            _states[state.GetType()] = state;
        }

        /// <summary>设置初始状态（泛型，不走 Exit）。</summary>
        public void SetInitial<T>() where T : class, IEnemyState
        {
            Current = _states[typeof(T)];
            Current.Enter();
        }

        /// <summary>设置初始状态（非泛型重载，供 Enemy03 动态指定 Spawn 为初始态）。</summary>
        public void SetInitial(Type stateType)
        {
            Current = _states[stateType];
            Current.Enter();
        }

        /// <summary>切换状态：先 Exit 当前，再 Enter 新态。</summary>
        public void SwitchState<T>() where T : class, IEnemyState
        {
            var next = _states[typeof(T)];
            if (next == Current)
            {
                return;
            }
            Current?.Exit();
            Current = next;
            Current.Enter();
        }

        /// <summary>切换状态（非泛型重载）。</summary>
        public void SwitchState(Type stateType)
        {
            var next = _states[stateType];
            if (next == Current)
            {
                return;
            }
            Current?.Exit();
            Current = next;
            Current.Enter();
        }

        /// <summary>该状态是否已注册（不同敌人注册的状态集不同，切换前先查，避免 KeyNotFound）。</summary>
        public bool HasState<T>() where T : class, IEnemyState
        {
            return _states.ContainsKey(typeof(T));
        }

        /// <summary>仅在已注册时切换；未注册则什么都不做（供可选状态使用，如巡逻）。</summary>
        public void SwitchStateIfRegistered<T>() where T : class, IEnemyState
        {
            if (HasState<T>())
            {
                SwitchState<T>();
            }
        }

        public void Tick()
        {
            Current?.Tick();
        }

        public void FixedTick()
        {
            Current?.FixedTick();
        }
    }
}
