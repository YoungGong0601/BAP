using UnityEngine;

public enum InGameStateType
{
    None,       // 초기 상태
    EnterWave,  // 웨이브 시작 (걷기)
    InCombat,   // 웨이브 진행 중 (공격)
    Rest,       // 아군 대기
    Finish,
}

public class InGameState
{
    public InGameStateType CurrentState { get; private set; } = InGameStateType.EnterWave;

    public void ChangeState(InGameStateType newState)
    {
        CurrentState = newState;
    }
}