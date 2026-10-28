using DG.Tweening;
using UnityEngine;

// 엄폐물 테스트용 트윈 연출 (애니메이션 대체)
// 사용 : TestObstacleAnimation.Break(visualBody);
public static class TestObstacleAnimation
{
    // ── 파괴 : 흔들림 -> 바닥으로 무너져 납작해짐 (잔해로 남음) ──────────────
    const float BREAK_SHAKE = 0.2f;     // 흔들림 세기
    const float BREAK_SHAKE_TIME = 0.25f;
    const float BREAK_FLAT = 0.15f;     // 무너진 뒤 y 비율
    const float BREAK_FALL_TIME = 0.3f;

    // 파괴. 한 번만 재생되는 연출이라 기준값 저장 없이 현재 값 기준
    public static void Break(Transform body)
    {
        body.DOKill();

        var pos = body.localPosition;
        var scale = body.localScale;

        DOTween.Sequence()
            .Append(body.DOShakePosition(BREAK_SHAKE_TIME, BREAK_SHAKE, 30))                                // 흔들림
            .Append(body.DOScaleY(scale.y * BREAK_FLAT, BREAK_FALL_TIME).SetEase(Ease.InBack))              // 무너짐
            .Join(body.DOLocalMoveY(pos.y * BREAK_FLAT, BREAK_FALL_TIME).SetEase(Ease.InBack))              // 피벗이 중앙이라 같이 내려서 바닥 고정
            .SetTarget(body);
    }
}
