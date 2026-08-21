using UnityEngine;

namespace VRGame.Core
{
    /// <summary>플레이어를 실제로 움직이는 주체. 게임 로직은 이 인터페이스만 알고, 그 구현이
    /// WASD 임시 조작인지 내가 만든 팔스윙인지 외부 MIT 패키지인지는 모른다.
    ///
    /// 로코모션을 교체할 때 게임 코드를 고치지 않으려면 두 가지만 계약으로 잡으면 된다:
    ///   1) 지금 얼마나 빠른가 (Velocity) — 적의 예측 사격·낙하 판정·연출이 쓴다
    ///   2) 순간이동됐다는 통보 (OnTeleported) — 리스폰 후 내부 상태를 리셋해야 하는 구현이 있다.
    ///      예를 들어 팔스윙 로코모션은 "지난 프레임 손 위치"를 들고 있어서, 몸이 순간이동하면
    ///      그 값이 거짓 속도를 만들어 플레이어를 로켓처럼 날려버린다.</summary>
    public interface ILocomotionDriver
    {
        Vector3 Velocity { get; }
        void OnTeleported(Vector3 position);
    }
}
