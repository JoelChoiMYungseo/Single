using UnityEngine;
using UnityEngine.InputSystem;
using VRGame.Core;

namespace VRGame.Tower
{
    /// <summary>런 전체를 통제하고 HUD로 값을 밀어주는 허브. 각 시스템이 서로를 직접 찾지 않게 해서
    /// 초기화 순서 문제를 없앤다.
    ///
    /// 추락 사고가 나면 진행도를 전부 잃고 밑에서 다시 시작한다 — Only Up 계열의 잔혹함이 이 장르의
    /// 정체성이고, '절망 리액션'이라는 숏폼 소재 자체가 여기서 나온다. 대신 최고 도달 높이는 남겨
    /// 다시 도전할 이유를 준다.</summary>
    public class TowerRunManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerRig player;
        [SerializeField] private Transform startPoint;
        [SerializeField] private TowerManager tower;
        [SerializeField] private FallManager fallManager;
        [SerializeField] private TowerHUD hud;

        [Header("Rules")]
        [Tooltip("추락 사고 후 리스폰까지 기다리는 시간 (실제 시간 초) — 절망을 음미할 여유.")]
        [SerializeField] private float respawnDelay = 1.4f;
        [Tooltip("이 높이 아래로 떨어지면 사고로 처리한다 (안전망).")]
        [SerializeField] private float killPlaneY = -8f;
        [SerializeField] private Key restartKey = Key.R;

        [Header("Debug HUD")]
        [SerializeField] private int simulatedBudgetForDisplay = 64;

        public float BestHeight { get; private set; }

        private float respawnAt;
        private bool awaitingRespawn;

        private void OnEnable()
        {
            fallManager.OnCrashed -= HandleCrashed;
            fallManager.OnCrashed += HandleCrashed;
        }

        private void OnDisable()
        {
            fallManager.OnCrashed -= HandleCrashed;
        }

        private void Start()
        {
            ResetRun();
        }

        private void Update()
        {
            float height = player.Position.y;
            if (height > BestHeight)
            {
                BestHeight = height;
                hud.SetBest(BestHeight);
            }

            hud.SetHeight(height);
            hud.SetBudget(tower.SimulatedCount, simulatedBudgetForDisplay);
            hud.SetState(fallManager.IsBulletTime, GetHeldStrain());

            if (!awaitingRespawn && height < killPlaneY)
            {
                BeginRespawn();
            }

            if (awaitingRespawn && Time.unscaledTime >= respawnAt)
            {
                ResetRun();
            }

            if (Keyboard.current != null && Keyboard.current[restartKey].wasPressedThisFrame)
            {
                ResetRun();
            }
        }

        /// <summary>2026-09-17 세션에서 물리 손 등반을 팔스윙으로 교체하면서 "잡기(grab)" 개념
        /// 자체가 없어졌다 — 블록을 붙잡을 손이 없으니 미끄러짐 경고도 항상 0이다.</summary>
        private float GetHeldStrain()
        {
            return 0f;
        }

        private void HandleCrashed(float peakSpeed)
        {
            if (awaitingRespawn)
            {
                return;
            }

            Debug.Log($"[Tower] 추락 — 최고 낙하 속력 {peakSpeed:0.0} m/s");
            BeginRespawn();
        }

        private void BeginRespawn()
        {
            awaitingRespawn = true;
            respawnAt = Time.unscaledTime + respawnDelay;
        }

        public void ResetRun()
        {
            awaitingRespawn = false;

            // 순간이동은 PlayerRig 한 경로로만 한다 — 속도 초기화와 로코모션 상태 리셋이 함께 일어난다.
            player.Teleport(startPoint.position);

            tower.RebuildTower();

            hud.SetHeight(player.Position.y);
            hud.SetBest(BestHeight);
            hud.SetState(false, 0f);
        }
    }
}
