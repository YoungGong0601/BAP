using UnityEngine;

// 공격 적용
public static class AttackApplier
{
    // 명중하면 true, damage에 최종 데미지
    public static bool Attack(EntityStats attacker, EntityStats target, out float damage)
    {
        damage = 0f;
        if (attacker == null || target == null) return false;
        if (!attacker.CanAttack || !target.CanHit) return false; // 공격 불가, 피격 불가

        if (Random.value >= CombatFormula.GetHitRate(attacker, target)) return false; // 회피

        damage = CombatFormula.CalculateDamage(attacker, target);

        target.Health = Mathf.Max(0f, target.Health - damage);

        return true;
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

    public static float CalculateDamage(EntityStats attacker, EntityStats target)
    {
        if (attacker == null || target == null) return 0f;
        float totalDamage = attacker.Damage;

        // ── 계산식 ──────────────

        // 치명
        if (Random.value < GetCriticalRate(attacker))
            totalDamage *= attacker.CriticalDamage / CRITICAL_DAMAGE_UNIT;

        // 안정 (최소 비율 ~ 100% 사이 랜덤)
        totalDamage *= Random.Range(GetStabilityMin(attacker), 1f);

        // 방어
        totalDamage *= GetDefenseRatio(target);

        return totalDamage;
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
