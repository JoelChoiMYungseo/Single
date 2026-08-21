using UnityEngine;
using UnityEngine.InputSystem;
using VRGame.Danmaku;

namespace VRGame.Survivor
{
    /// <summary>런 전체를 통제한다. 모든 시스템의 참조 연결과 초기화 시점을 여기서 한 번에 잡아
    /// 각 컴포넌트가 Start에서 서로를 찾다가 순서가 꼬이는 일을 없앤다.</summary>
    public class SurvivorRunManager : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerExperience playerExperience;
        [SerializeField] private AutoAttackWeapon autoAttack;

        [Header("Systems")]
        [SerializeField] private SwarmSpawner swarmSpawner;
        [SerializeField] private ExperienceGemPool gemPool;
        [SerializeField] private BulletPool bulletPool;
        [SerializeField] private UpgradeSystem upgradeSystem;
        [SerializeField] private SurvivorHUD hud;

        [Header("Boss")]
        [SerializeField] private GameObject bossObject;
        [SerializeField] private EnemyTarget bossTarget;
        [SerializeField] private float firstBossAtSeconds = 60f;
        [SerializeField] private float bossIntervalSeconds = 90f;
        [SerializeField] private float bossHealth = 1500f;
        [SerializeField] private float bossHealthGrowth = 1.6f;
        [SerializeField] private int bossExperienceValue = 40;
        [SerializeField] private float bossSpawnDistance = 16f;

        private float runElapsed;
        private float nextBossTime;
        private int bossesSpawned;
        private int killCount;
        private bool isRunning;

        private void Awake()
        {
            // 매니저가 소유한 오브젝트의 초기 상태를 여기서 확정한다.
            if (bossObject != null)
            {
                bossObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            playerHealth.OnHealthChanged -= HandleHealthChanged;
            playerHealth.OnHealthChanged += HandleHealthChanged;
            playerHealth.OnDied -= HandlePlayerDied;
            playerHealth.OnDied += HandlePlayerDied;

            playerExperience.OnExperienceChanged -= HandleExperienceChanged;
            playerExperience.OnExperienceChanged += HandleExperienceChanged;
            playerExperience.OnLeveledUp -= HandleLeveledUp;
            playerExperience.OnLeveledUp += HandleLeveledUp;

            swarmSpawner.OnEnemyKilled -= HandleEnemyKilled;
            swarmSpawner.OnEnemyKilled += HandleEnemyKilled;

            if (bossTarget != null)
            {
                bossTarget.OnDied -= HandleBossDied;
                bossTarget.OnDied += HandleBossDied;
            }
        }

        private void OnDisable()
        {
            playerHealth.OnHealthChanged -= HandleHealthChanged;
            playerHealth.OnDied -= HandlePlayerDied;
            playerExperience.OnExperienceChanged -= HandleExperienceChanged;
            playerExperience.OnLeveledUp -= HandleLeveledUp;
            swarmSpawner.OnEnemyKilled -= HandleEnemyKilled;

            if (bossTarget != null)
            {
                bossTarget.OnDied -= HandleBossDied;
            }
        }

        private void Start()
        {
            BeginRun();
        }

        public void BeginRun()
        {
            // static 레지스트리는 씬을 다시 로드해도 남을 수 있으므로 런 시작마다 비운다.
            EnemyRegistry.Clear();

            Time.timeScale = 1f;
            runElapsed = 0f;
            killCount = 0;
            bossesSpawned = 0;
            nextBossTime = firstBossAtSeconds;
            isRunning = true;

            if (bossObject != null)
            {
                bossObject.SetActive(false);
            }

            swarmSpawner.Bind(playerTransform, playerHealth);
            gemPool.Bind(playerTransform, playerStats, playerExperience);
            bulletPool.Bind(playerTransform, playerHealth);

            playerStats.ResetToBase();
            playerHealth.ResetForRun();
            playerExperience.ResetForRun();

            swarmSpawner.DespawnAll();
            gemPool.DespawnAll();
            bulletPool.DespawnAll();

            upgradeSystem.CancelAll();
            hud.HideGameOver();
            hud.SetLevel(playerExperience.Level);
            hud.SetKills(0);
            hud.SetTime(0f);

            autoAttack.SetFiring(true);
            swarmSpawner.BeginRun();
        }

        private void Update()
        {
            if (!isRunning)
            {
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                {
                    BeginRun();
                }
                return;
            }

            runElapsed += Time.deltaTime;
            hud.SetTime(runElapsed);

            if (runElapsed >= nextBossTime)
            {
                SpawnBoss();
            }
        }

        private void SpawnBoss()
        {
            nextBossTime = runElapsed + bossIntervalSeconds;

            if (bossObject == null || bossTarget == null || bossObject.activeSelf)
            {
                return;
            }

            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * bossSpawnDistance;
            // 보스 자신의 높이는 유지한다 — 플레이어 y를 그대로 쓰면 크기 차이 때문에 바닥에 파묻힌다.
            Vector3 spawnPosition = playerTransform.position + offset;
            spawnPosition.y = bossObject.transform.position.y;
            bossObject.transform.position = spawnPosition;

            float health = bossHealth * Mathf.Pow(bossHealthGrowth, bossesSpawned);
            bossesSpawned++;

            bossObject.SetActive(true);
            bossTarget.ResetHealth(health);
            bossTarget.SetExperienceValue(bossExperienceValue);
        }

        private void HandleBossDied(EnemyTarget target)
        {
            killCount++;
            hud.SetKills(killCount);
            gemPool.Spawn(target.transform.position, target.ExperienceValue);
            bossObject.SetActive(false);
        }

        private void HandleHealthChanged(float current, float max)
        {
            hud.SetHealth(current, max);
        }

        private void HandleExperienceChanged(int current, int required)
        {
            hud.SetExperience(current, required);
        }

        private void HandleEnemyKilled(EnemyTarget target)
        {
            killCount++;
            hud.SetKills(killCount);
        }

        private void HandleLeveledUp(int level)
        {
            hud.SetLevel(level);
        }

        private void HandlePlayerDied()
        {
            isRunning = false;
            autoAttack.SetFiring(false);
            swarmSpawner.StopRun();
            upgradeSystem.CancelAll();
            hud.ShowGameOver(runElapsed, killCount, playerExperience.Level);
        }
    }
}
