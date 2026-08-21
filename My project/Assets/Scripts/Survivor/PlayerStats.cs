using System;
using UnityEngine;

namespace VRGame.Survivor
{
    /// <summary>레벨업 업그레이드로 변하는 플레이어 능력치를 한곳에 모은다. 다른 시스템(자동공격,
    /// 이동, 체력, 자석)은 여기서 최종값만 읽어가므로, 업그레이드 로직이 여기저기 흩어지지 않는다.</summary>
    public class PlayerStats : MonoBehaviour
    {
        [Header("Base Values")]
        [SerializeField] private float baseDamage = 10f;
        [SerializeField] private float baseAttacksPerSecond = 2.5f;
        [SerializeField] private float baseMoveSpeed = 6f;
        [SerializeField] private float baseMaxHealth = 100f;
        [SerializeField] private float basePickupRadius = 2.5f;
        [SerializeField] private float baseAttackRange = 14f;
        [SerializeField] private int baseProjectileCount = 1;

        private float damageMultiplier = 1f;
        private float attackSpeedMultiplier = 1f;
        private float moveSpeedMultiplier = 1f;
        private float maxHealthBonus;
        private float pickupRadiusMultiplier = 1f;
        private int projectileBonus;

        /// <summary>업그레이드가 적용돼 값이 바뀌었을 때. 체력바 등 최대치를 캐싱한 쪽이 구독한다.</summary>
        public event Action OnStatsChanged;

        public float Damage => baseDamage * damageMultiplier;
        public float AttacksPerSecond => baseAttacksPerSecond * attackSpeedMultiplier;
        public float MoveSpeed => baseMoveSpeed * moveSpeedMultiplier;
        public float MaxHealth => baseMaxHealth + maxHealthBonus;
        public float PickupRadius => basePickupRadius * pickupRadiusMultiplier;
        public float AttackRange => baseAttackRange;
        public int ProjectileCount => baseProjectileCount + projectileBonus;

        /// <summary>런을 다시 시작할 때 모든 업그레이드를 지운다.</summary>
        public void ResetToBase()
        {
            damageMultiplier = 1f;
            attackSpeedMultiplier = 1f;
            moveSpeedMultiplier = 1f;
            maxHealthBonus = 0f;
            pickupRadiusMultiplier = 1f;
            projectileBonus = 0;
            OnStatsChanged?.Invoke();
        }

        public void AddDamageMultiplier(float amount)
        {
            damageMultiplier += amount;
            OnStatsChanged?.Invoke();
        }

        public void AddAttackSpeedMultiplier(float amount)
        {
            attackSpeedMultiplier += amount;
            OnStatsChanged?.Invoke();
        }

        public void AddMoveSpeedMultiplier(float amount)
        {
            moveSpeedMultiplier += amount;
            OnStatsChanged?.Invoke();
        }

        public void AddMaxHealth(float amount)
        {
            maxHealthBonus += amount;
            OnStatsChanged?.Invoke();
        }

        public void AddPickupRadiusMultiplier(float amount)
        {
            pickupRadiusMultiplier += amount;
            OnStatsChanged?.Invoke();
        }

        public void AddProjectile(int amount)
        {
            projectileBonus += amount;
            OnStatsChanged?.Invoke();
        }
    }
}
