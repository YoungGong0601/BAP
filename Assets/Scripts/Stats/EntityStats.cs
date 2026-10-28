
using UnityEngine;

// entity stats
[System.Serializable]
public class EntityStats
{
    // types
    [SerializeField] public AttackType AttackType = AttackType.Normal;
    [SerializeField] public ArmorType ArmorType = ArmorType.Light;

    // ── Event ──────────────
    public event System.Action OnDead;

    // health
    [SerializeField] public Stat MaxHealth = new(100);
    float health;
    public float Health
    {
        get => health;
        set
        {
            bool wasAlive = health > 0f;
            health = value;
            if (wasAlive && health <= 0f) OnDead?.Invoke(); // 0 이하로 떨어지는 순간 1회
        }
    }

    //━━━━━━━━━━ Stats ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public Stat Defense;
    public Stat Evasion;
    // attack
    public Stat Damage, AttackRange, AttackSpeed, CriticalRate, CriticalDamage, Accuracy, Stability;
    // speed
    public Stat MoveSpeed;

    public bool CanAttack = true;
    public bool CanBeTargeted = true;
    public bool CanHit = true;
    public bool CanMove = true;


    //━━━━━━━━━━ Data ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    // FIXME : Public, Entity에다가 두는게 맞는지 고민 필요
    public Obstacle OccupiedObstacle;
}