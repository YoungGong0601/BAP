using System;
using System.Collections.Generic;
using UnityEngine;

// Stat System v2.0
// 계산 공식 (rank마다 순서대로) : ((value + Flat) + (value * PercentAdd)) * Multiplier

// 값(정수)은 직렬화(UC_StackAble_DamageUP.modifierType)에 저장되므로 바꾸지 말 것
public enum ModifierType { Flat = 0, PercentAdd = 100, Multiplier = 200 }

// Rank가 필요할 때 enum에 추가. 값(= 숫자)은 지정하지 말 것 (선언 순서 = 적용 순서 = 버킷 배열 인덱스)
public enum ModRank { Base, Default, Second, Third, Fourth, Final }

//━━━━━━━━━━ Stat ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Serializable]
public class Stat
{
    static readonly int RankCount = Enum.GetValues(typeof(ModRank)).Length;

    // 기본값 (인스펙터에서만 수정)
    [SerializeField] float _baseValue;
    public float BaseValue => _baseValue;

    // ** Modifiers **
    readonly Dictionary<string, Mod> _mods = new();

    // bucket (각 rank 별 modType 별 bucket)
    struct Bucket { public float flat, percent, multiply; }
    readonly Bucket[] _buckets = new Bucket[RankCount];

    // 나를 참조하는 외부 스탯
    readonly List<Stat> _dependents = new();

    bool _dirty = true; // 버킷 재계산 필요 flag
    bool _evaluating; // 순환 참조 flag (계산 중 on flag)

#if UNITY_EDITOR
    static readonly List<string> s_refPath = new(); // 지금 평가 중인 참조 키 경로.
#endif

    // 생성자
    public Stat(float baseValue) { _baseValue = baseValue; }
    public Stat() : this(0) { }

    // Value
    public static implicit operator float(Stat s) => s.Value;

    public float Value
    {
        get
        {
            if (_dirty) {
                if (_evaluating) {
#if UNITY_EDITOR
                    Debug.LogError($"[Stat] 순환 참조 BUG: {string.Join(" -> ", s_refPath)}"); // 순환 오류 log 출력
                    Debug.Break();
#else
                    Debug.LogError("[Stat] 순환 참조 BUG.");
#endif
                    return _baseValue;
                }
                _evaluating = true;
                try { RebuildBuckets(); }
                finally { _evaluating = false; }
            }

            float value = _baseValue;
            foreach (var b in _buckets) // 안 쓰는 rank는 (0, 0, 1)
                value = ((value + b.flat) + (value * b.percent)) * b.multiply;
            return value;
        }
    }

    // ── Dirty methods ──────────────

    void MarkDirty()
    {
        if (_dirty) return;
        _dirty = true;
        foreach (var d in _dependents) d.MarkDirty();
    }

    // ── Modifier methods ──────────────

    // Add + Modify + Remove
    public bool Set(string key, Mod? mod)
    {
        if (mod == null) return Remove(key);
        return _mods.ContainsKey(key) ? Modify(key, mod.Value) : Add(key, mod.Value);
    }

    bool Add(string key, Mod mod)
    {
        if (!_mods.TryAdd(key, mod)) return false;
        mod.source?._dependents.Add(this);
        MarkDirty();
        return true;
    }

    bool Modify(string key, Mod mod)
    {
        if (!_mods.TryGetValue(key, out var old)) return false;
        old.source?._dependents.Remove(this);
        _mods[key] = mod;
        mod.source?._dependents.Add(this);
        MarkDirty();
        return true;
    }

    public bool Remove(string key)
    {
        if (!_mods.Remove(key, out var old)) return false;
        old.source?._dependents.Remove(this);
        MarkDirty();
        return true;
    }

    public bool TryGet(string key, out Mod mod) => _mods.TryGetValue(key, out mod);

    // 디버그 창 표시용 (읽기 전용)
    public IReadOnlyDictionary<string, Mod> Mods => _mods;

    // ── build buckets ──────────────

    void RebuildBuckets()
    {
        for (int i = 0; i < RankCount; i++) _buckets[i] = new Bucket { multiply = 1 }; // flat, percent는 0
        foreach (var (key, mod) in _mods) {
            ref var b = ref _buckets[(int)mod.rank];
#if UNITY_EDITOR
            float v;
            if (mod.source != null) {
                s_refPath.Add(key); // 순환 오류 log 경로용. 에디터에서만 작동
                try { v = mod.GetValue(); }
                finally { s_refPath.RemoveAt(s_refPath.Count - 1); }
            }
            else v = mod.GetValue();
#else
            float v = mod.GetValue();
#endif
            switch (mod.type) {
                case ModifierType.Flat: b.flat += v; break;
                case ModifierType.PercentAdd: b.percent += v; break;
                case ModifierType.Multiplier: b.multiply *= v; break;
            }
        }
        _dirty = false;
    }

    // ── Debug ──────────────

    public string PrintBuckets(string naming = "")
    {
        if (_dirty) RebuildBuckets();

        string log = $"{naming} Buckets:";
        float total = _baseValue;
        for (int i = 0; i < RankCount; i++) {
            var b = _buckets[i];
            total = ((total + b.flat) + (total * b.percent)) * b.multiply;
            log += $"\n{(ModRank)i}:=> {total} ( add = {b.flat} , pct = {b.percent} , multi={b.multiply} )";
        }
        return log;
    }
}

// Modifier (struct)
public readonly struct Mod
{
    public readonly float value;
    public readonly ModifierType type;
    public readonly ModRank rank;
    public readonly Stat source;    // null -> fixed value, not null -> reference value

    //━━━━━━━━━━ 생성 static ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    Mod(float value, ModifierType type, ModRank rank, Stat source)
        => (this.value, this.type, this.rank, this.source) = (value, type, rank, source);

    // default Modifier
    public static Mod Fixed(float value, ModifierType type, ModRank rank = ModRank.Default) => new(value, type, rank, null);
    // source Modifier
    public static Mod Ref(Stat source, float ratio, ModifierType type, ModRank rank = ModRank.Default) => new(ratio, type, rank, source);

    //━━━━━━━━━━ API ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public float GetValue() => source != null ? source.Value * value : value;
}
