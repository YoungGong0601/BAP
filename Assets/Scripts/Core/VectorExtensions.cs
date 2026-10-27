using UnityEngine;

// 3D 바닥(XZ 평면) 기준 변환. y축은 쓰지 않음
public static class VectorExtensions
{
    public static Vector2 ToXZ(this Vector3 v) => new(v.x, v.z);
    public static Vector3 SetY0(this Vector3 v) => new(v.x, 0f, v.z);
    // public static Vector3 ToInt(this Vector3 v) => new(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y), Mathf.FloorToInt(v.z));
}
