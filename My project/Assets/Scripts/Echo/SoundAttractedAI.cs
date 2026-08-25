using UnityEngine;
using UnityEngine.AI;

namespace VRGame.Echo
{
    /// <summary>소리로만 플레이어를 찾는 괴물. 눈이 안 보이는 설정과 짝을 이루는 AI라 "시야"가 아니라
    /// "최근에 들은 소리의 크기와 위치"로만 반응한다.
    ///
    /// 소리가 나면 즉시 그 위치로 달려오지만, 소리가 멈춰도 곧바로 배회로 안 돌아간다 —
    /// memoryDuration 동안은 "마지막으로 들은 지점"을 계속 쫓는다. 그래야 "방금까지 여기 있었는데
    /// 조용해지니 어디 갔는지 모르겠다"는 긴장이 생긴다.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class SoundAttractedAI : MonoBehaviour
    {
        private enum State { Wandering, Alerted }

        [Header("Input")]
        [SerializeField] private MicInputListener micListener;
        [SerializeField] private Transform player;

        [Header("Hearing")]
        [Tooltip("이 값을 넘는 Loudness만 '소리를 들었다'로 취급한다.")]
        [SerializeField] private float hearingThreshold = 0.3f;
        [Tooltip("소리가 멈춘 뒤에도 마지막 위치를 계속 쫓는 시간 (초).")]
        [SerializeField] private float memoryDuration = 4f;

        [Header("Speed")]
        [SerializeField] private float wanderSpeed = 1.3f;
        [SerializeField] private float alertedSpeed = 4.5f;
        [Tooltip("속도가 바뀌는 체감 — 낮을수록 서서히 가속·감속한다.")]
        [SerializeField] private float speedChangeRate = 3f;

        [Header("Wander")]
        [SerializeField] private float wanderRadius = 12f;
        [SerializeField] private float wanderPauseSeconds = 2f;

        [Header("Pacing")]
        [Tooltip("SetDestination 호출 간격 — 매 프레임 부르면 NavMesh 쿼리 비용이 쌓인다.")]
        [SerializeField] private float destinationUpdateInterval = 0.2f;

        public bool IsAlerted => state == State.Alerted;

        private NavMeshAgent agent;
        private State state = State.Wandering;
        private float lastHeardTime = float.NegativeInfinity;
        private float nextDestinationUpdateTime;
        private float nextWanderPickTime;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = wanderSpeed;
        }

        private void Update()
        {
            UpdateHearingState();
            UpdateDestination();
            UpdateSpeed();
        }

        private void UpdateHearingState()
        {
            if (micListener != null && micListener.Loudness >= hearingThreshold)
            {
                lastHeardTime = Time.time;
                state = State.Alerted;
            }
            else if (state == State.Alerted && Time.time - lastHeardTime >= memoryDuration)
            {
                state = State.Wandering;
            }
        }

        private void UpdateDestination()
        {
            if (Time.time < nextDestinationUpdateTime)
            {
                return;
            }
            nextDestinationUpdateTime = Time.time + destinationUpdateInterval;

            if (state == State.Alerted)
            {
                agent.SetDestination(player.position);
                return;
            }

            Wander();
        }

        /// <summary>배회 중엔 목적지에 도착했을 때만 새 지점을 뽑는다 — 매 갱신 주기마다 새로 뽑으면
        /// 도착하기도 전에 목적지가 계속 바뀌어 제자리에서 서성이게 된다.</summary>
        private void Wander()
        {
            if (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.1f)
            {
                return;
            }

            if (Time.time < nextWanderPickTime)
            {
                return;
            }
            nextWanderPickTime = Time.time + wanderPauseSeconds;

            Vector3 randomPoint = transform.position + UnityEngine.Random.insideUnitSphere * wanderRadius;
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }

        private void UpdateSpeed()
        {
            float targetSpeed = state == State.Alerted ? alertedSpeed : wanderSpeed;
            agent.speed = Mathf.MoveTowards(agent.speed, targetSpeed, speedChangeRate * Time.deltaTime);
        }
    }
}
