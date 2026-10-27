using UnityEngine;
using UnityEngine.UI;

namespace VRGame.HoseCleaner
{
    /// <summary>Dirt Monster가 너무 가까이 오면 헬멧 바이저에 진흙이 튄 것처럼 화면을 잠깐 가린다.
    /// 텍스처 에셋을 새로 만들 수 없어(RULE-02 지원 범위 밖) 스플래터 무늬 대신 단색 오버레이의
    /// 알파를 급격히 올렸다가 서서히 내리는 것으로 "확 튀었다가 흘러내리는" 느낌을 낸다 — Echo의
    /// EcholocationVisuals가 쓴 "빠른 반응 + 느린 감쇠" 엔벨로프와 같은 아이디어.</summary>
    public class VisorBlindness : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("근접 판정 기준점 — 보통 헤드 카메라 트랜스폼.")]
        [SerializeField] private Transform head;
        [SerializeField] private Image splatterOverlay;

        [Header("Trigger")]
        [SerializeField] private float triggerRange = 1.4f;
        [SerializeField] private float retriggerCooldown = 1.2f;

        [Header("Envelope")]
        [SerializeField] private float maxAlpha = 0.85f;
        [SerializeField] private float decayPerSecond = 0.5f;

        private float currentAlpha;
        private float nextTriggerTime;

        private void Update()
        {
            if (head != null && Time.time >= nextTriggerTime)
            {
                DirtMonster nearest = DirtMonsterRegistry.FindNearest(head.position, triggerRange);
                if (nearest != null)
                {
                    currentAlpha = maxAlpha;
                    nextTriggerTime = Time.time + retriggerCooldown;
                }
            }

            currentAlpha = Mathf.MoveTowards(currentAlpha, 0f, decayPerSecond * Time.deltaTime);
            ApplyAlpha();
        }

        private void ApplyAlpha()
        {
            if (splatterOverlay == null)
            {
                return;
            }

            Color color = splatterOverlay.color;
            color.a = currentAlpha;
            splatterOverlay.color = color;
        }
    }
}
