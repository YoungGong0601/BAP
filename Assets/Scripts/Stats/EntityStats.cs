
using UnityEngine;
using UnityEngine.Serialization;

// entity stats
[System.Serializable]
public class EntityStats
{
    // health
    [FormerlySerializedAs("Health")] public Stat MaxHealth;
    [System.NonSerialized] public float Health;
    public Stat Defense, Evasion;
    // attack
    public Stat Damage, AttackRange, AttackSpeed, CriticalRate, CriticalDamage, Accuracy, Stability;
    // speed
    public Stat MoveSpeed;

    public bool CanAttack = true;
    public bool CanBeTargeted = true;
    public bool CanHit = true;
    public bool CanMove = true;

}