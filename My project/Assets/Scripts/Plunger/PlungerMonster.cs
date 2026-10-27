using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace VRGame.Plunger
{
    /// <summary>플런저 프로토타입 전용 최소 몬스터 — 플레이어를 추적하다가, 얼굴에 플런저가 꽂히면
    /// (MonsterFaceStick이 <see cref="Blind"/>를 호출) 일정 시간 멈춘다. 공격·데미지 로직은 없다 —
    /// 이번 스코프의 목적은 "흡착해서 실명시키는" 메커닉을 시험할 표적을 제공하는 것뿐이다.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class PlungerMonster : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float chaseSpeed = 3.5f;
        [Tooltip("Blind()에 duration을 안 주면(음수) 이 기본값을 쓴다.")]
        [SerializeField] private float panicDuration = 2f;

        public bool IsBlind { get; private set; }

        private NavMeshAgent agent;
        private Animator animator;
        private Coroutine panicHandle;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = chaseSpeed;
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            if (IsBlind || target == null)
            {
                return;
            }

            agent.SetDestination(target.position);
        }

        /// <summary>MonsterFaceStick이 얼굴 흡착 순간 호출한다. duration을 생략(음수)하면 panicDuration을 쓴다.</summary>
        public void Blind(float duration = -1f)
        {
            float actualDuration = duration >= 0f ? duration : panicDuration;

            if (panicHandle != null)
            {
                StopCoroutine(panicHandle);
            }
            panicHandle = StartCoroutine(PanicRoutine(actualDuration));
        }

        private IEnumerator PanicRoutine(float duration)
        {
            IsBlind = true;
            agent.isStopped = true;

            if (animator != null)
            {
                animator.SetTrigger("Panic");
            }

            yield return new WaitForSeconds(duration);

            agent.isStopped = false;
            IsBlind = false;
            panicHandle = null;
        }
    }
}
