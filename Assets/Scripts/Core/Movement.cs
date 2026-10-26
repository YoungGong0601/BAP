using UnityEngine;

// movement base class for Entity
// 개발 시 전투 액션과, 애니메이션 공간을 생각해서 구조를 구성해보자
public abstract class Movement
{
    protected const float ARRIVE_DISTANCE = 0.1f; // 도착 거리

    protected readonly Entity owner;
    protected readonly Transform transform;

    public Vector2 Destination { get; protected set; } = Vector2.zero;
    public bool IsMoving { get; protected set; } = false;

    // ── 생성자 ──────────────
    public Movement(Entity owner)
    {
        this.owner = owner;
        this.transform = owner.transform;
    }

    //━━━━━━━━━━ Methods ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public abstract void MoveTo(Vector3 destination);
    public abstract void Tick(float delta);

    public virtual void Stop()
    {
        IsMoving = false;
    }

    public bool IsArrived()
     => ((Vector2)transform.position - Destination).sqrMagnitude <= ARRIVE_DISTANCE * ARRIVE_DISTANCE;


}