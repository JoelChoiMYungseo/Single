using System;
using UnityEngine;

namespace VRGame.Survivor
{
    public enum UpgradeKind
    {
        Damage,
        AttackSpeed,
        MoveSpeed,
        MaxHealth,
        ProjectileCount,
        PickupRadius,
        Heal,
    }

    /// <summary>레벨업 화면에 뜨는 선택지 하나. 데이터와 적용 로직을 한곳에 두어, 업그레이드가
    /// 늘어나도 UI나 매니저를 건드릴 필요가 없게 한다.</summary>
    [Serializable]
    public struct UpgradeDefinition
    {
        public UpgradeKind Kind;
        public string Title;
        public string Description;
        public float Value;

        public void Apply(PlayerStats stats, PlayerHealth health)
        {
            switch (Kind)
            {
                case UpgradeKind.Damage:
                    stats.AddDamageMultiplier(Value);
                    break;
                case UpgradeKind.AttackSpeed:
                    stats.AddAttackSpeedMultiplier(Value);
                    break;
                case UpgradeKind.MoveSpeed:
                    stats.AddMoveSpeedMultiplier(Value);
                    break;
                case UpgradeKind.MaxHealth:
                    stats.AddMaxHealth(Value);
                    health.Heal(Value);
                    break;
                case UpgradeKind.ProjectileCount:
                    stats.AddProjectile(Mathf.RoundToInt(Value));
                    break;
                case UpgradeKind.PickupRadius:
                    stats.AddPickupRadiusMultiplier(Value);
                    break;
                case UpgradeKind.Heal:
                    health.Heal(Value);
                    break;
            }
        }

        /// <summary>기본 선택지 풀. ScriptableObject로 뺄 만큼 수가 많아지면 그때 옮긴다.</summary>
        public static UpgradeDefinition[] CreateDefaultPool()
        {
            return new[]
            {
                new UpgradeDefinition { Kind = UpgradeKind.Damage,          Title = "화력 강화",   Description = "공격력 +25%",       Value = 0.25f },
                new UpgradeDefinition { Kind = UpgradeKind.AttackSpeed,     Title = "속사",        Description = "공격 속도 +20%",    Value = 0.20f },
                new UpgradeDefinition { Kind = UpgradeKind.MoveSpeed,       Title = "경보",        Description = "이동 속도 +15%",    Value = 0.15f },
                new UpgradeDefinition { Kind = UpgradeKind.MaxHealth,       Title = "체력 단련",   Description = "최대 체력 +25",     Value = 25f },
                new UpgradeDefinition { Kind = UpgradeKind.ProjectileCount, Title = "다중 사격",   Description = "탄환 +1발",         Value = 1f },
                new UpgradeDefinition { Kind = UpgradeKind.PickupRadius,    Title = "자력 확장",   Description = "획득 범위 +40%",    Value = 0.40f },
                new UpgradeDefinition { Kind = UpgradeKind.Heal,            Title = "응급 처치",   Description = "체력 40 회복",      Value = 40f },
            };
        }
    }
}
