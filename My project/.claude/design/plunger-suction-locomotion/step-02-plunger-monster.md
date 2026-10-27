# Step 02: PlungerMonster — 최소 추적 몬스터 + Blind()

- **영역:** `plunger` (`Assets/Scripts/Plunger/`)
- **선행 단계:** 없음 (step-01과 병렬 실행 가능 — 서로 참조하지 않음)
- **후행 단계:** step-03(MonsterFaceStick)이 `Blind()`를 호출하고, step-05(SceneBuilder)가 이 컴포넌트를 몬스터 프리팹에 붙인다

---

## 목적
원 프롬프트는 "몬스터 AI"의 존재를 전제로 `MonsterFaceStick`을 그 위에 얹으라고 요청했지만, 이 프로젝트엔 재사용 가능한 공용 몬스터 AI가 없다(Echo의 `SoundAttractedAI`, HoseCleaner의 `DirtMonster`는 각자 프로토타입 전용 로직이 섞여 있어 그대로 가져다 쓸 수 없음). 이 단계는 플런저 프로토타입을 플레이 가능하게 만드는 데 필요한 **최소** 몬스터 하나를 새로 만든다 — NavMeshAgent로 플레이어를 추적하고, `Blind(duration)`이 호출되면 일정 시간 추적을 멈추고(패닉) 이후 재개한다.

**아키텍트 확인 필요:** 이 몬스터의 공격/데미지 로직은 이번 스코프에 없음 (원 프롬프트에도 없었음) — 추적만 하는 "표적" 역할이다. 실제 위협/데미지가 필요하면 별도 설계로 추가한다.

---

## 에이전트 실행 지침

`/task-start`를 먼저 호출해 범위를 확정한 뒤, 아래 지시를 수행한다.

### 생성 파일
- `Assets/Scripts/Plunger/PlungerMonster.cs` — 신규

### 핵심 심볼
```csharp
namespace VRGame.Plunger
{
    [RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
    public class PlungerMonster : MonoBehaviour
    {
        [SerializeField] private float chaseSpeed = 3.5f;
        [SerializeField] private float panicDuration = 2f; // 기본 실명 지속 시간 — MonsterFaceStick이 override 가능

        public bool IsBlind { get; }

        // step-03(MonsterFaceStick)이 얼굴에 플런저가 꽂히는 순간 호출한다.
        // duration을 생략하면 panicDuration을 쓴다.
        public void Blind(float duration = -1f);
    }
}
```

### 동작 규칙
- `Awake()`에서 `NavMeshAgent`를 캐싱하고 `speed = chaseSpeed`로 설정한다 ([rules/scripts.md](../../rules/scripts.md) 컴포넌트 캐싱 원칙).
- `Update()`에서 `IsBlind`가 아니면 플레이어 위치로 `SetDestination` (플레이어 참조는 스포너가 주입 — Echo의 `BuildMonster(Transform player, ...)` 패턴 참고, [EchoSceneBuilder.cs](../../../Assets/Scripts/Editor/EchoSceneBuilder.cs) 근처).
- `Blind()` 호출 시 `NavMeshAgent.isStopped = true`로 즉시 멈추고, 코루틴으로 `panicDuration`(또는 인자로 받은 값) 뒤 `isStopped = false`로 복귀 — [코루틴 중복 방지 원칙](../../knowledge/unity-scripting-gotchas.md) 적용 (핸들 저장 후 재호출 시 기존 코루틴 정지).
- "패닉 애니메이션"은 이번 스코프에서 `Animator` 유무를 가정하지 않는다 — `Animator` 컴포넌트가 있으면 `SetTrigger("Panic")`을 시도하고, 없으면 조용히 스킵한다 (null 체크로 방어, 프리팹에 Animator가 없어도 에러 안 나게).

### 선행 산출물 의존성
- 없음

### 완료 판정
- [ ] `Grep "class PlungerMonster" Assets/Scripts/` 로 정의 확인
- [ ] `Blind(float duration = -1f)` public 메서드 시그니처 확인
- [ ] 코루틴 핸들이 필드에 저장되고 재호출 시 기존 코루틴을 정지하는지 확인
- [ ] Unity 컴파일 통과

---

## 금지 사항
- 데미지/공격 로직을 넣지 않는다 (스코프 밖 — 아키텍트 확인 전엔 추측 금지).
- `MonsterFaceStick`을 이 파일에 같이 만들지 않는다 — step-03의 범위다.
