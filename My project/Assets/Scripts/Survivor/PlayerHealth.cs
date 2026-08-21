using System;
using UnityEngine;
using VRGame.Combat;

namespace VRGame.Survivor
{
    /// <summary>플레이어 체력. 적 탄막·접촉 데미지가 IDamageable을 통해 여기로 들어온다.
    /// 이동 방식(임시 WASD / 추후 VR 리그)과 무관하게 동작하도록 이동 코드와 분리해 둔다.</summary>
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private float invulnerabilitySeconds = 0.4f;

        /// <summary>현재 체력, 최대 체력</summary>
        public event Action<float, float> OnHealthChanged;
        public event Action OnDied;

        private PlayerStats stats;
        private float currentHealth;
        private float nextVulnerableTime;

        public bool IsDead => currentHealth <= 0f;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
        }

        private void OnEnable()
        {
            stats.OnStatsChanged -= HandleStatsChanged;
            stats.OnStatsChanged += HandleStatsChanged;
        }

        private void OnDisable()
        {
            stats.OnStatsChanged -= HandleStatsChanged;
        }

        /// <summary>런 시작 시 매니저가 호출한다. 자기 Start에서 초기화하면 다른 시스템과 순서가 꼬인다.</summary>
        public void ResetForRun()
        {
            currentHealth = stats.MaxHealth;
            nextVulnerableTime = 0f;
            OnHealthChanged?.Invoke(currentHealth, stats.MaxHealth);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || Time.time < nextVulnerableTime)
            {
                return;
            }

            nextVulnerableTime = Time.time + invulnerabilitySeconds;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, stats.MaxHealth);

            if (currentHealth <= 0f)
            {
                OnDied?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead)
            {
                return;
            }

            currentHealth = Mathf.Min(stats.MaxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, stats.MaxHealth);
        }

        /// <summary>최대 체력 업그레이드를 받으면 늘어난 만큼 즉시 채워준다 — 안 그러면
        /// "최대 HP +20"을 골랐는데 체감상 아무 일도 안 일어난 것처럼 보인다.</summary>
        private void HandleStatsChanged()
        {
            OnHealthChanged?.Invoke(currentHealth, stats.MaxHealth);
        }
    }
}
