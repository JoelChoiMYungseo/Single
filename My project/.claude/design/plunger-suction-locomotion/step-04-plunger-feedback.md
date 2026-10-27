# Step 04: PlungerFeedback — 햅틱·오디오 피드백

- **영역:** `plunger` (`Assets/Scripts/Plunger/`)
- **선행 단계:** step-01(`PlungerLocomotion.OnStateChanged`/`OnLaunched`) 완료 필요. step-02/03과는 무관 — **step-03과 병렬 실행 가능**.
- **후행 단계:** step-05(SceneBuilder)가 플레이어 리그에 붙이고 `Bind(playerLocomotion)`을 호출, 오디오 클립/컨트롤러 참조를 주입한다

---

## 목적
`PlungerLocomotion`의 상태 전이를 구독해 압축(Compressed 진입)·발사(Launched) 순간에 각 손 컨트롤러로 햅틱을 보내고, 흡착("Suck")·발사("Pop") 효과음을 재생한다. 물리·게임 로직을 전혀 모르는 순수 프레젠테이션 레이어 — [MagneticLocomotion.SendHaptic](../../../Assets/Scripts/Magnetic/MagneticLocomotion.cs#L211)이 로코모션 안에 직접 있는 것과 달리, 이번엔 프롬프트가 명시적으로 별도 매니저를 요구했으므로 로코모션 파일 밖으로 분리한다.

---

## 에이전트 실행 지침

`/task-start`를 먼저 호출해 범위를 확정한 뒤, 아래 지시를 수행한다.

### 생성 파일
- `Assets/Scripts/Plunger/PlungerFeedback.cs` — 신규

### 핵심 심볼
```csharp
namespace VRGame.Plunger
{
    public class PlungerFeedback : MonoBehaviour
    {
        [Header("Haptics")]
        [SerializeField] private float compressHapticAmplitude = 0.3f;
        [SerializeField] private float launchHapticAmplitude = 1f;
        [SerializeField] private float launchHapticDuration = 0.08f;

        [Header("Audio")]
        [SerializeField] private AudioSource leftHandAudio;
        [SerializeField] private AudioSource rightHandAudio;
        [SerializeField] private AudioClip suctionAttachClip;
        [SerializeField] private AudioClip launchPopClip;

        // step-05(SceneBuilder)가 플레이어 리그를 만든 뒤 호출해 이벤트를 연결한다.
        public void Bind(PlungerLocomotion locomotion);
    }
}
```

### 동작 규칙
- `Bind()`에서 `OnStateChanged`, `OnLaunched` 구독 — 구독 직전 `-=` 먼저 호출 ([이벤트 구독 해제 원칙](../../rules/scripts.md)).
- `OnDestroy()`에서 구독 해제.
- `state == PlungerState.Stuck`으로 전이하는 순간 `suctionAttachClip` 재생 (해당 손의 `AudioSource`).
- `OnLaunched(isLeft, impulse)` 발생 시: `launchPopClip` 재생 + `impulse.magnitude`에 비례한 세기로 햅틱(`InputDevices.GetDeviceAtXRNode` + `TryGetHapticCapabilities` + `SendHapticImpulse` — [Magnetic의 SendHaptic](../../../Assets/Scripts/Magnetic/MagneticLocomotion.cs#L211)과 동일한 API 호출 형태, 이 프로젝트에 아직 공용 `XRHaptics` 유틸이 없으므로 이번에도 직접 호출).
- 압축 중(Compressed 유지 시간 동안) 텐션이 커질수록 햅틱이 강해지는 연출이 필요하면 `Update()`에서 `PlungerLocomotion`이 노출하는 현재 텐션 값을 폴링해야 하는데, step-01 계약엔 그런 프로퍼티가 없다 — **이번 스코프에선 압축 중 지속 햅틱은 생략**하고 상태 전이 시점 펄스 햅틱만 구현한다(원 프롬프트의 "compression과 launch 중 햅틱" 요구를 최소 범위로 축소한 것 — 아키텍트 확인 필요, 원하면 step-01에 `CurrentTension(bool isLeft)` 프로퍼티를 추가하는 후속 스텝을 판다).

### 선행 산출물 의존성
- `PlungerLocomotion.OnStateChanged` / `OnLaunched` — step-01에서 정의됨

### 완료 판정
- [ ] `Grep "class PlungerFeedback" Assets/Scripts/` 로 정의 확인
- [ ] `Bind(PlungerLocomotion)` public 메서드 존재 확인
- [ ] 이벤트 구독 해제 쌍 존재 확인 (`OnDestroy`)
- [ ] Unity 컴파일 통과

---

## 금지 사항
- `PlungerLocomotion.cs`를 수정하지 않는다 — 이벤트만 소비한다.
- 압축 중 지속 햅틱을 구현하려고 step-01의 계약(이벤트 시그니처)을 임의로 바꾸지 않는다 — 필요하면 먼저 보고한다.
