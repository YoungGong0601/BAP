using System;
using UnityEngine;
using UnityEngine.AI;

// 캐릭터
[RequireComponent(typeof(NavMeshAgent))]
public class Character : Entity
{
    NavMeshMovement movement;

    // FSM
    abstract class StateBase
    {
        public Character c;
        public StateBase(Character c) { this.c = c; }
        public abstract void OnEnter();
        public abstract void OnUpdate();
        public abstract void OnExit();
    }
    StateBase currentState;
    StateBase rest, chase, attackFocus, dead;

    Entity target;



    // InCombat (wave 진행 중 == wave에 적이 남음)
    // 타겟 :
    // 0 엄폐자일 경우, 상대를 공격할 수 있는 범위 내의 엄폐물 뒤
    // 1 가장 가까운 상대

    // 정지 :
    // -1 웨이브에 적이 없음 => 아군 준비 대기
    // 0 -> 엄폐물 뒤에서 정지
    // 1 -> 상대를 공격할 수 있는 범위 내에서 정지

    // OutCombat
    // 아군 이동가능 까지 대기 -> 앞으로 이동 -> 웨이브 시작 시 InCombat 진행

    private void Awake()
    {
        movement = new(this);
        Stat.Health = Stat.MaxHealth;

        rest = new Rest(this);
        chase = new Chase(this);
        attackFocus = new AttackFocus(this);
        dead = new Dead(this);

        // test
        Stat.AttackRange.SetBaseValue(UnityEngine.Random.Range(10f, 17f));
    }

    void Update()
    {
        movement.Tick(Time.deltaTime);
        InCombatTick();
    }



    void InCombatTick()
    {
        currentState?.OnUpdate();

        if (Stat.Health <= 0f)
        {
            Enter(dead);
            return;
        }

        if (!HasTarget())
            target = EntityManager.GetNearestEntity(transform.position, 50f, (Team.Me | Team.Enemy) ^ Team, this);

        if (HasTarget() && TargetInRange())
            Enter(attackFocus);
        else if (HasTarget())
            Enter(chase);
        else
            Enter(rest);

    }

    void Enter(StateBase state)
    {
        if (currentState == state) return;
        currentState?.OnExit();
        currentState = state;
        currentState.OnEnter();
    }

    bool HasTarget() => target != null && target.isActiveAndEnabled && target.Stat.CanBeTargeted;
    bool TargetInRange()
    => target != null && (target.transform.position - transform.position).ToXZ().sqrMagnitude <= Stat.AttackRange * Stat.AttackRange;



    // ═══════════════════════════════════════
    //  States
    // ═══════════════════════════════════════

    class Rest : StateBase
    {
        public Rest(Character c) : base(c) { }

        public override void OnEnter()
        {
            c.movement.Stop();
        }

        public override void OnExit()
        {
        }

        public override void OnUpdate()
        {
        }
    }

    class Chase : StateBase
    {
        const float REPATH_DISTANCE = 0.5f; // 타겟이 이만큼 움직이면 경로 재계산

        public Chase(Character c) : base(c) { }

        public override void OnEnter()
        {
            c.movement.MoveTo(c.target.transform.position);
            TestCharacterAnimation.StartMove(c.VisualBody);
        }

        public override void OnExit()
        {
            c.movement.Stop();
            TestCharacterAnimation.StopMove(c.VisualBody);
        }

        public override void OnUpdate()
        {
            var destination = c.movement.Destination;
            var targetPos = c.target.transform.position;
            if (c.target != null && (destination - targetPos.ToXZ()).sqrMagnitude > REPATH_DISTANCE * REPATH_DISTANCE)
                c.movement.MoveTo(c.target.transform.position);
        }
    }

    class AttackFocus : StateBase
    {
        const float BASE_ATTACK_INTERVAL = 1f; // 기본 공격 간격 (초, 공격 시작 ~ 다음 공격 시작)
        const float ATTACK_COOLRESET_TIME = 1f; // 비공격 시간 후 리셋
        const float ATTACK_ANIMATION_TIME = 0.7f; // 공격 애니메이션 시간 (초)

        float nextAttackTime; // 다음 공격 가능 시각
        float animEndTime;    // 공격 애니메이션 끝나는 시각

        // float AttackInterval => BASE_ATTACK_INTERVAL / Mathf.Max(0.01f, c.stat.AttackSpeed / 10000f);
        float AttackInterval = BASE_ATTACK_INTERVAL;

        // 공격 애니메이션 중 (밖에서 전환 보류 판단용)
        public bool IsPlayingAttackAnim => Time.time < animEndTime;

        public AttackFocus(Character c) : base(c) { }

        public override void OnEnter()
        {
            c.movement.Stop();
            if (Time.time > nextAttackTime + ATTACK_COOLRESET_TIME)
                nextAttackTime = Time.time + AttackInterval; // 공격 쿨다운 리셋
        }

        public override void OnExit()
        {
            animEndTime = 0f; // 사망 등으로 나갈 때 애니메이션 취소
        }

        public override void OnUpdate()
        {
            if (IsPlayingAttackAnim) return;
            if (Time.time < nextAttackTime) return;
            if (c.target == null) return;

            nextAttackTime = Time.time + AttackInterval;
            animEndTime = Time.time + ATTACK_ANIMATION_TIME;

            c.VisualBody.rotation = Quaternion.LookRotation((c.target.transform.position - c.VisualBody.position).SetY0());
            TestCharacterAnimation.Shoot(c.VisualBody, c.target.transform.position);

            if (AttackApplier.Attack(c.Stat, c.target.Stat, out var damage))
            {
                BulletLineDrawer.DrawLine(c.VisualBody.position, c.target.transform.position, Color.white, 0.1f);
                Debug.Log($"{c.name} -> {c.target.name} : {damage:0}");
                if (c.target is Character t) TestCharacterAnimation.Hit(t.VisualBody);
            }
            else
                Debug.Log($"{c.name} -> {c.target.name} : Miss");

            // 더 가까운 타겟이 있으면 공격 직후 전환
            var nearestTarget = EntityManager.GetNearestEntity(c.transform.position, 50f, (Team.Me | Team.Enemy) ^ c.Team, c);
            if (nearestTarget != null && nearestTarget != c.target)
                c.target = nearestTarget;
        }
    }

    class Dead : StateBase
    {
        public Dead(Character c) : base(c) { }

        public override void OnEnter()
        {
            c.movement.Stop();
            c.Stat.CanBeTargeted = false;
            TestCharacterAnimation.Dead(c.VisualBody);
        }

        public override void OnExit()
        {
            c.Stat.CanBeTargeted = true;
        }

        public override void OnUpdate()
        {
        }
    }
}


