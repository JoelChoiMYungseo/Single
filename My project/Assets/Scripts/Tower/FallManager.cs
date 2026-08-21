using System;
using UnityEngine;

namespace VRGame.Tower
{
    /// <summary>추락 감지와 "패닉 슬로우모션".
    ///
    /// 떨어지기 시작하면 2초간 시간을 늦춘다. 목적은 자비가 아니라 반대다 — 다음 블록을 붙잡을 수
    /// 있다는 희망을 준 상태에서 손을 허우적거리게 만드는 것이다. 그 몇 초가 숏폼에서 가장 잘 팔리는
    /// 구간이고, 살아남으면 '아슬아슬한 세이브', 실패하면 '절망 리액션'이 나온다.
    ///
    /// timeScale을 건드릴 때 fixedDeltaTime을 같이 줄이지 않으면 물리 스텝이 게임 시간 기준으로
    /// 듬성듬성해져 블록 충돌이 뚫리고 손 스프링이 튄다. 반드시 함께 스케일하고 원복해야 한다.
    /// 지속 시간은 unscaledDeltaTime으로 재야 한다 — 늦춰진 시간으로 재면 2초가 4초가 된다.</summary>
    public class FallManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody playerBody;
        [Tooltip("붙잡고 있으면 슬로우모션을 즉시 해제하기 위해 참조한다.")]
        [SerializeField] private VRHandController[] hands;

        [Header("Fall Detection")]
        [Tooltip("아래로 이 속력을 넘으면 '추락 중'으로 본다 (m/s).")]
        [SerializeField] private float fallSpeedThreshold = 6f;
        [Tooltip("추락 판정이 이 시간 이상 유지돼야 슬로우모션이 발동한다 — 점프 착지로 오발동하는 것 방지.")]
        [SerializeField] private float fallConfirmDelay = 0.12f;

        [Header("Bullet Time")]
        [Range(0.05f, 1f)]
        [SerializeField] private float slowTimeScale = 0.5f;
        [Tooltip("실제 시간 기준 지속 시간 (초).")]
        [SerializeField] private float bulletTimeDuration = 2f;
        [Tooltip("연속 발동 방지 쿨다운 (실제 시간 초).")]
        [SerializeField] private float cooldown = 3f;
        [Tooltip("무언가를 붙잡으면 즉시 정상 속도로 돌아온다.")]
        [SerializeField] private bool cancelOnGrab = true;

        [Header("Crash")]
        [Tooltip("이 속력 이상으로 떨어지다 멈추면 추락 사고로 처리한다 (m/s).")]
        [SerializeField] private float crashSpeedThreshold = 9f;
        [Tooltip("멈췄다고 판정할 속력 (m/s).")]
        [SerializeField] private float landedSpeedThreshold = 1.5f;

        /// <summary>추락 시작 — 트레일·비명 사운드(유료 스킨)가 여기 붙는다.</summary>
        public event Action OnFallStarted;
        public event Action OnBulletTimeStarted;
        public event Action OnBulletTimeEnded;
        /// <summary>추락 사고 — 낙하 직전 최고 속력을 넘겨준다.</summary>
        public event Action<float> OnCrashed;
        /// <summary>슬로우모션 중에 붙잡아 살아남음 — '아슬아슬한 세이브'.</summary>
        public event Action OnSaved;

        public bool IsFalling { get; private set; }
        public bool IsBulletTime { get; private set; }
        /// <summary>이번 추락에서 기록한 최고 낙하 속력 — 연출 세기에 쓴다.</summary>
        public float PeakFallSpeed { get; private set; }

        private float defaultFixedDeltaTime;
        private float fallElapsed;
        private float bulletTimeRemaining;
        private float nextAllowedTime;

        private void Awake()
        {
            defaultFixedDeltaTime = Time.fixedDeltaTime;
        }

        private void OnDisable()
        {
            // 씬 전환·비활성화 중에 시간이 늦춰진 채로 남으면 다음 씬 전체가 느려진다.
            RestoreTimeScale();
        }

        private void Update()
        {
            // 시간 관련 판단은 모두 실제 시간 기준이다.
            float unscaled = Time.unscaledDeltaTime;

            UpdateFallState(unscaled);
            UpdateBulletTime(unscaled);
        }

        private void UpdateFallState(float unscaledDelta)
        {
            float verticalSpeed = playerBody.linearVelocity.y;
            bool fallingNow = verticalSpeed < -fallSpeedThreshold;

            if (fallingNow)
            {
                PeakFallSpeed = Mathf.Max(PeakFallSpeed, -verticalSpeed);
                fallElapsed += unscaledDelta;

                if (!IsFalling && fallElapsed >= fallConfirmDelay)
                {
                    IsFalling = true;
                    OnFallStarted?.Invoke();
                    TryStartBulletTime();
                }
                return;
            }

            if (!IsFalling)
            {
                fallElapsed = 0f;
                return;
            }

            // 추락이 끝났다 — 붙잡아서 멈춘 것인지, 바닥에 처박은 것인지 구분한다.
            bool stopped = playerBody.linearVelocity.magnitude < landedSpeedThreshold;
            bool grabbed = IsAnyHandHolding();

            if (grabbed)
            {
                OnSaved?.Invoke();
            }
            else if (stopped && PeakFallSpeed >= crashSpeedThreshold)
            {
                OnCrashed?.Invoke(PeakFallSpeed);
            }

            IsFalling = false;
            fallElapsed = 0f;
            PeakFallSpeed = 0f;

            if (IsBulletTime)
            {
                EndBulletTime();
            }
        }

        private void TryStartBulletTime()
        {
            if (IsBulletTime || Time.unscaledTime < nextAllowedTime)
            {
                return;
            }

            IsBulletTime = true;
            bulletTimeRemaining = bulletTimeDuration;

            Time.timeScale = slowTimeScale;
            // 물리 스텝도 같은 비율로 줄여 스텝 밀도를 유지한다.
            Time.fixedDeltaTime = defaultFixedDeltaTime * slowTimeScale;

            OnBulletTimeStarted?.Invoke();
        }

        private void UpdateBulletTime(float unscaledDelta)
        {
            if (!IsBulletTime)
            {
                return;
            }

            if (cancelOnGrab && IsAnyHandHolding())
            {
                EndBulletTime();
                return;
            }

            bulletTimeRemaining -= unscaledDelta;
            if (bulletTimeRemaining <= 0f)
            {
                EndBulletTime();
            }
        }

        private void EndBulletTime()
        {
            IsBulletTime = false;
            bulletTimeRemaining = 0f;
            nextAllowedTime = Time.unscaledTime + cooldown;

            RestoreTimeScale();
            OnBulletTimeEnded?.Invoke();
        }

        private void RestoreTimeScale()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = defaultFixedDeltaTime;
        }

        private bool IsAnyHandHolding()
        {
            for (int i = 0; i < hands.Length; i++)
            {
                if (hands[i] != null && hands[i].HasGrabbedObject)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
