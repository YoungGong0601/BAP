using UnityEngine;
using UnityEngine.AI;

public class NavMeshMovement : Movement
{
    private readonly NavMeshAgent agent;

    public NavMeshMovement(Entity owner) : base(owner)
    {
        agent = owner.GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.stoppingDistance = ARRIVE_DISTANCE;
    }

    public override void MoveTo(Vector3 destination)
    {
        destination.y = 0f;
        Destination = destination.ToXZ();
        IsMoving = agent.SetDestination(destination);
    }

    public override void Tick(float delta)
    {
        if (!owner.Stat.CanMove)
        {
            Stop();
            return;
        }

        Vector3 moveDirection = agent.velocity;
        moveDirection.y = 0f;
        if (moveDirection.sqrMagnitude > 0.0001f)
            owner.transform.rotation = Quaternion.LookRotation(moveDirection);

        agent.speed = owner.Stat.MoveSpeed;
        if (IsMoving && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            Stop();

    }

    public override void Stop()
    {
        base.Stop();
        if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
    }
}