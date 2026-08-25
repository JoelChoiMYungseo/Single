using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VRGame.Echo
{
    /// <summary>마이크 크기를 화면 상의 "실명" 정도로 바꾼다. 커스텀 리빌 셰이더를 새로 짜는 대신
    /// URP의 내장 Volume 오버라이드(Vignette + ColorAdjustments)를 조합했다 — 셰이더 컴파일 실패
    /// 위험 없이, 이미 프로젝트가 쓰는 URP 파이프라인만으로 "화면이 거의 새까맣다가 소리 지르면
    /// 잠깐 보인다"를 구현할 수 있다.
    ///
    /// 조용함 = Vignette 최대(가장자리 완전히 검게) + 노출 대폭 감소(중앙까지 어둡게).
    /// 소리 지름 = 둘 다 원래대로 돌아와 시야가 트인다.
    ///
    /// 반응 속도를 마이크 자체의 어택/릴리즈와 별도로 둔 이유: 소리가 멈춘 뒤 시야가 즉시 캄캄해지면
    /// "방금 본 게 순간적으로 지워진다"는 느낌이라 뚝뚝 끊긴다. 잔향처럼 천천히 사라지게 하면
    /// 실제 반향정위(에코로케이션)에 가까운 느낌이 난다.</summary>
    [RequireComponent(typeof(Volume))]
    public class EcholocationVisuals : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private MicInputListener micListener;

        [Header("Reveal Envelope")]
        [Tooltip("소리가 커질 때 시야가 트이는 속도 (1/초) — 미리 듣기가 없어야 하니 즉각적이어야 한다.")]
        [SerializeField] private float revealAttackSpeed = 25f;
        [Tooltip("소리가 멈춘 뒤 얼마나 천천히 다시 어두워지는가 (1/초). 낮을수록 잔향처럼 오래 남는다.")]
        [SerializeField] private float revealDecaySpeed = 1.2f;

        [Header("Vignette")]
        [SerializeField] private Color vignetteColor = Color.black;
        [SerializeField] private float darkVignetteIntensity = 1f;
        [SerializeField] private float litVignetteIntensity = 0.15f;
        [SerializeField] private float vignetteSmoothness = 0.9f;

        [Header("Exposure")]
        [Tooltip("EV 단위. 조용할 때 화면 전체를 이만큼 어둡게 눌러 중앙까지 안 보이게 한다.")]
        [SerializeField] private float darkExposure = -8f;
        [SerializeField] private float litExposure = 0f;

        /// <summary>0=완전히 안 보임, 1=완전히 트임. 다른 시스템이 참조할 수 있게 공개한다.</summary>
        public float RevealAmount { get; private set; }

        private Volume volume;
        private Vignette vignette;
        private ColorAdjustments colorAdjustments;

        private void Awake()
        {
            volume = GetComponent<Volume>();

            if (volume.profile == null || !volume.profile.TryGet(out vignette) || !volume.profile.TryGet(out colorAdjustments))
            {
                Debug.LogError("[Echo] Volume Profile에 Vignette/ColorAdjustments 오버라이드가 필요합니다. " +
                                "EcholocationVisuals를 비활성화합니다.");
                enabled = false;
                return;
            }

            // 프로필을 코드로 만들었을 때 개별 파라미터의 overrideState가 꺼져 있으면 값을 대입해도
            // 무시된다 — 여기서 한 번 강제로 켜서 항상 우리가 계산한 값이 적용되게 한다.
            vignette.intensity.overrideState = true;
            vignette.smoothness.overrideState = true;
            vignette.color.overrideState = true;
            vignette.color.value = vignetteColor;
            colorAdjustments.postExposure.overrideState = true;
        }

        private void Update()
        {
            float target = micListener != null ? micListener.Loudness : 0f;
            float speed = target > RevealAmount ? revealAttackSpeed : revealDecaySpeed;
            RevealAmount = Mathf.MoveTowards(RevealAmount, target, speed * Time.deltaTime);

            vignette.intensity.value = Mathf.Lerp(darkVignetteIntensity, litVignetteIntensity, RevealAmount);
            vignette.smoothness.value = vignetteSmoothness;
            colorAdjustments.postExposure.value = Mathf.Lerp(darkExposure, litExposure, RevealAmount);
        }
    }
}
