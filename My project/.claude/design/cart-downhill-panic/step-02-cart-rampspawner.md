# Step 02: DownhillRampSpawner — 무한 램프 스포너

- **영역:** `cart` (`Assets/Scripts/Cart/`)
- **선행 단계:** 없음 (step-01과 병렬 실행 가능 — 카트 Transform만 추적할 뿐 `DownhillCart` 타입을 참조하지 않음)
- **후행 단계:** step-05(SceneBuilder)가 세그먼트 프리팹을 만들어 주입하고 씬에 배치한다

---

## 목적
"무한 내리막"을 실제 무한 지형으로 만들지 않고, 경사+장애물이 있는 세그먼트(수십 미터 길이)를 앞쪽에 몇 개 유지하다가 카트가 지나간 뒤쪽 세그먼트를 앞으로 재배치하는 방식으로 흉내낸다 — [BulletPool](../../../Assets/Scripts/Danmaku/BulletPool.cs)류의 "새로 만들지 않고 재사용" 관례를 세그먼트 단위로 적용한 것.

---

## 에이전트 실행 지침

`/task-start`를 먼저 호출해 범위를 확정한 뒤, 아래 지시를 수행한다.

### 생성 파일
- `Assets/Scripts/Cart/DownhillRampSpawner.cs` — 신규

### 핵심 심볼
```csharp
namespace VRGame.Cart
{
    public class DownhillRampSpawner : MonoBehaviour
    {
        [SerializeField] private Transform tracked;           // 카트 Transform — SceneBuilder가 주입
        [SerializeField] private GameObject[] segmentPrefabs;  // 경사+장애물이 배치된 세그먼트 프리팹 후보
        [SerializeField] private float segmentLength = 20f;
        [SerializeField] private int segmentsAhead = 4;        // tracked 앞에 항상 유지할 개수
        [SerializeField] private float recycleBehindDistance = 15f; // tracked보다 이만큼 뒤처지면 재배치

        // SceneBuilder가 빌드 시점에 결정적 배치를 원하면 호출 — 생략하면 Random 기본 시드 사용.
        public void Seed(int seed);
    }
}
```

### 동작 규칙
- `Awake()`(또는 최초 `Update()` 1회)에서 `segmentsAhead`개만큼 세그먼트를 `tracked.position`부터 `segmentLength` 간격으로 미리 배치한다(내부 리스트에 인스턴스·시작 Z좌표 보관).
- 매 프레임(`Update()`로 충분 — 지형 배치는 물리가 아니라 위치 계산이라 RULE-03 대상 아님) 가장 뒤에 있는 세그먼트가 `tracked.position`보다 `recycleBehindDistance` 이상 뒤처졌는지 확인 — 그러면 그 세그먼트를 리스트에서 꺼내 가장 앞 세그먼트 바로 뒤(앞쪽)로 위치만 옮겨 다시 리스트 앞에 넣는다 (Destroy/Instantiate 없이 재배치 — Danmaku BulletPool과 동일한 "재사용" 철학).
- 세그먼트 프리팹 내부의 장애물 배치(어떤 장애물이 어디 있는지)는 이 스크립트가 책임지지 않는다 — `segmentPrefabs` 배열에 미리 다양한 배치를 가진 여러 프리팹을 준비해두고 `Random.Range`로 고르는 정도로 충분하다(진짜 절차적 배치 알고리즘은 스코프 밖 — 필요해지면 후속 설계).
- 재배치 시 `Random.Range(0, segmentPrefabs.Length)`로 다음 세그먼트 프리팹을 새로 골라 **모델을 바꿔야** 하면(다른 프리팹으로 교체) Destroy 후 재-Instantiate가 불가피하다 — 이 경우는 예외적으로 허용하되, 같은 프리팹을 반복 재사용할 땐 위치만 옮긴다.

### 선행 산출물 의존성
- 없음

### 완료 판정
- [ ] `Grep "class DownhillRampSpawner" Assets/Scripts/` 로 정의 확인
- [ ] `Seed(int)` public 메서드 확인
- [ ] 세그먼트 재배치가 Update()(물리 아님)에 있는지 확인 — RULE-03 위반 아님을 명시적으로 확인
- [ ] Unity 컴파일 통과

---

## 금지 사항
- `DownhillCart` 타입을 참조하지 않는다 — 이 스크립트는 순수하게 `Transform tracked`만 안다 (결합 최소화).
- 실시간 절차적 장애물 배치 알고리즘(난이도 곡선 등)을 새로 설계하지 않는다 — 프리팹 후보 중 무작위 선택으로 충분.
