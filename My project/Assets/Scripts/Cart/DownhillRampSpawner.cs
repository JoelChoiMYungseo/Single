using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Cart
{
    /// <summary>"무한 내리막"을 진짜 무한 지형이 아니라 장애물 재활용으로 흉내낸다 — 바닥 자체는
    /// SceneBuilder가 만드는 하나의 긴 경사 슬래브고, 이 스크립트는 그 위에 놓일 장애물을 카트 앞에
    /// 항상 몇 개 유지하다가 지나간 것을 뒤에서 앞으로 재배치한다(Danmaku BulletPool류의 "새로
    /// 만들지 않고 재사용" 관례를 적용한 것). 경사면 위에 정확히 놓이도록 <see cref="slopeYPerZ"/>로
    /// Z에 따른 Y를 계산한다 — 바닥 각도와 반드시 일치해야 장애물이 뜨거나 파묻히지 않는다.
    /// 이 스크립트는 <see cref="DownhillCart"/> 타입을 전혀 모른다 — 추적 대상은 순수 Transform
    /// 하나뿐이라 다른 무엇을 태워도 동작한다(결합 최소화).</summary>
    public class DownhillRampSpawner : MonoBehaviour
    {
        [SerializeField] private Transform tracked;
        [SerializeField] private GameObject[] segmentPrefabs;
        [SerializeField] private float segmentLength = 20f;
        [SerializeField] private int segmentsAhead = 4;
        [SerializeField] private float recycleBehindDistance = 15f;
        [Tooltip("경사면의 Z 1m당 Y 변화량 — 바닥 기울기와 일치시켜야 장애물이 슬로프 위에 정확히 놓인다.")]
        [SerializeField] private float slopeYPerZ = -0.18f;

        private readonly List<GameObject> segments = new List<GameObject>();
        private float nextSpawnZ;
        private float baseY;
        private float baseZ;
        private System.Random random = new System.Random();

        private void Start()
        {
            if (tracked == null || segmentPrefabs == null || segmentPrefabs.Length == 0)
            {
                return;
            }

            nextSpawnZ = tracked.position.z;
            baseY = tracked.position.y;
            baseZ = tracked.position.z;
            for (int i = 0; i < segmentsAhead; i++)
            {
                SpawnNextSegment();
            }
        }

        /// <summary>SceneBuilder가 결정적 배치를 원하면 호출 — 생략하면 기본 시드를 쓴다.</summary>
        public void Seed(int seed)
        {
            random = new System.Random(seed);
        }

        private void Update()
        {
            if (tracked == null || segments.Count == 0)
            {
                return;
            }

            // 지형 배치는 위치 계산일 뿐 물리 연산이 아니므로 Update에서 해도 RULE-03(FixedUpdate 전용
            // 물리 API) 대상이 아니다.
            GameObject rearmost = segments[0];
            float rearmostFrontZ = rearmost.transform.position.z + segmentLength;
            if (tracked.position.z - rearmostFrontZ > recycleBehindDistance)
            {
                RecycleRearmost();
            }
        }

        /// <summary>가장 뒤처진 세그먼트를 재사용한다 — 같은 프리팹을 다시 뽑았으면 위치만
        /// 앞으로 옮기고(Destroy/Instantiate 없음), 다른 프리팹을 뽑았을 때만 예외적으로
        /// 통째로 교체한다.</summary>
        private void RecycleRearmost()
        {
            GameObject segment = segments[0];
            segments.RemoveAt(0);

            GameObject prefab = PickPrefab();
            Vector3 newPosition = new Vector3(segment.transform.position.x, HeightAt(nextSpawnZ), nextSpawnZ);
            bool samePrefab = prefab != null && segment.name == prefab.name + "(Clone)";

            if (samePrefab)
            {
                segment.transform.position = newPosition;
            }
            else
            {
                Object.Destroy(segment);
                segment = prefab != null ? Object.Instantiate(prefab, newPosition, Quaternion.identity, transform) : null;
            }

            if (segment != null)
            {
                segments.Add(segment);
            }
            nextSpawnZ += segmentLength;
        }

        private void SpawnNextSegment()
        {
            GameObject prefab = PickPrefab();
            if (prefab == null)
            {
                return;
            }

            Vector3 position = new Vector3(tracked.position.x, HeightAt(nextSpawnZ), nextSpawnZ);
            GameObject segment = Object.Instantiate(prefab, position, Quaternion.identity, transform);
            segments.Add(segment);
            nextSpawnZ += segmentLength;
        }

        private GameObject PickPrefab()
        {
            if (segmentPrefabs == null || segmentPrefabs.Length == 0)
            {
                return null;
            }
            return segmentPrefabs[random.Next(segmentPrefabs.Length)];
        }

        private float HeightAt(float z)
        {
            return baseY + (z - baseZ) * slopeYPerZ;
        }
    }
}
