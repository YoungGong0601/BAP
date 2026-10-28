// 최대 사용자 1인 엄폐물.
// 회피 30% (3000) 마지막 가능성이 쌓임. 즉 플레이어 명중 계산 후 초가로 30% 확률로 엄폐물이 대신 받아줌.
// user가 회피 시 -> 엄폐물이 데미지를 받음 (x2배)

// 흐름 : Hold(예약, 도착 예정) -> Occupy(점유, 도착 후) -> Leave(스스로 떠남)
// 강제로 잃는 경우(더 가까운 사람에게 뺏김, 파괴)만 onLost 콜백 호출. 스스로 떠날 땐 호출 안 함

using System;
using UnityEngine;

public class Obstacle : Entity
{
    public enum State { NotUsed, Reserved, Occupied, Broken }

    public const float REPLACE_RESERVER_DISTANCE_OFFSET = 0.8f; // 예약자 교체 가능 추가 거리 (갈아타기에도 사용)

    State currentState = State.NotUsed;
    public State CurrentState => currentState;
    Entity holder;   // 예약자
    Entity occupant; // 점유자

    Action onLost; // 강제로 잃었을 때 (뺏김, 파괴)

    public float sqrDistanceToReserver => holder != null ? (holder.transform.position - transform.position).sqrMagnitude : float.MaxValue;
    public float sqrDistanceTo(Entity entity) => entity != null ? (entity.transform.position - transform.position).sqrMagnitude : float.MaxValue;
    public bool isOccupied => occupant != null;

    void Awake()
    {
        Stat.Health = Stat.MaxHealth;
        Stat.OnDead += Break;
    }

    // 파괴 (체력 0)
    public void Break()
    {
        if (currentState == State.Broken) return;

        Kick();

        currentState = State.Broken;
        Stat.CanHit = false;
        Stat.CanBeTargeted = false;

        TestObstacleAnimation.Break(VisualBody != null ? VisualBody : transform);
    }

    public bool canHold(Entity entity)
    {
        if (currentState == State.Broken) return false;
        if (occupant != null) return occupant == entity; // 점유 중이면 점유자만
        if (holder == null || holder == entity) return true; // 비었거나 이미 내 예약

        // 예약자보다 offset 이상 가까우면 뺏기 가능
        return Mathf.Sqrt(sqrDistanceTo(entity)) + REPLACE_RESERVER_DISTANCE_OFFSET < Mathf.Sqrt(sqrDistanceToReserver);
    }

    // 홀딩 (도착 예정) 기존 예약자보다 충분히 가까우면 뺏음
    public bool Hold(Entity entity, Action onLost)
    {
        if (!canHold(entity)) return false;
        if (holder == entity || occupant == entity) return true; // 이미 내 것

        Kick(); // 기존 예약자에게 뺏김 알림

        this.onLost = onLost;
        holder = entity;
        currentState = State.Reserved;
        return true;
    }

    // 점유 (도착 후 점유) 예약자만 가능
    public bool Occupy(Entity entity)
    {
        if (holder != entity) return false;

        holder = null;
        occupant = entity;
        currentState = State.Occupied;
        return true;
    }

    // 스스로 떠남 (예약 취소 / 점유 해제)
    public void Leave(Entity entity)
    {
        if (holder != entity && occupant != entity) return;

        holder = null;
        occupant = null;
        onLost = null;
        currentState = State.NotUsed;
    }

    // 강제 해지
    void Kick()
    {
        var callback = onLost;

        holder = null;
        occupant = null;
        onLost = null;
        currentState = State.NotUsed;

        callback?.Invoke();
    }
}
