using UnityEngine;
using UnityEngine.XR;

namespace VRGame.Plunger
{
    /// <summary>PlungerLocomotion의 상태 전이를 구독해 햅틱·오디오만 재생하는 순수 프레젠테이션
    /// 레이어. 물리·게임 로직은 전혀 모른다 — MagneticLocomotion처럼 로코모션이 직접 햅틱을 보내는
    /// 대신, 이 프로토타입은 피드백을 별도 매니저로 분리한다.</summary>
    public class PlungerFeedback : MonoBehaviour
    {
        [Header("Haptics")]
        [SerializeField] private XRNode leftHandNode = XRNode.LeftHand;
        [SerializeField] private XRNode rightHandNode = XRNode.RightHand;
        [SerializeField] private float compressHapticAmplitude = 0.3f;
        [SerializeField] private float compressHapticDuration = 0.05f;
        [SerializeField] private float launchHapticAmplitude = 1f;
        [SerializeField] private float launchHapticDuration = 0.08f;

        [Header("Audio")]
        [SerializeField] private AudioSource leftHandAudio;
        [SerializeField] private AudioSource rightHandAudio;
        [SerializeField] private AudioClip suctionAttachClip;
        [SerializeField] private AudioClip launchPopClip;

        private PlungerLocomotion boundLocomotion;

        /// <summary>SceneBuilder가 플레이어 리그를 만든 뒤 호출해 이벤트를 연결한다.</summary>
        public void Bind(PlungerLocomotion locomotion)
        {
            Unbind();

            boundLocomotion = locomotion;
            if (boundLocomotion == null)
            {
                return;
            }

            boundLocomotion.OnStuck += HandleStuck;
            boundLocomotion.OnStateChanged += HandleStateChanged;
            boundLocomotion.OnLaunched += HandleLaunched;
        }

        private void Unbind()
        {
            if (boundLocomotion == null)
            {
                return;
            }
            boundLocomotion.OnStuck -= HandleStuck;
            boundLocomotion.OnStateChanged -= HandleStateChanged;
            boundLocomotion.OnLaunched -= HandleLaunched;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void HandleStuck(bool isLeft, Transform anchor)
        {
            PlayClip(isLeft, suctionAttachClip);
        }

        private void HandleStateChanged(bool isLeft, PlungerState state)
        {
            if (state != PlungerState.Compressed)
            {
                return;
            }
            SendHaptic(isLeft, compressHapticAmplitude, compressHapticDuration);
        }

        private void HandleLaunched(bool isLeft, Vector3 impulse)
        {
            PlayClip(isLeft, launchPopClip);
            SendHaptic(isLeft, launchHapticAmplitude, launchHapticDuration);
        }

        private void PlayClip(bool isLeft, AudioClip clip)
        {
            AudioSource source = isLeft ? leftHandAudio : rightHandAudio;
            if (source != null && clip != null)
            {
                source.PlayOneShot(clip);
            }
        }

        private void SendHaptic(bool isLeft, float amplitude, float duration)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(isLeft ? leftHandNode : rightHandNode);
            if (!device.isValid || !device.TryGetHapticCapabilities(out HapticCapabilities caps) || !caps.supportsImpulse)
            {
                return;
            }
            device.SendHapticImpulse(0u, amplitude, duration);
        }
    }
}
