using System;
using UnityEngine;
using VRGame.Combat;

namespace VRGame.Survivor
{
    /// <summary>적의 "맞는 쪽" 역할만 담당한다. 잡몹과 보스는 행동이 전혀 다르지만 조준·피격 대상이라는
    /// 점은 같으므로, 플레이어 자동조준이 이 컴포넌트 하나만 알면 되도록 분리했다.
    /// 활성/비활성 시점에 스스로 레지스트리를 갱신하므로 스포너가 등록을 잊을 일이 없다.</summary>
    [DisallowMultipleComponent]
    public class EnemyTarget : MonoBehaviour, IDamageable
    {
        [SerializeField] private float defaultMaxHealth = 20f;
        [SerializeField] private int experienceValue = 1;

        public event Action<EnemyTarget> OnDied;

        private float currentHealth;

        public bool IsDead { get; private set; }
        public int ExperienceValue => experienceValue;

        private void Awake()
        {
            currentHealth = defaultMaxHealth;
        }

        private void OnEnable()
        {
            EnemyRegistry.Register(this);
        }

        private void OnDisable()
        {
            EnemyRegistry.Unregister(this);
        }

        /// <summary>풀에서 꺼내 재사용할 때 체력을 다시 채운다.</summary>
        public void ResetHealth(float maxHealth)
        {
            currentHealth = maxHealth;
            IsDead = false;
        }

        public void SetExperienceValue(int value)
        {
            experienceValue = value;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead)
            {
                return;
            }

            currentHealth -= amount;
            if (currentHealth > 0f)
            {
                return;
            }

            IsDead = true;
            OnDied?.Invoke(this);
        }
    }
}
