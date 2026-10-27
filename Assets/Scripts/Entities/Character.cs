using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 캐릭터
[RequireComponent(typeof(NavMeshAgent))]
public class Character : Entity
{

    const float FIND_OBSTACLE_RANGE_DELTA_FROM_TARGET = -3f; // target 사거리 가능한 내가 되도록
    const float OBSTACLE_FIND_RANGE = 6f; // 자신 근처 장애물인가
    const float MIN_OBSTACLE_DISTANCE_FROM_TARGET = 3f; // 장애물과 타겟 사이의 최소 거리

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
    StateBase rest, chase, attackFocus, dead, goToObstacle;

    Entity nearestTarget;
    Obstacle validObstacle; // 찾은 엄폐물. 이용중인 엄폐물은 target.Stat.JoinedObstacle



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
        goToObstacle = new GoToObstacle(this);
        // test
        Stat.AttackRange.SetBaseValue(Random.Range(10f, 17f));
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
        findTick++;
        if (findTick >= 30) // 0.5초마다 갱신
        {
            findTick = 0;
            nearestTarget = EntityManager.GetNearestEntity(transform.position, 50f, (Team.Me | Team.Enemy) ^ Team, this);
            // if (!IsValidObstacle(validObstacle, nearestTarget))
            // {
            validObstacle = GetValidObstacle(nearestTarget);
            validObstacle?.Join(this); // 선 타겟팅: 고른 즉시 예약
            // }
        }
    }



    void InCombatTick()
    {
        currentState?.OnUpdate();

        if (Stat.Health <= 0f)
        {
            Enter(dead);
            return;
        }

        // 엄폐물이 바뀌었거나 사라졌으면 기존 엄폐물에서 나옴
        if (Stat.JoinedObstacle != null && Stat.JoinedObstacle != validObstacle)
            Stat.JoinedObstacle.Leave(this);

        bool hasTarget = HasTarget();
        if (hasTarget && Stat.JoinedObstacle != null && !Stat.JoinedObstacle.IsUserArrived)
            Enter(goToObstacle); // 예약한 엄폐물로 이동, 도착하면 Arrive
        else if (hasTarget && TargetInRange())
            Enter(attackFocus);
        else if (hasTarget)
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

    bool HasTarget() => nearestTarget != null && nearestTarget.isActiveAndEnabled && nearestTarget.Stat.CanBeTargeted;
    bool TargetInRange()
    => nearestTarget != null && (nearestTarget.transform.position - transform.position).ToXZ().sqrMagnitude <= Stat.AttackRange * Stat.AttackRange;

    bool IsValidObstacle(Obstacle obstacle, Entity target)
    {
        if (obstacle == null || target == null) return false;

        bool inRangeFromMe
            = (obstacle.transform.position - transform.position).ToXZ().sqrMagnitude <= OBSTACLE_FIND_RANGE * OBSTACLE_FIND_RANGE;
        bool isObstacleWithinAttackRangeOfTarget
            = (obstacle.transform.position - target.transform.position).ToXZ().sqrMagnitude <= (Stat.AttackRange + FIND_OBSTACLE_RANGE_DELTA_FROM_TARGET) * (Stat.AttackRange + FIND_OBSTACLE_RANGE_DELTA_FROM_TARGET);
        bool res = inRangeFromMe && isObstacleWithinAttackRangeOfTarget;
        return res;
    }



    // 타겟과의 거리가 공격 범위 내에 있는 유효한 장애물을 찾는다.
    // 이미 유효한 장애물이 없으면,
    // 적을 공격 할 수 있는 거리에 있는 장애물 중
    // 사용되지 않은 장애물 중
    // 자기 근처의 장애물 중
    // 장애물이 너무 타겟에게 가깝지 않은 것 중
    // 자신에게 가장 가까운 장애물을 반환한다.
    List<Obstacle> obstacles_cache = new();
    Obstacle GetValidObstacle(Entity target)
    {
        if (target == null) return null;

        Obstacle validObstacle = null;
        obstacles_cache.Clear();

        var obstacleCnt = EntityManager.GetEntitiesInRange(target.transform.position, Stat.AttackRange + FIND_OBSTACLE_RANGE_DELTA_FROM_TARGET, Team.Obstacle, obstacles_cache);
        if (obstacleCnt <= 0) return null;

        var nearestFromMeSqr = OBSTACLE_FIND_RANGE * OBSTACLE_FIND_RANGE; // 못해도 이것보단 가까워야한다.

        for (int i = obstacleCnt - 1; i >= 0; i--)
        {
            var o = obstacles_cache[i];
            if (o.User == this) return o; // 예약/사용 중인 내 엄폐물은 유지

            // 사용된 장애물은 제외
            if (o.IsUsing()) continue;

            // 타겟과의 거리가 너무 가까운 장애물은 제외
            if ((o.transform.position - target.transform.position).ToXZ().sqrMagnitude < MIN_OBSTACLE_DISTANCE_FROM_TARGET * MIN_OBSTACLE_DISTANCE_FROM_TARGET) continue;

            // 자기 근처의 장애물 중 가장 가까운 장애물을 반환!
            if ((o.transform.position - transform.position).ToXZ().sqrMagnitude <= nearestFromMeSqr)
            {
                nearestFromMeSqr = (o.transform.position - transform.position).ToXZ().sqrMagnitude;
                validObstacle = o;
            }
        }

        return validObstacle;
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
        }
    }

    class Chase : StateBase
    {
        const float REPATH_DISTANCE = 0.5f; // 타겟이 이만큼 움직이면 경로 재계산

        public Chase(Character c) : base(c) { }

        public override void OnEnter()
        {
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
            var destination = c.movement.Destination;
            var targetPos = c.nearestTarget.transform.position;
            if (c.nearestTarget != null && (destination - targetPos.ToXZ()).sqrMagnitude > REPATH_DISTANCE * REPATH_DISTANCE)
                c.movement.MoveTo(c.nearestTarget.transform.position);
        }
    }

    class GoToObstacle : StateBase
    {
        const float REPATH_DISTANCE = 0.5f; // 엄폐 위치가 이만큼 바뀌면 경로 재계산
        const float COVER_OFFSET = 1f;      // 엄폐물 중심에서 타겟 반대편으로 떨어질 거리
        const float ARRIVE_DISTANCE = 0.5f; // 엄폐 위치에 이만큼 가까우면 도착

        public GoToObstacle(Character c) : base(c) { }

        // 엄폐물 기준 타겟 반대편 위치
        Vector3 CoverPos()
        {
            var obstaclePos = c.Stat.JoinedObstacle.transform.position;
            return obstaclePos + (obstaclePos - c.nearestTarget.transform.position).SetY0().normalized * COVER_OFFSET;
        }

        public override void OnEnter()
        {
            c.movement.MoveTo(CoverPos());
            TestCharacterAnimation.StartMove(c.VisualBody);
        }

        public override void OnExit()
        {
            c.movement.Stop();
            TestCharacterAnimation.StopMove(c.VisualBody);
        }

        public override void OnUpdate()
        {
            if (c.Stat.JoinedObstacle == null || c.nearestTarget == null) return;

            var coverPos = CoverPos();

            // 도착 -> Arrive (다음 InCombatTick에서 attackFocus로 전환)
            if ((coverPos - c.transform.position).ToXZ().sqrMagnitude <= ARRIVE_DISTANCE * ARRIVE_DISTANCE)
            {
                c.Stat.JoinedObstacle.Arrive(c);
                return;
            }

            // 엄폐물이 바뀌었거나 타겟이 움직여 엄폐 위치가 바뀌면 재계산
            if ((c.movement.Destination - coverPos.ToXZ()).sqrMagnitude > REPATH_DISTANCE * REPATH_DISTANCE)
                c.movement.MoveTo(coverPos);
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
                BulletLineDrawer.DrawLine(c.VisualBody.position, c.nearestTarget.transform.position, Color.white, 0.1f);
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
            c.Stat.JoinedObstacle?.Leave(c);
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


