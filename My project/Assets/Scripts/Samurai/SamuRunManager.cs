using UnityEngine;
using UnityEngine.InputSystem;

namespace VRGame.Samurai
{
    /// <summary>런 전체를 통제하고 HUD로 값을 밀어주는 허브. 각 시스템이 서로를 직접 찾지 않게 해서
    /// 초기화 순서 문제를 없애고, 어떤 값이 어디로 흐르는지 한 파일에서 읽히게 한다.</summary>
    public class SamuRunManager : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private Transform player;
        [SerializeField] private BambooExploration exploration;
        [SerializeField] private AscentZoneManager zones;
        [SerializeField] private RisingHazard hazard;
        [SerializeField] private ComboTracker combo;
        [SerializeField] private EnemySpawnDirector enemies;
        [SerializeField] private SamuHUD hud;

        [Header("Restart")]
        [Tooltip("임시 조작에서 런을 처음부터 다시 시작하는 키.")]
        [SerializeField] private Key restartKey = Key.R;

        private void OnEnable()
        {
            zones.OnZoneChanged -= HandleZoneChanged;
            zones.OnZoneChanged += HandleZoneChanged;

            combo.OnChanged -= HandleComboChanged;
            combo.OnChanged += HandleComboChanged;

            exploration.OnRespawned -= HandleRespawned;
            exploration.OnRespawned += HandleRespawned;
        }

        private void OnDisable()
        {
            zones.OnZoneChanged -= HandleZoneChanged;
            combo.OnChanged -= HandleComboChanged;
            exploration.OnRespawned -= HandleRespawned;
        }

        private void Start()
        {
            hud.SetFalls(0);
            hud.SetScore(0);
            hud.SetCombo(0, 1);
        }

        private void Update()
        {
            // 매 프레임 바뀌는 값은 이벤트로 만들 이유가 없어 여기서 폴링해 밀어준다.
            hud.SetHeight(player.position.y);
            hud.SetHazardGap(hazard.Gap);

            if (Keyboard.current != null && Keyboard.current[restartKey].wasPressedThisFrame)
            {
                RestartRun();
            }
        }

        public void RestartRun()
        {
            exploration.ResetRun();
            hazard.ResetHazard();
            combo.ResetRun();
            enemies.DespawnAll();

            hud.SetFalls(0);
            hud.SetScore(0);
            hud.SetCombo(0, 1);
        }

        private void HandleZoneChanged(int index, AscentZone zone)
        {
            hud.SetZone(index, zones.ZoneCount, zone.Name);
        }

        private void HandleComboChanged(int comboCount, int multiplier, int score)
        {
            hud.SetCombo(comboCount, multiplier);
            hud.SetScore(score);
        }

        private void HandleRespawned(Vector3 position)
        {
            hud.SetFalls(exploration.FallCount);
        }
    }
}
