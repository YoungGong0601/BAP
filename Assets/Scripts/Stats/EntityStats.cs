
using UnityEngine;

// entity stats
[System.Serializable]
public class EntityStats
{
    // health
    public Stat Health, Defense, Evasion;
    // attack
    public Stat Damage, AttackRadius, AttackSpeed, CriticalRate, CriticalDamage, Accuracy, Stability;
    // speed
    public Stat MoveSpeed;

}