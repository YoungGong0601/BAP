using UnityEngine;

public enum AttackType { Normal = 0, Explosive = 1, Piercing = 2, Mystic = 3, Sonic = 4 }
public enum ArmorType { Light = 0, Heavy = 1, Special = 2, Elastic = 3 }
public enum AttackResult { Error = 0, Miss = 1, Hit = 1 << 1, Critical = 1 << 2, Weak = 1 << 3, Vulnerable = 1 << 4, Resist = 1 << 5, Blocked = 1 << 6 }
// LostTarget == Error

// 공격 적용
public static class AttackApplier
{
    // 명중하면 true, damage에 최종 데미지
    public static AttackResult Attack(Entity attacker, Entity target, out float damage)
    {
        var (a_stat, t_stat) = (attacker.Stat, target.Stat);
        damage = 0f;
        if (a_stat == null || t_stat == null) return AttackResult.Error;
        if (!a_stat.CanAttack || !t_stat.CanHit) return AttackResult.Error; // 공격 불가, 피격 불가 // FIXME: Miss로 해야하는가?

        // 명중률
        if (Random.value >= CombatFormula.GetHitRate(a_stat, t_stat)) return AttackResult.Miss; // 회피

        // 엄폐물
        // FIXME: 엄폐물 회피율 30%(default)로 고정됨. SS,S,A... 필요. 예외 없이 2배 데미지 적용인 점.
        if (target is not Obstacle && t_stat.JoinedObstacle != null && t_stat.JoinedObstacle.IsUserArrived)
        {
            if (Random.value < 0.3f) // 30% 확률로 엄폐물이 데미지를 받음
            {
                Attack(attacker, t_stat.JoinedObstacle, out var obsDamage);
                return AttackResult.Blocked; // 엄폐물 회피 성공
            }
        }

        if (!CombatFormula.CalculateDamage(attacker, target, out damage, out var result))
            return AttackResult.Error;

        t_stat.Health = Mathf.Max(0f, t_stat.Health - damage);

        return result;
    }
}

// 블루아카 데미지 공식
// 블아 수치 단위 (치명 피해 20000 = 200%)
public static class CombatFormula
{
    const float EVASION_CONST = 2000f;          // 명중률 = 2000 / (회피 - 명중 + 2000)
    const float CRITICAL_CONST = 666.66f;       // 치명 확률 = 치명 / (치명 + 666.66)
    const float CRITICAL_DAMAGE_UNIT = 10000f;  // 치명 피해 10000 = 100%
    const float STABILITY_CONST = 1000f;        // 최소 데미지 비율 = 안정 / (안정 + 1000) + 0.2
    const float STABILITY_MIN = 0.2f;
    const float DEFENSE_CONST = 1666.66f;       // 피해 감소 = 1 / (1 + 방어 / 1666.66)

    public static bool CalculateDamage(Entity attacker, Entity target, out float totalDamage, out AttackResult attackResult)
    {
        totalDamage = 0f;
        attackResult = AttackResult.Error;
        var a_stat = attacker.Stat;
        var t_stat = target.Stat;

        if (a_stat == null || t_stat == null) return false; // calculation failed
        totalDamage = a_stat.Damage;

        // ── 계산식 ──────────────

        // 치명
        if (Random.value < GetCriticalRate(a_stat))
        {
            totalDamage *= a_stat.CriticalDamage / CRITICAL_DAMAGE_UNIT;
            attackResult |= AttackResult.Critical;
        }

        // 안정 (최소 비율 ~ 100% 사이 랜덤)
        totalDamage *= Random.Range(GetStabilityMin(a_stat), 1f);

        // 방어
        totalDamage *= GetDefenseRatio(t_stat);

        // 상성
        var typeMultipleValue = TypeChart.GetMultiplier(a_stat.AttackType, t_stat.ArmorType);
        totalDamage *= typeMultipleValue;

        attackResult |=
            typeMultipleValue == 2f ? AttackResult.Weak :
            typeMultipleValue == 1.5f ? AttackResult.Vulnerable :
            typeMultipleValue == 0.5f ? AttackResult.Resist : AttackResult.Hit;

        return true; // calculation success
    }

    // ── 공식 ──────────────

    // 명중률 = 2000 / (회피 - 명중 + 2000)
    public static float GetHitRate(EntityStats attacker, EntityStats target)
    {
        float diff = target.Evasion - attacker.Accuracy;
        return diff <= 0f ? 1f : EVASION_CONST / (diff + EVASION_CONST);
    }

    // 치명 확률 = 치명 / (치명 + 666.66)
    public static float GetCriticalRate(EntityStats attacker)
    {
        float crit = Mathf.Max(0f, attacker.CriticalRate);
        return crit / (crit + CRITICAL_CONST);
    }

    // 최소 데미지 비율 = 안정 / (안정 + 1000) + 0.2
    public static float GetStabilityMin(EntityStats attacker)
    {
        float stability = Mathf.Max(0f, attacker.Stability);
        return Mathf.Min(1f, stability / (stability + STABILITY_CONST) + STABILITY_MIN);
    }

    // 피해 감소 = 1 / (1 + 방어 / 1666.66)
    public static float GetDefenseRatio(EntityStats target)
        => 1f / (1f + Mathf.Max(0f, target.Defense) / DEFENSE_CONST);
}


public static class TypeChart
{
    static readonly float[,] MULTIPLIER =
    {
        //             Light Heavy Special Elastic
        /*    Normal */{ 1.0f, 1.0f, 1.0f, 1.0f },
        /* Explosive */{ 2.0f, 1.0f, 0.5f, 0.5f },
        /*  Piercing */{ 0.5f, 2.0f, 1.0f, 1.0f },
        /*    Mystic */{ 1.0f, 0.5f, 2.0f, 1.0f },
        /*     Sonic */{ 1.0f, 0.5f, 1.5f, 2.0f },
    };

    public static float GetMultiplier(AttackType atk, ArmorType armor)
        => MULTIPLIER[(int)atk, (int)armor];
}