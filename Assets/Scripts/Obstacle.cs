// 최대 사용자 1인 엄폐물.
// 회피 30% (3000) 마지막 가능성이 쌓임. 즉 플레이어 명중 계산 후 초가로 30% 확률로 엄폐물이 대신 받아줌.
// user가 회피 시 -> 엄폐물이 데미지를 받음 (x2배)
public class Obstacle : Entity
{
    protected Entity user;
    public Entity User => user;
    public bool IsUsing() => user != null;
    public bool IsUserArrived { get; private set; } // Join은 예약, 도착해야 엄폐 효과

    public void Arrive(Entity _user)
    {
        if (user == _user) IsUserArrived = true;
    }

    public bool Join(Entity _user)
    {
        if (_user == null) return false;
        if (IsUsing()) return false;
        if (_user.Stat.JoinedObstacle != null) _user.Stat.JoinedObstacle.Leave(_user);
        SetUser(_user);
        return true;
    }

    public void Leave(Entity _user)
    {
        if (_user == null) return;
        if (user == _user)
            RemoveUser();
    }

    protected void SetUser(Entity _user)
    {
        user = _user;
        user.Stat.JoinedObstacle = this;
        IsUserArrived = false;
    }

    protected void RemoveUser()
    {
        user.Stat.JoinedObstacle = null;
        user = null;
        IsUserArrived = false;
    }


    protected override void OnDisable()
    {
        base.OnDisable();
        if (user != null) RemoveUser();
    }
}