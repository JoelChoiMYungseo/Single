using System.Collections;
using UnityEngine;

namespace VRGame.Danmaku
{
    /// <summary>3가지 탄막 패턴을 순환하는 좀비 보스 컨트롤러. 정교한 슈팅게임식 기하 패턴보다는
    /// "탕탕특공대"류 서바이버 게임처럼 사방에서 지저분하게 쏟아지는 물량감을 우선한다 — 완벽한
    /// 대칭 대신 약간의 지터를 섞고, 한 발의 정밀한 예측사격보다는 스프레이 뭉치로 쏜다.</summary>
    public class ZombieDanmakuAI : MonoBehaviour
    {
        public enum AttackPattern { Idle, RingBurst, PredictiveSpray, SpiralWave }

        [Header("References")]
        [SerializeField] private BulletPool bulletPool;
        [SerializeField] private Transform playerTarget;
        [SerializeField] private Transform firePoint;

        [Header("Cycle Cooldowns")]
        [SerializeField] private float ringBurstCooldown = 4f;
        [SerializeField] private float predictiveSprayCooldown = 2.5f;
        [SerializeField] private float spiralWaveCooldown = 5f;

        [Header("Pattern 1 — Ring Burst (AoE Panic, 점프 유도)")]
        [SerializeField] private int ringBulletCount = 24;
        [SerializeField] private int ringBurstWaves = 2;
        [SerializeField] private float ringBurstWaveDelay = 0.12f;
        [SerializeField] private float ringAngleJitter = 6f;
        [SerializeField] private float ringBulletSpeed = 6f;
        [SerializeField] private float ringBulletDamage = 8f;
        [SerializeField] private float ringBulletLifetime = 4f;

        [Header("Pattern 2 — Predictive Spray (조준 패닉)")]
        [SerializeField] private int predictiveSprayCount = 5;
        [SerializeField] private float predictiveSpreadAngle = 18f;
        [SerializeField] private float predictionTime = 0.6f;
        [SerializeField] private float predictiveBulletSpeed = 14f;
        [SerializeField] private float predictiveBulletDamage = 10f;
        [SerializeField] private float predictiveBulletLifetime = 3f;

        [Header("Pattern 3 — Spiral Wave (좌우 지속 회피)")]
        [SerializeField] private int spiralArmCount = 4;
        [SerializeField] private float spiralWaveDuration = 3f;
        [SerializeField] private float spiralFireInterval = 0.08f;
        [SerializeField] private float spiralRotationSpeed = 220f;
        [SerializeField] private float spiralBulletSpeed = 8f;
        [SerializeField] private float spiralBulletDamage = 6f;
        [SerializeField] private float spiralBulletLifetime = 4f;

        public AttackPattern CurrentPattern { get; private set; } = AttackPattern.Idle;

        private VRGame.Core.PlayerRig playerRig;
        private Coroutine attackRoutine;
        private WaitForSeconds ringBurstCooldownWait;
        private WaitForSeconds ringBurstWaveDelayWait;
        private WaitForSeconds predictiveSprayCooldownWait;
        private WaitForSeconds spiralWaveCooldownWait;
        private WaitForSeconds spiralFireIntervalWait;

        private void Awake()
        {
            if (firePoint == null)
            {
                firePoint = transform;
            }
            if (playerTarget != null)
            {
                // 로코모션 종류를 몰라도 속도를 얻는다 — PlayerRig가 물리든 Transform 이동이든
                // 하나의 값으로 정규화해 준다.
                playerRig = playerTarget.GetComponent<VRGame.Core.PlayerRig>();
            }

            ringBurstCooldownWait = new WaitForSeconds(ringBurstCooldown);
            ringBurstWaveDelayWait = new WaitForSeconds(ringBurstWaveDelay);
            predictiveSprayCooldownWait = new WaitForSeconds(predictiveSprayCooldown);
            spiralWaveCooldownWait = new WaitForSeconds(spiralWaveCooldown);
            spiralFireIntervalWait = new WaitForSeconds(spiralFireInterval);
        }

        private void OnEnable()
        {
            attackRoutine = StartCoroutine(AttackCycle());
        }

