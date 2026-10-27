using System;
using UnityEngine;

namespace VRGame.Samurai
{
    /// <summary>높이에 따른 난이도 구역 하나. 수직 레벨에서는 "얼마나 올라갔는가"가 곧 진행도이므로
    /// 별도의 스테이지 전환 없이 고도만으로 레벨을 나눈다.</summary>
    [Serializable]
    public struct AscentZone
    {
        public string Name;
        public float StartY;
        public int MaxEnemies;
        public float EnemySpawnInterval;
        public float EnemySpeedMultiplier;
        [Tooltip("아래에서 쫓아오는 위협이 초당 올라오는 높이 (m/s).")]
        public float HazardRiseSpeed;
    }

    /// <summary>플레이어 고도로 현재 구역을 판정하고 바뀔 때 알린다. 스포너와 위협이 이 값을 읽어
    /// 스스로 난이도를 올리므로, 난이도 곡선을 한 곳에서만 수정할 수 있다.</summary>
    public class AscentZoneManager : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private AscentZone[] zones = Array.Empty<AscentZone>();

        /// <summary>구역 인덱스, 구역 데이터</summary>
        public event Action<int, AscentZone> OnZoneChanged;

        public int CurrentZoneIndex { get; private set; }
        public int HighestZoneReached { get; private set; }
        public AscentZone CurrentZone => zones.Length > 0 ? zones[CurrentZoneIndex] : default;
        public int ZoneCount => zones.Length;

        private void Start()
        {
            // 시작 구역도 이벤트로 한 번 알린다 — 구독자가 초기 상태를 따로 조회하지 않게 한다.
            CurrentZoneIndex = ResolveZoneIndex(player.position.y);
            HighestZoneReached = CurrentZoneIndex;
            OnZoneChanged?.Invoke(CurrentZoneIndex, CurrentZone);
        }

        private void Update()
        {
            int resolved = ResolveZoneIndex(player.position.y);
            if (resolved == CurrentZoneIndex)
            {
                return;
            }

            CurrentZoneIndex = resolved;
            if (resolved > HighestZoneReached)
            {
                HighestZoneReached = resolved;
            }

            OnZoneChanged?.Invoke(CurrentZoneIndex, CurrentZone);
        }

        private int ResolveZoneIndex(float height)
        {
            int index = 0;
            for (int i = 0; i < zones.Length; i++)
            {
                if (height >= zones[i].StartY)
                {
                    index = i;
                }
            }
            return index;
        }

        /// <summary>기본 난이도 곡선. 올라갈수록 적이 많고 빠르며 아래에서 쫓아오는 위협도 가속한다.</summary>
        public static AscentZone[] CreateDefaultZones()
        {
            return new[]
            {
                new AscentZone { Name = "죽림",   StartY = 0f,   MaxEnemies = 3,  EnemySpawnInterval = 4.5f, EnemySpeedMultiplier = 1.0f,  HazardRiseSpeed = 0.22f },
                new AscentZone { Name = "중단",   StartY = 24f,  MaxEnemies = 5,  EnemySpawnInterval = 3.2f, EnemySpeedMultiplier = 1.2f,  HazardRiseSpeed = 0.40f },
                new AscentZone { Name = "고지",   StartY = 52f,  MaxEnemies = 7,  EnemySpawnInterval = 2.3f, EnemySpeedMultiplier = 1.45f, HazardRiseSpeed = 0.62f },
                new AscentZone { Name = "능선",   StartY = 82f,  MaxEnemies = 9,  EnemySpawnInterval = 1.7f, EnemySpeedMultiplier = 1.7f,  HazardRiseSpeed = 0.85f },
                new AscentZone { Name = "설산",   StartY = 112f, MaxEnemies = 11, EnemySpawnInterval = 1.3f, EnemySpeedMultiplier = 1.95f, HazardRiseSpeed = 1.05f },
                new AscentZone { Name = "정상",   StartY = 140f, MaxEnemies = 13, EnemySpawnInterval = 1.0f, EnemySpeedMultiplier = 2.2f,  HazardRiseSpeed = 1.3f },
            };
        }
    }
}
