using UnityEngine;

namespace VRGame.Echo
{
    /// <summary>마이크 입력을 읽어 0~1 사이의 "얼마나 시끄러운가" 값으로 정규화한다.
    ///
    /// 매 프레임 클립 전체를 다시 읽거나, 마지막으로 읽은 뒤 새로 쌓인 샘플 수만큼 가변 크기로
    /// 읽으면 프레임이 튀어 밀린 구간이 쌓였을 때 그 프레임에서 스파이크가 난다. 그래서 이 스크립트는
    /// "현재 위치 기준 최근 N밀리초"라는 **고정 크기 창**만 매 프레임 읽는다 — 버퍼를 한 번만
    /// 할당해 두면 그 이후로는 프레임레이트나 말한 길이와 무관하게 항상 같은(작은) 비용이 든다.</summary>
    public class MicInputListener : MonoBehaviour
    {
        [Header("Device")]
        [Tooltip("비워두면 시스템 첫 번째 마이크 장치를 쓴다.")]
        [SerializeField] private string deviceNameOverride = "";
        [Tooltip("음성 RMS 판정엔 이 정도면 충분하다 — 44.1kHz는 CPU만 낭비한다.")]
        [SerializeField] private int requestedFrequency = 16000;
        [SerializeField] private int clipLengthSeconds = 1;

        [Header("Loudness Window")]
        [Tooltip("매 프레임 읽는 창 길이(초). 짧을수록 반응은 빠르지만 값이 들쭉날쭉해진다.")]
        [SerializeField] private float sampleWindowSeconds = 0.05f;
        [Tooltip("이 미만 RMS는 마이크 잡음/숨소리로 보고 0으로 처리한다.")]
        [SerializeField] private float silenceThreshold = 0.01f;
        [Tooltip("RMS를 0~1로 매핑하는 배율 — 마이크 감도에 맞춰 캘리브레이션한다.")]
        [SerializeField] private float gain = 6f;

        [Header("Envelope")]
        [Tooltip("소리가 커질 때 따라가는 속도 (1/초). 크게 둬서 비명에 즉각 반응하게 한다.")]
        [SerializeField] private float attackSpeed = 20f;
        [Tooltip("소리가 작아질 때 따라가는 속도 (1/초). 작게 둬서 말이 끊길 때마다 뚝뚝 안 끊기게 한다.")]
        [SerializeField] private float releaseSpeed = 3f;

        /// <summary>부드럽게 감쇠된 최종 값(0~1). 시각·AI가 이걸 읽는다.</summary>
        public float Loudness { get; private set; }
        public bool IsMicAvailable { get; private set; }

        private AudioClip micClip;
        private string deviceName;
        private int windowFrames;
        private float[] windowBuffer;

        private void OnEnable()
        {
            StartMic();
        }

        private void OnDisable()
        {
            StopMic();
        }

        private void StartMic()
        {
            if (Microphone.devices.Length == 0)
            {
                IsMicAvailable = false;
                Debug.LogWarning("[Echo] 마이크 장치를 찾지 못했습니다. Loudness는 항상 0으로 유지됩니다.");
                return;
            }

            deviceName = string.IsNullOrEmpty(deviceNameOverride) ? Microphone.devices[0] : deviceNameOverride;
            micClip = Microphone.Start(deviceName, true, clipLengthSeconds, requestedFrequency);

            if (micClip == null)
            {
                IsMicAvailable = false;
                Debug.LogWarning($"[Echo] 마이크 \"{deviceName}\" 시작에 실패했습니다.");
                return;
            }

            // 실제 채널 수·주파수는 요청값과 다를 수 있다 (기기가 다운믹스/리샘플하는 경우) —
            // 반드시 클립이 알려주는 값을 기준으로 버퍼 크기를 정한다.
            windowFrames = Mathf.Max(1, Mathf.RoundToInt(micClip.frequency * sampleWindowSeconds));
            windowBuffer = new float[windowFrames * micClip.channels]; // 한 번만 할당 — 이후 매 프레임 재사용.
            IsMicAvailable = true;
        }

        private void StopMic()
        {
            if (!string.IsNullOrEmpty(deviceName) && Microphone.IsRecording(deviceName))
            {
                Microphone.End(deviceName);
            }
        }

        private void Update()
        {
            if (!IsMicAvailable)
            {
                return;
            }

            if (!Microphone.IsRecording(deviceName))
            {
                // OS가 포커스 전환 등으로 녹음을 끊는 경우가 있다 — 재시작을 시도한다.
                StartMic();
                return;
            }

            float rms = ReadWindowRms();
            float target = rms < silenceThreshold ? 0f : Mathf.Clamp01(rms * gain);
            float speed = target > Loudness ? attackSpeed : releaseSpeed;
            Loudness = Mathf.MoveTowards(Loudness, target, speed * Time.deltaTime);
        }

        private float ReadWindowRms()
        {
            int writePosition = Microphone.GetPosition(deviceName);
            int startFrame = writePosition - windowFrames;

            if (startFrame < 0)
            {
                // 녹음을 시작한 지 얼마 안 돼 아직 한 창 분량도 안 쌓였다 — 조용한 것으로 취급한다.
                return 0f;
            }

            micClip.GetData(windowBuffer, startFrame);

            double sumSquares = 0;
            for (int i = 0; i < windowBuffer.Length; i++)
            {
                float sample = windowBuffer[i];
                sumSquares += sample * sample;
            }

            return (float)System.Math.Sqrt(sumSquares / windowBuffer.Length);
        }
    }
}
