using System.Collections.Generic;
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
    GoToObstacle goToObstacle;

    Entity nearestTarget;




    //━━━━━━━━━━ LifeCycle ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void Awake()
    {
        movement = new(this);
        Stat.Health = Stat.MaxHealth;

        rest = new Rest(this);
        chase = new Chase(this);
        attackFocus = new AttackFocus(this);
        dead = new Dead(this);
        goToObstacle = new GoToObstacle(this);

        Stat.OnDead += () => SetState(dead);

        // test init value
        Stat.AttackRange.SetBaseValue(Random.Range(20f, 30f));
    }

    void Update()
    {
        movement.Tick(Time.deltaTime);
        InCombatTick();
    }

    int findTick = 0;
    void FixedUpdate()
    {
        // fixme: 갱신은 Tick으로 진행하는걸 추천한다. 임시로 구현함.
        if (currentState == dead) return;

        findTick++;
        if (findTick >= 30)
        {
            findTick = 0;


            // Find Target
            nearestTarget = EntityManager.GetNearestEntity(transform.position, 100f, (Team.Me | Team.Enemy) ^ Team, this);

            // Find Obstacle (점유 중엔 유지)
            if (nearestTarget != null && Stat.OccupiedObstacle == null)
                SetCover(FindCover());
        }
    }

    void InCombatTick()
    {
        currentState?.OnUpdate();

        if (currentState == dead) return;

        bool hasTarget = HasTarget();

        // 즉시 타겟이 죽었거나, 타겟이 공격 불가 상태이면, 가장 가까운 타겟으로 전환
        if (nearestTarget != null && nearestTarget.Stat.CanBeTargeted == false)
            nearestTarget = EntityManager.GetNearestEntity(transform.position, 100f, (Team.Me | Team.Enemy) ^ Team, this);


        // ── Logic ──────────────
        // Behavior Tree?
        if (cover != null && Stat.OccupiedObstacle != cover) // 엄폐물로 이동 중
            SetState(goToObstacle);
        else if (hasTarget && TargetInRange())
            SetState(attackFocus);
        else if (hasTarget && !TargetInRange() && Stat.CanMove)
            SetState(chase);
        else
            SetState(rest);
    }

    void SetState(StateBase state)
    {
        if (currentState == state) return;
        currentState?.OnExit();
        currentState = state;
        currentState.OnEnter();
    }

    bool HasTarget() => nearestTarget != null && nearestTarget.isActiveAndEnabled && nearestTarget.Stat.CanBeTargeted;
    bool TargetInRange()
    => nearestTarget != null && (nearestTarget.transform.position - transform.position).ToXZ().sqrMagnitude <= Stat.AttackRange * Stat.AttackRange;


    //━━━━━━━━━━ Obstacle Methods ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 타겟을 중심으로 내 사거리-offset가 닿는 범위를 체크하는데 이용하는 offset const
    const float OBSTACLE_DETECTION_OFFSET = 3f; // target 사거리 가능한 내가 되도록

    List<Obstacle> obstacleCache = new(3);

    Obstacle cover; // 내 엄폐물 변경은 SetCover

    // 타겟 기준 범위 안에서 나에게 가장 가까운 엄폐물
    Obstacle FindCover()
    {
        var cnt = EntityManager.GetEntitiesInRange(nearestTarget.transform.position, Stat.AttackRange - OBSTACLE_DETECTION_OFFSET, Team.Obstacle, obstacleCache);

        Obstacle best = obstacleCache.Contains(cover) ? cover : null; // 범위 밖이면 버림
        float bestDist = best != null ? Mathf.Sqrt(best.sqrDistanceTo(this)) - Obstacle.REPLACE_RESERVER_DISTANCE_OFFSET : float.MaxValue;

        for (int i = 0; i < cnt; i++)
        {
            var o = obstacleCache[i];
            if (o == cover || !o.isActiveAndEnabled || !o.canHold(this)) continue;

            var dist = Mathf.Sqrt(o.sqrDistanceTo(this));
            if (dist < bestDist)
            {
                bestDist = dist;
                best = o;
            }
        }
        return best;
    }

    // 엄폐물 교체
    void SetCover(Obstacle next)
    {
        if (cover == next) return;

        if (cover != null) cover.Leave(this);
        cover = null;
        Stat.OccupiedObstacle = null;

        if (next != null && next.Hold(this, OnCoverLost)) cover = next;

        if (currentState == goToObstacle) goToObstacle.MoveToCover(); // 이동 중 교체 -> 목적지 재설정
    }

    // 강제로 잃을 경우 호출되는 callback func
    void OnCoverLost()
    {
        cover = null;
        Stat.OccupiedObstacle = null;
    }

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
            // idle 애니메이션
        }
    }

    class Chase : StateBase
    {
        const float REPATH_DISTANCE = 0.5f; // 타겟이 이만큼 움직이면 경로 재계산

        public Chase(Character c) : base(c) { }

        public override void OnEnter()
        {
            c.SetCover(null);
            c.movement.MoveTo(c.nearestTarget.transform.position);
            TestCharacterAnimation.StartMove(c.VisualBody);
        }

        public override void OnExit()
        {
            c.movement.Stop();
            TestCharacterAnimation.StopMove(c.VisualBody);
        }

        public override void OnUpdate()
        {
            if (c.nearestTarget == null) return;

            var destination = c.movement.Destination;
            var targetPos = c.nearestTarget.transform.position;

            // 경로 재계산
            if ((destination - targetPos.ToXZ()).sqrMagnitude > REPATH_DISTANCE * REPATH_DISTANCE)
                c.movement.MoveTo(c.nearestTarget.transform.position);
        }
    }

    class GoToObstacle : StateBase
    {

        public GoToObstacle(Character c) : base(c) { }

        public override void OnEnter()
        {
            MoveToCover();
            TestCharacterAnimation.StartMove(c.VisualBody);
        }

        public void MoveToCover()
        {
            if (c.cover == null || c.nearestTarget == null) return;
            c.movement.MoveTo(BackToCover(c.cover, c.nearestTarget));
        }

        // 엄폐물 뒤(타겟 반대편) 위치
        static Vector3 BackToCover(Obstacle cover, Entity target)
        {
            var dir = (cover.transform.position - target.transform.position).normalized;
            return cover.transform.position + dir * 1.5f;
        }

        public override void OnExit()
        {
            c.movement.Stop();
            TestCharacterAnimation.StopMove(c.VisualBody);
        }

        public override void OnUpdate()
        {
            if (c.cover == null) return;

            if (c.movement.IsArrived() && c.cover.Occupy(c))
            {
                c.Stat.OccupiedObstacle = c.cover;
                TestCharacterAnimation.Cover(c.VisualBody);
            }
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
            if (c.nearestTarget == null) return;

            nextAttackTime = Time.time + AttackInterval;
            animEndTime = Time.time + ATTACK_ANIMATION_TIME;

            c.VisualBody.rotation = Quaternion.LookRotation((c.nearestTarget.transform.position - c.VisualBody.position).SetY0());
            TestCharacterAnimation.Shoot(c.VisualBody, c.nearestTarget.transform.position);

            AttackResult result = AttackApplier.Attack(c, c.nearestTarget, out var damage);

            if (result >= AttackResult.Hit) // Hit!
            {
                BulletLineDrawer.DrawLine(c.VisualBody.position, c.nearestTarget.transform.position);
                Debug.Log($"{c.name} -> {c.nearestTarget.name} : {damage:0}");
                if (c.nearestTarget is Character t) TestCharacterAnimation.Hit(t.VisualBody);
            }
            else // Miss!
                Debug.Log($"{c.name} -> {c.nearestTarget.name} : Miss");

            // 더 가까운 타겟이 있으면 공격 직후 전환
            var nearestTarget = EntityManager.GetNearestEntity(c.transform.position, 50f, (Team.Me | Team.Enemy) ^ c.Team, c);
            if (nearestTarget != null && nearestTarget != c.nearestTarget)
                c.nearestTarget = nearestTarget;
        }
    }

    class Dead : StateBase
    {
        public Dead(Character c) : base(c) { }

        public override void OnEnter()
        {
            c.SetCover(null);
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


