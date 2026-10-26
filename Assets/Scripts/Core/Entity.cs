using System;
using UnityEngine;

[Flags]
public enum Team { None = 0, Me = 1, Enemy = 1 << 1 }

// Entity base class
public abstract class Entity : MonoBehaviour
{
    [SerializeField] public EntityStats stat;
    [SerializeField] public Team Team;

    public (Vector2Int cell, int idx) ManageValue = (new Vector2Int(int.MaxValue, int.MaxValue), -1);

    protected virtual void OnEnable()
    {
        EntityManager.Add(this);
    }

    protected virtual void OnDisable()
    {
        EntityManager.Remove(this);
    }

    protected virtual void LateUpdate()
    {
        EntityManager.Update(this);
    }
}