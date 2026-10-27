using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Tower
{
    /// <summary>젠가식 물리 타워를 생성하고, Quest에서 돌아가게 유지한다.
    ///
    /// 성능의 핵심은 "블록을 몇 개 놓느냐"가 아니라 "동시에 몇 개를 시뮬레이션하느냐"다.
    /// 타워 전체는 수백 개여도 되지만, 실제로 흔들려야 하는 것은 플레이어 손이 닿는 범위뿐이다.
    /// 나머지는 isKinematic으로 재워두면 PhysX 솔버 비용이 사실상 0이 된다.
    ///
    /// 그래서 이 매니저는 플레이어 높이를 따라 '시뮬레이션 창(window)'을 이동시키며, 창 안에 들어온
    /// 층만 깨우고 나간 층은 다시 재운다. 창의 크기는 maxSimulatedBlocks로 하드 제한된다.
    /// 층은 높이 순으로 생성되므로 창 계산은 인덱스 산술로 끝나고, 상태 변경은 창을 드나든
    /// 블록에만 적용해 매 프레임 전체를 훑지 않는다.</summary>
    public class TowerManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UnstableBlock blockPrefab;
        [SerializeField] private Transform player;

        [Header("Tower Shape")]
        [SerializeField] private int layerCount = 130;
        [SerializeField] private int blocksPerLayer = 3;
        [SerializeField] private Vector3 blockSize = new Vector3(0.9f, 0.32f, 0.3f);
        [Tooltip("층 사이 수직 간격. 블록 높이보다 조금 크게 두면 자리 잡을 여유가 생긴다.")]
        [SerializeField] private float layerSpacing = 0.34f;
        [Tooltip("층마다 블록을 조금씩 어긋나게 놓아 처음부터 불안해 보이게 한다 (m).")]
        [SerializeField] private float layerJitter = 0.05f;
        [SerializeField] private float baseY = 0f;
        [SerializeField] private int randomSeed = 91117;

        [Header("Physics Budget — Quest 성능의 상한선")]
        [Tooltip("동시에 non-kinematic으로 둘 블록 수의 하드 상한. Quest 2/3 기준 60~80이 안전권.")]
        [SerializeField] private int maxSimulatedBlocks = 64;
        [Tooltip("시뮬레이션 창을 다시 계산하는 주기 (초). 매 프레임 할 필요가 없다.")]
        [SerializeField] private float windowRefreshInterval = 0.2f;
        [Tooltip("플레이어보다 아래쪽으로도 이 비율만큼 창을 남긴다 — 무너지는 걸 보게 하려면 필요하다.")]
        [SerializeField] private float belowWindowRatio = 0.45f;

        [Header("Block Mass / Friction")]
        [SerializeField] private float blockMass = 6f;
        [SerializeField] private float blockDrag = 0.05f;
        [SerializeField] private float blockAngularDrag = 0.4f;

        [Header("Weakening — 올라갈수록 불안정해진다")]
        [Tooltip("약한 블록으로 뽑힐 기본 확률 (0층).")]
        [SerializeField] private float weakChanceAtBase = 0.08f;
        [Tooltip("최상층에서의 약한 블록 확률.")]
        [SerializeField] private float weakChanceAtTop = 0.58f;
        [Tooltip("약한 블록의 강도 배율. 0.35면 정상 블록의 35% 하중만 버틴다.")]
        [SerializeField] private float weakStrengthMultiplier = 0.35f;
        [SerializeField] private float normalStrengthMultiplier = 1f;

        [Header("Unstable Band — 중간에 유독 위험한 구간 하나")]
        [Tooltip("전체 흐름이 단조롭게만 어려워지지 않도록, 특정 층 구간만 국지적으로 약한 블록 비율을 확 올린다.")]
        [SerializeField] private float unstableBandStart = 0.35f; // layerCount에 대한 비율(0~1)
        [SerializeField] private float unstableBandEnd = 0.48f;
        [SerializeField] private float unstableBandWeakChance = 0.7f;

        [Header("Debris")]
        [Tooltip("빠져나온 블록을 이 시간 뒤 회수한다 — 잔해가 쌓이면 물리 비용이 계속 늘어난다.")]
        [SerializeField] private float debrisLifetime = 7f;
        [Tooltip("플레이어보다 이만큼 아래로 떨어진 잔해는 즉시 회수한다.")]
        [SerializeField] private float debrisCullDepth = 25f;

        /// <summary>빠져나온 블록 — 사운드·이펙트가 구독한다.</summary>
        public event Action<UnstableBlock> OnBlockDetached;

        public float TopY => baseY + layerCount * layerSpacing;
        public int SimulatedCount { get; private set; }

        private readonly List<UnstableBlock> blocks = new List<UnstableBlock>(512);
        private readonly Queue<UnstableBlock> pool = new Queue<UnstableBlock>();
        private readonly Dictionary<UnstableBlock, float> debrisExpiry = new Dictionary<UnstableBlock, float>();
        private readonly List<UnstableBlock> expiredDebris = new List<UnstableBlock>(16);

        private Transform blockRoot;
        private int windowStartLayer = -1;
        private int windowEndLayer = -1;
        private float nextWindowRefresh;

        // ────────────────────────────── Build ──────────────────────────────

        private void Awake()
        {
            blockRoot = new GameObject("TowerBlocks").transform;
            blockRoot.SetParent(transform, false);
        }

        private void Start()
        {
            BuildTower();
            RefreshSimulationWindow(force: true);
        }

        private void BuildTower()
        {
            var random = new System.Random(randomSeed);

            for (int layer = 0; layer < layerCount; layer++)
            {
                // 젠가처럼 층마다 90도 돌려 쌓는다 — 이래야 위층이 아래층 두 개를 걸치며 물린다.
                bool rotated = layer % 2 == 1;
                float y = baseY + layer * layerSpacing;
                float progress = (float)layer / Mathf.Max(1, layerCount - 1);
                float weakChance = Mathf.Lerp(weakChanceAtBase, weakChanceAtTop, progress);
                if (progress >= unstableBandStart && progress <= unstableBandEnd)
                {
                    weakChance = Mathf.Max(weakChance, unstableBandWeakChance);
                }

                for (int i = 0; i < blocksPerLayer; i++)
                {
                    float offset = (i - (blocksPerLayer - 1) * 0.5f) * blockSize.z;
                    float jitterX = ((float)random.NextDouble() - 0.5f) * layerJitter;
                    float jitterZ = ((float)random.NextDouble() - 0.5f) * layerJitter;

                    Vector3 localPosition = rotated
                        ? new Vector3(offset + jitterX, y, jitterZ)
                        : new Vector3(jitterX, y, offset + jitterZ);
                    Quaternion rotation = Quaternion.Euler(0f, rotated ? 90f : 0f, 0f);

                    bool weak = random.NextDouble() < weakChance;
                    SpawnBlock(localPosition, rotation, weak ? weakStrengthMultiplier : normalStrengthMultiplier);
                }
            }
        }

        private void SpawnBlock(Vector3 localPosition, Quaternion rotation, float strengthMultiplier)
        {
            UnstableBlock block = pool.Count > 0 ? pool.Dequeue() : CreateBlock();
            block.transform.SetParent(blockRoot, false);
            block.gameObject.SetActive(true);

            block.ResetForSpawn(blockRoot.TransformPoint(localPosition), rotation, strengthMultiplier);
            blocks.Add(block);
        }

        private UnstableBlock CreateBlock()
        {
            UnstableBlock block = Instantiate(blockPrefab, blockRoot);

            var body = block.GetComponent<Rigidbody>();
            body.mass = blockMass;
            body.linearDamping = blockDrag;
            body.angularDamping = blockAngularDrag;
            // 블록끼리는 이산 검출로 충분하다 — 연속 검출을 전부 켜면 Quest에서 그것만으로 예산을 먹는다.
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            block.transform.localScale = blockSize;

            block.OnDetached -= HandleBlockDetached;
            block.OnDetached += HandleBlockDetached;
            return block;
        }

        // ────────────────────────────── Simulation window ──────────────────────────────

        private void Update()
        {
            if (Time.time >= nextWindowRefresh)
            {
                nextWindowRefresh = Time.time + windowRefreshInterval;
                RefreshSimulationWindow(force: false);
            }

            CullDebris();
        }

        /// <summary>플레이어 높이를 중심으로 창을 옮기고, 창을 드나든 층만 상태를 바꾼다.</summary>
        private void RefreshSimulationWindow(bool force)
        {
            int layersInBudget = Mathf.Max(1, maxSimulatedBlocks / Mathf.Max(1, blocksPerLayer));
            int below = Mathf.RoundToInt(layersInBudget * belowWindowRatio);
            int above = layersInBudget - below;

            int playerLayer = Mathf.Clamp(
                Mathf.FloorToInt((player.position.y - baseY) / Mathf.Max(0.01f, layerSpacing)),
                0, Mathf.Max(0, layerCount - 1));

            int start = Mathf.Clamp(playerLayer - below, 0, layerCount);
            int end = Mathf.Clamp(playerLayer + above, 0, layerCount);

            if (!force && start == windowStartLayer && end == windowEndLayer)
            {
                return;
            }

            // 이전 창에서 빠진 층은 재우고, 새로 들어온 층은 깨운다.
            if (windowStartLayer >= 0)
            {
                SetLayerRangeSimulated(windowStartLayer, windowEndLayer, false, exceptStart: start, exceptEnd: end);
            }
            SetLayerRangeSimulated(start, end, true, exceptStart: -1, exceptEnd: -1);

            windowStartLayer = start;
            windowEndLayer = end;
            SimulatedCount = (end - start) * blocksPerLayer;
        }

        private void SetLayerRangeSimulated(int startLayer, int endLayer, bool simulated, int exceptStart, int exceptEnd)
        {
            for (int layer = startLayer; layer < endLayer; layer++)
            {
                if (layer >= exceptStart && layer < exceptEnd)
                {
                    continue; // 새 창에도 포함된 층은 건드리지 않는다
                }

                int from = layer * blocksPerLayer;
                int to = Mathf.Min(from + blocksPerLayer, blocks.Count);
                for (int i = from; i < to; i++)
                {
                    // 손에 잡혀 있거나 이미 빠진 블록은 강제로 재우지 않는다.
                    if (!simulated && (blocks[i].IsHeld || blocks[i].IsDetached))
                    {
                        continue;
                    }
                    blocks[i].SetSimulated(simulated);
                }
            }
        }

        // ────────────────────────────── Debris ──────────────────────────────

        private void HandleBlockDetached(UnstableBlock block)
        {
            debrisExpiry[block] = Time.time + debrisLifetime;
            OnBlockDetached?.Invoke(block);
        }

        /// <summary>잔해를 시간·깊이 두 기준으로 회수한다. 방치하면 동적 Rigidbody가 계속 누적돼
        /// 시뮬레이션 예산이 무의미해진다.</summary>
        private void CullDebris()
        {
            if (debrisExpiry.Count == 0)
            {
                return;
            }

            expiredDebris.Clear();
            float cullY = player.position.y - debrisCullDepth;

            foreach (var pair in debrisExpiry)
            {
                if (Time.time >= pair.Value || pair.Key.transform.position.y < cullY)
                {
                    expiredDebris.Add(pair.Key);
                }
            }

            for (int i = 0; i < expiredDebris.Count; i++)
            {
                Recycle(expiredDebris[i]);
            }
        }

        /// <summary>잔해를 화면에서 내린다. blocks 리스트에서는 빼지 않는다 — 층 인덱스를
        /// (layer * blocksPerLayer)로 계산하기 때문에 중간을 제거하면 그 뒤 모든 층이 밀려
        /// 시뮬레이션 창이 엉뚱한 블록을 깨우게 된다. 빈 슬롯으로 남겨두고 재사용은 RebuildTower에서만
        /// 한다.</summary>
        private void Recycle(UnstableBlock block)
        {
            debrisExpiry.Remove(block);
            block.gameObject.SetActive(false);
        }

        // ────────────────────────────── Run control ──────────────────────────────

        /// <summary>런을 다시 시작할 때 타워를 처음 상태로 되돌린다.</summary>
        public void RebuildTower()
        {
            // 런 중에는 풀에 아무것도 넣지 않으므로, 여기서 전량을 정확히 한 번 회수한다.
            for (int i = 0; i < blocks.Count; i++)
            {
                blocks[i].gameObject.SetActive(false);
                pool.Enqueue(blocks[i]);
            }
            blocks.Clear();
            debrisExpiry.Clear();

            windowStartLayer = -1;
            windowEndLayer = -1;

            BuildTower();
            RefreshSimulationWindow(force: true);
        }
    }
}
