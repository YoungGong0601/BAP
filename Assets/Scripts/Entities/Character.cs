using UnityEngine;
using UnityEngine.AI;

// 블루아카이브 캐릭터
[RequireComponent(typeof(NavMeshAgent))]
public class Character : Entity
{
    NavMeshAgent agent;
    NavMeshMovement movement;

    // test
    [SerializeField] GameObject destinationMarker;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        movement = new NavMeshMovement(this);
    }

    void Start()
    {
        // agent.enabled = false;
    }

    void Update()
    {
        movement.Tick(Time.deltaTime);

        // test
        if (Input.GetKeyDown(KeyCode.R))
        {
            movement.MoveTo(destinationMarker.transform.position);
        }
    }
}
