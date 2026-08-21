using UnityEngine;

namespace VRGame.Samurai
{
    /// <summary>칼로 벨 수 있는 대상. 절단면은 "어디를(point) 어느 평면으로(planeNormal)" 두 값으로
    /// 표현한다 — 실제 메쉬를 런타임에 자르지 않고 미리 나뉜 조각을 쓰더라도 이 정보만 있으면 된다.</summary>
    public interface ISliceable
    {
        /// <summary>이미 잘린 대상을 다시 자르지 않도록 슬라이서가 확인한다.</summary>
        bool CanBeSliced { get; }

        /// <summary>베었을 때 콤보에 더해질 점수. 대나무는 낮고 적은 높다.</summary>
        int ScoreValue { get; }

        /// <param name="point">칼날이 닿은 월드 좌표</param>
        /// <param name="planeNormal">절단 평면의 법선 (칼날 진행 방향 × 칼날 축)</param>
        /// <param name="sliceSpeed">절단 순간의 칼날 속력 — 조각이 튀는 세기에 쓴다</param>
        void Slice(Vector3 point, Vector3 planeNormal, float sliceSpeed);
    }
}
