using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using VRGame.Combat;

namespace VRGame.Weapons
{
    /// <summary>한 손으로 드는 레이캐스트 권총. 트리거를 당길 때마다(엣지 검출) 한 발 발사하고
    /// 시각적 반동과 손 햅틱을 재생한다. fireRate는 너무 빠른 연속 트리거를 막는 안전장치다.</summary>
    public class OneHandedGun : MonoBehaviour
    {
        [Header("Hand")]
        [SerializeField] private XRNode holdingHand = XRNode.RightHand;

        [Header("Shooting")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private float damage = 20f;
        [SerializeField] private float range = 100f;
        [SerializeField] private float fireRate = 6f;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Recoil")]
        [SerializeField] private float recoilKickAngle = 8f;
        [SerializeField] private float recoilRecoverySpeed = 10f;

        [Header("Haptics")]
        [SerializeField] private float hapticAmplitude = 0.6f;
        [SerializeField] private float hapticDuration = 0.08f;

        public UnityEvent OnFired;

        private InputDevice device;
        private bool wasTriggerPressed;
        private float nextFireTime;
        private Quaternion restLocalRotation;
        private Quaternion currentKickRotation;

        private void Awake()
        {
            if (muzzle == null)
            {
                muzzle = transform;
            }
            restLocalRotation = transform.localRotation;
            currentKickRotation = Quaternion.identity;
        }

        private void OnEnable()
        {
            device = InputDevices.GetDeviceAtXRNode(holdingHand);
            wasTriggerPressed = false;
        }

        private void Update()
        {
            if (!device.isValid)
            {
                device = InputDevices.GetDeviceAtXRNode(holdingHand);
            }

            if (device.isValid && device.TryGetFeatureValue(CommonUsages.triggerButton, out bool isPressed))
            {
                if (isPressed && !wasTriggerPressed && Time.time >= nextFireTime)
                {
                    Fire();
                }
                wasTriggerPressed = isPressed;
            }

            RecoverRecoil();
        }

        private void Fire()
        {
            nextFireTime = Time.time + 1f / fireRate;

            if (Physics.Raycast(muzzle.position, muzzle.forward, out RaycastHit hit, range, hitMask))
            {
                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                damageable?.TakeDamage(damage);
            }

            ApplyRecoil();
            SendHaptic();
            OnFired?.Invoke();
        }

        private void ApplyRecoil()
        {
            currentKickRotation = Quaternion.AngleAxis(-recoilKickAngle, Vector3.right);
            transform.localRotation = restLocalRotation * currentKickRotation;
        }

        private void RecoverRecoil()
        {
            currentKickRotation = Quaternion.Slerp(currentKickRotation, Quaternion.identity, recoilRecoverySpeed * Time.deltaTime);
            transform.localRotation = restLocalRotation * currentKickRotation;
        }

        private void SendHaptic()
        {
            if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            {
                device.SendHapticImpulse(0u, hapticAmplitude, hapticDuration);
            }
        }
    }
}
