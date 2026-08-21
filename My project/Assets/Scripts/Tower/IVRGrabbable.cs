using UnityEngine;

namespace VRGame.Tower
{
    /// <summary>손으로 붙잡을 수 있는 물리 오브젝트. 손 컨트롤러는 이 인터페이스만 알면 되므로,
    /// 붙잡히는 쪽이 흔들리는 블록인지 고정 손잡이인지 밧줄인지 몰라도 된다.
    ///
    /// 핵심은 <see cref="ApplyLoad"/>다 — 손은 매 물리 스텝마다 자기가 대상에 걸고 있는 하중을
    /// 통보하고, 대상은 그 하중으로 부서질지 스스로 판단한다. 손이 "이 블록은 부서져야 한다"를
    /// 결정하지 않기 때문에 블록 종류를 아무리 늘려도 손 코드는 그대로다.</summary>
    public interface IVRGrabbable
    {
        /// <summary>붙잡을 때 조인트를 연결할 대상. 정적 지형이면 null을 반환할 수 있다.</summary>
        Rigidbody Body { get; }

        /// <summary>이미 부서졌거나 잡을 수 없는 상태면 false.</summary>
        bool CanBeGrabbed { get; }

        /// <summary>손이 잡는 지점을 대상이 보정할 수 있게 한다 (모서리로 손이 빨려드는 것 방지).</summary>
        Vector3 GetGripPoint(Vector3 handPosition);

        void OnGrabbed(VRHandController hand);
        void OnReleased(VRHandController hand);

        /// <summary>매 물리 스텝 호출. 이 하중이 임계치를 넘으면 대상이 스스로 부서진다.</summary>
        /// <param name="force">대상에 걸린 힘 (N 단위 근사)</param>
        /// <param name="point">힘이 걸린 월드 좌표</param>
        void ApplyLoad(Vector3 force, Vector3 point);
    }
}
