using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// VisualBody 테스트용 트윈 연출 (애니메이션 대체)
// 기준값(위치/스케일/회전)은 처음 호출할 때 저장. 같은 연출이 겹치면 이전 것을 끊고 기준값에서 다시 시작
// 사용 : TestCharacterAnimation.Shoot(visualBody, target.position);
public static class TestCharacterAnimation
{
    // ── 총격 : 타겟 반대쪽으로 젖혔다 복귀 ──────────────
    const float SHOOT_TILT = 12f;      // 젖히는 각도
    const float SHOOT_OUT = 0.05f;
    const float SHOOT_BACK = 0.15f;

    // ── 피격 : y 눌렸다 튀어오름 ──────────────
    const float HIT_SQUASH = 0.8f;     // 눌릴 때 y 비율
    const float HIT_DOWN = 0.05f;
    const float HIT_UP = 0.2f;

    // ── 이동 : x 늘었다 줄었다 반복 ──────────────
    const float MOVE_STRETCH = 1.1f;   // 늘어날 때 x 비율
    const float MOVE_HALF = 0.15f;     // 한 번 늘어나는 시간 (왕복 = x2)

    // ── 사망 : 튀어올랐다 위로 끌려 올라감 (블아 헬기 회수) ──────────────
    const float DEAD_HOP = 0.5f;       // 처음 튀어오르는 높이
    const float DEAD_RISE = 10f;       // 최종 높이
    const float DEAD_TIME = 1.2f;      // 끌려 올라가는 시간

    class Body
    {
        public Vector3 pos, scale;
        public Quaternion rot;
        public Tween shoot, hit, move, dead;
    }

    static readonly Dictionary<Transform, Body> bodies = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        bodies.Clear();
    }

    //━━━━━━━━━━ API ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    // 총격 반동. targetPos 반대쪽으로 젖힘
    public static void Shoot(Transform body, Vector3 targetPos)
    {
        var b = Get(body);
        if (b.dead != null) return;

        b.shoot?.Kill();
        body.localRotation = b.rot;

        var dir = targetPos - body.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        // 부모 로컬 기준 축 (타겟 방향과 위쪽의 수직축)
        var localDir = body.parent != null ? body.parent.InverseTransformDirection(dir.normalized) : dir.normalized;
        var axis = Vector3.Cross(Vector3.up, localDir);
        var tilted = Quaternion.AngleAxis(-SHOOT_TILT, axis) * b.rot;

        b.shoot = DOTween.Sequence()
            .Append(body.DOLocalRotateQuaternion(tilted, SHOOT_OUT).SetEase(Ease.OutQuad))
            .Append(body.DOLocalRotateQuaternion(b.rot, SHOOT_BACK).SetEase(Ease.OutBack))
            .SetTarget(body);
    }

    // 피격. y가 눌렸다가 OutBack으로 살짝 넘치며 튀어오름
    public static void Hit(Transform body)
    {
        var b = Get(body);
        if (b.dead != null) return;

        b.hit?.Kill();
        SetSquash(body, b, 1f);

        b.hit = DOTween.Sequence()
            .Append(DOVirtual.Float(1f, HIT_SQUASH, HIT_DOWN, r => SetSquash(body, b, r)).SetEase(Ease.OutQuad))
            .Append(DOVirtual.Float(HIT_SQUASH, 1f, HIT_UP, r => SetSquash(body, b, r)).SetEase(Ease.OutBack))
            .SetTarget(body);
    }

    // 이동 시작. StopMove 전까지 반복
    public static void StartMove(Transform body)
    {
        var b = Get(body);
        if (b.dead != null || b.move != null) return;

        b.move = body.DOScaleX(b.scale.x * MOVE_STRETCH, MOVE_HALF)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetTarget(body);
    }

    public static void StopMove(Transform body)
    {
        var b = Get(body);
        b.move?.Kill();
        b.move = null;

        var s = body.localScale;
        s.x = b.scale.x;
        body.localScale = s;
    }

    // 사망. 다른 연출 전부 끊고 움츠림 -> 툭 튀어오름 -> 살짝 처짐 -> 위로 끌려 올라감
    public static void Dead(Transform body)
    {
        var b = Get(body);
        if (b.dead != null) return;

        KillAll(b);
        Restore(body, b);

        b.dead = DOTween.Sequence()
            .Append(DOVirtual.Float(1f, HIT_SQUASH, 0.08f, r => SetSquash(body, b, r)))                   // 움츠림
            .Append(DOVirtual.Float(HIT_SQUASH, 1f, 0.12f, r => SetSquash(body, b, r)))
            .Append(body.DOLocalMoveY(b.pos.y + DEAD_HOP, 0.25f).SetEase(Ease.OutQuad))                    // 툭 튀어오름
            .Append(body.DOLocalMoveY(b.pos.y + DEAD_HOP * 0.6f, 0.15f).SetEase(Ease.InOutQuad))           // 줄에 걸려 살짝 처짐
            .Append(body.DOLocalMoveY(b.pos.y + DEAD_RISE, DEAD_TIME).SetEase(Ease.InQuad))                // 끌려 올라감
            .SetTarget(body);
    }

    // 연출 전부 중단 + 기준값 복구 + 등록 해제 (재사용, 파괴 전 호출)
    public static void Release(Transform body)
    {
        if (!bodies.Remove(body, out var b)) return;

        KillAll(b);
        if (body != null) Restore(body, b);
    }

    // ── Methods ──────────────

    static Body Get(Transform body)
    {
        if (!bodies.TryGetValue(body, out var b))
        {
            b = new Body { pos = body.localPosition, scale = body.localScale, rot = body.localRotation };
            bodies[body] = b;
        }
        return b;
    }

    // y 스케일 비율 적용. 피벗이 몸 중앙이라 위치 y도 같은 비율로 낮춰서 발을 바닥에 고정
    // (기준 위치 y = 몸 높이의 절반일 때 정확. 지금 VisualBody : y 0.75, scale y 1.5)
    static void SetSquash(Transform body, Body b, float ratio)
    {
        var s = body.localScale;
        s.y = b.scale.y * ratio;
        body.localScale = s;

        var p = body.localPosition;
        p.y = b.pos.y * ratio;
        body.localPosition = p;
    }

    static void KillAll(Body b)
    {
        b.shoot?.Kill();
        b.hit?.Kill();
        b.move?.Kill();
        b.dead?.Kill();
        b.move = null;
        b.dead = null;
    }

    static void Restore(Transform body, Body b)
    {
        body.localPosition = b.pos;
        body.localScale = b.scale;
        body.localRotation = b.rot;
    }
}
