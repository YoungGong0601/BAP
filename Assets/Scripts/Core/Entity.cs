using System;
using UnityEngine;

[Flags]
public enum Team { None = 0, Me = 1, Enemy = 1 << 1, Allied = 1 << 2, Neutral = 1 << 3, Obstacle = 1 << 4 }

// Entity base class
public abstract class Entity : MonoBehaviour
{
    // FIXME: 전체 공개이지 않도록. getter만 제공하도록 수정 필요
    [SerializeField] public EntityStats Stat;
    [SerializeField] public Team Team;
    [SerializeField] public Transform VisualBody;


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