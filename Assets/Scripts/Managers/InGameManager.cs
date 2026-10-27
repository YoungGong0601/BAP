using UnityEngine;

public class InGameManager : MonoBehaviour
{
    public static InGameManager Instance { get; private set; }
    public InGameState State;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        State = new InGameState();
    }

    private void Start()
    {
        State.ChangeState(InGameStateType.InCombat);
    }
}