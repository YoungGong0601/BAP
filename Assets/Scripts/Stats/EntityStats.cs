
using System;
using UnityEngine;
using UnityEngine.Serialization;

// entity stats
[System.Serializable]
public class EntityStats
{
    // types
    [SerializeField] public AttackType AttackType = AttackType.Normal;
    [SerializeField] public ArmorType ArmorType = ArmorType.Light;

    // health
    [SerializeField] public Stat MaxHealth = new(100);
    [HideInInspector] public float Health;
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


    // Action
    public Obstacle JoinedObstacle;

}