        private void OnDisable()
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }
            CurrentPattern = AttackPattern.Idle;
        }

        private IEnumerator AttackCycle()
        {
            while (true)
            {
                CurrentPattern = AttackPattern.RingBurst;
                yield return StartCoroutine(RingBurst());
                CurrentPattern = AttackPattern.Idle;
                yield return ringBurstCooldownWait;

                CurrentPattern = AttackPattern.PredictiveSpray;
                yield return StartCoroutine(PredictiveSpray());
                CurrentPattern = AttackPattern.Idle;
                yield return predictiveSprayCooldownWait;

                CurrentPattern = AttackPattern.SpiralWave;
                yield return StartCoroutine(SpiralWave());
                CurrentPattern = AttackPattern.Idle;
                yield return spiralWaveCooldownWait;
            }
        }

        /// <summary>360도 링을 2~3파 겹쳐서 순간적으로 쏟아붓는다. 매 파마다 살짝 회전 오프셋과
        /// 각도 지터를 줘서 기하학적으로 딱 맞는 원이 아니라 뭉텅이로 몰아치는 느낌을 낸다.
        /// 링 전체를 뛰어넘어야 피하므로 팔을 아래로 세게 저어 점프하게 만든다.</summary>
        private IEnumerator RingBurst()
        {
            float baseAngleStep = 360f / ringBulletCount;
            Vector3 origin = firePoint.position;

            for (int wave = 0; wave < ringBurstWaves; wave++)
            {
                float waveRotationOffset = Random.Range(0f, baseAngleStep);

                for (int i = 0; i < ringBulletCount; i++)
                {
                    float angle = i * baseAngleStep + waveRotationOffset + Random.Range(-ringAngleJitter, ringAngleJitter);
                    Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    bulletPool.Spawn(origin, Quaternion.LookRotation(direction), direction, ringBulletSpeed, ringBulletDamage, ringBulletLifetime);
                }

                if (wave < ringBurstWaves - 1)
                {
                    yield return ringBurstWaveDelayWait;
                }
            }
        }

        /// <summary>플레이어의 고릴라 스윙 모멘텀(Rigidbody 속도)으로 예상 위치를 계산한 뒤,
        /// 한 발이 아니라 부채꼴로 퍼진 스프레이를 쏜다 — 정밀 저격보다 물량 압박에 가깝게.</summary>
        private IEnumerator PredictiveSpray()
        {
            Vector3 origin = firePoint.position;
            Vector3 predicted = PredictPlayerPosition();
            Vector3 baseDirection = (predicted - origin).normalized;

            for (int i = 0; i < predictiveSprayCount; i++)
            {
                float t = predictiveSprayCount <= 1 ? 0f : (float)i / (predictiveSprayCount - 1) - 0.5f;
                float angle = t * predictiveSpreadAngle;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * baseDirection;
                bulletPool.Spawn(origin, Quaternion.LookRotation(direction), direction, predictiveBulletSpeed, predictiveBulletDamage, predictiveBulletLifetime);
            }

            yield return null;
        }

        private Vector3 PredictPlayerPosition()
        {
            if (playerRig != null)
            {
                return playerTarget.position + playerRig.Velocity * predictionTime;
            }
            return playerTarget.position;
        }

        /// <summary>여러 팔을 가진 나선을 계속 회전시키며 쏴서 좌우로 끊임없이 움직이게 만든다.</summary>
        private IEnumerator SpiralWave()
        {
            float currentAngle = 0f;
            float elapsed = 0f;
            Vector3 origin = firePoint.position;
            float armStep = 360f / spiralArmCount;

            while (elapsed < spiralWaveDuration)
            {
                for (int arm = 0; arm < spiralArmCount; arm++)
                {
                    float angle = currentAngle + arm * armStep;
                    Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    bulletPool.Spawn(origin, Quaternion.LookRotation(direction), direction, spiralBulletSpeed, spiralBulletDamage, spiralBulletLifetime);
                }

                currentAngle += spiralRotationSpeed * spiralFireInterval;
                elapsed += spiralFireInterval;
                yield return spiralFireIntervalWait;
            }
        }
    }
}
