using UnityEngine;
using UnityEngine.InputSystem;
using VRGame.Core;

namespace VRGame.Magnetic
{
    /// <summary>런 전체를 통제하고 HUD로 값을 밀어주는 허브. 다른 시스템이 서로를 직접 찾지 않게
    /// 해서 초기화 순서 문제를 없앤다.</summary>
    public class MagneticRunManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerRig player;
        [SerializeField] private CrashHandler crashHandler;
        [SerializeField] private Transform startPoint;
        [SerializeField] private MagneticHUD hud;

        [Header("Safety Net")]
        [Tooltip("이 높이 아래로 떨어지면 시작 지점으로 되돌린다 — 아레나 밖으로 튕겨나가는 것 방지.")]
        [SerializeField] private float killPlaneY = -20f;

        [Header("Input")]
        [SerializeField] private Key restartKey = Key.R;

        private int crashCount;

        private void OnEnable()
        {
            crashHandler.OnCrashed -= HandleCrashed;
            crashHandler.OnCrashed += HandleCrashed;
        }

        private void OnDisable()
        {
            crashHandler.OnCrashed -= HandleCrashed;
        }

        private void Start()
        {
            ResetRun();
        }

        private void Update()
        {
            // 2026-09-17 세션에서 로코모션을 팔스윙으로 교체하면서 극성/하자드 견인 개념이
            // 사라졌다 — 그 HUD 항목(SetPolarity/SetWarning)은 더 이상 갱신하지 않는다.
            hud.SetSpeed(player.Velocity.magnitude);

            if (player.Position.y < killPlaneY)
            {
                ResetRun();
            }

            if (Keyboard.current != null && Keyboard.current[restartKey].wasPressedThisFrame)
            {
                ResetRun();
            }
        }

        private void HandleCrashed(float impactSpeed)
        {
            crashCount++;
            hud.SetCrashCount(crashCount);
        }

        public void ResetRun()
        {
            player.Teleport(startPoint.position);
        }
    }
}
