# Step 03: MonsterFaceStick — 몬스터 얼굴 흡착 분기

- **영역:** `plunger` (`Assets/Scripts/Plunger/`)
- **선행 단계:** step-01(`PlungerLocomotion.OnStuck`/`OnLaunched` 이벤트), step-02(`PlungerMonster.Blind()`) 완료 필요
- **후행 단계:** step-05(SceneBuilder)가 몬스터 프리팹에 이 컴포넌트를 붙이고 `Bind(playerLocomotion)`을 호출한다

---

## 목적
몬스터의 머리 콜라이더에 붙는 컴포넌트. `PlungerLocomotion`은 자신이 무엇에 흡착됐는지 모른 채로 `OnStuck(isLeft, anchorTransform)` 이벤트만 쏜다(step-01 참고) — `MonsterFaceStick`은 그 이벤트를 구독해 `anchorTransform`이 자기 자신(또는 자기 하위)인지 확인하고, 맞으면:
1. [PlungerMonster](../../../Assets/Scripts/Plunger/PlungerMonster.cs)(같은 GameObject 또는 부모에 있음)의 `Blind()`를 호출해 일시 실명시킨다.
2. 이후 `OnLaunched` 이벤트가 같은 손에서 발생하면(=발사 순간), 설정에 따라 "플레이어를 몬스터 쪽으로 당김" 또는 "몬스터에서 튕겨 나감" 중 하나로 연출을 더한다 — 둘 다 물리 힘 자체는 `PlungerLocomotion`이 이미 처리하므로, 이 컴포넌트는 그 위에 얹는 부가 연출(예: 몬스터 쪽에도 반작용 힘을 살짝 주는 등)만 맡는다.

**아키텍트 확인 필요 (원 프롬프트가 "당기거나 발사" 둘 다 언급하고 택일 기준이 없음):** 기본값은 "튕겨 나감"(원 프롬프트의 슬링샷 메커닉과 일관), 인스펙터 bool로 "당김" 모드 전환 가능하게 만든다.

---

## 에이전트 실행 지침

`/task-start`를 먼저 호출해 범위를 확정한 뒤, 아래 지시를 수행한다.

### 생성 파일
- `Assets/Scripts/Plunger/MonsterFaceStick.cs` — 신규

### 핵심 심볼
```csharp
namespace VRGame.Plunger
{
    [RequireComponent(typeof(Collider))]
    public class MonsterFaceStick : MonoBehaviour
    {
        [SerializeField] private PlungerMonster monster; // 비우면 GetComponentInParent로 자동 탐색
        [SerializeField] private float blindDuration = 2.5f; // PlungerMonster.panicDuration 대신 이 값을 우선 사용
        [SerializeField] private bool pullPlayerTowardOnLaunch = false; // false = 몬스터에서 튕겨 나감(기본), true = 당김

        // step-05(SceneBuilder)가 플레이어 리그를 만든 뒤 호출해 이벤트를 연결한다.
        public void Bind(PlungerLocomotion locomotion);
    }
}
```

### 동작 규칙
- `Bind()`에서 `locomotion.OnStuck += HandleStuck;`처럼 구독하기 **직전에 먼저 `-=`로 해제**한다 ([이벤트 구독 해제 원칙](../../rules/scripts.md#이벤트-구독-해제) — 재바인딩 시 중복 구독 방지).
- `OnDestroy()`에서 반드시 구독 해제한다 (몬스터가 죽어서 파괴될 때 리그의 이벤트가 파괴된 객체를 계속 들고 있지 않게).
- `HandleStuck(bool isLeft, Transform anchor)`: `anchor == transform`(또는 `anchor.IsChildOf(transform)`)일 때만 반응. 아니면 무시.
- 반응 시: `monster.Blind(blindDuration)` 호출, 이후 `locomotion.OnLaunched`를 **1회만** 구독해 발사 연출을 적용하고 바로 구독 해제(다음 흡착까지 중복 반응 방지) — 또는 `isLeft` 일치 여부로 매칭.

### 선행 산출물 의존성
- `PlungerLocomotion.OnStuck` / `OnLaunched` — step-01에서 정의됨
- `PlungerMonster.Blind(float)` — step-02에서 정의됨

### 완료 판정
- [ ] `Grep "class MonsterFaceStick" Assets/Scripts/` 로 정의 확인
- [ ] `Bind(PlungerLocomotion)` public 메서드 존재 확인
- [ ] 이벤트 구독 해제 쌍(`OnDestroy`에서 `-=`) 존재 확인
- [ ] Unity 컴파일 통과

---

## 금지 사항
- `PlungerLocomotion.cs`를 수정해 몬스터 타입을 직접 참조하게 만들지 않는다 — 결합 방향은 항상 `MonsterFaceStick → PlungerLocomotion`이다 (반대 아님).
- 물리 힘 재계산(별도 AddForce)이 필요하면 `FixedUpdate()`에서만 (RULE-03).
