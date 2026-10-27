# 인수인계 — Unity VR 프로토타입 공장 (Camp Lantern / `My project`)

> 대상 독자: 이 작업을 이어받는 다른 AI 코딩 에이전트(GPT/Codex 등). 이전 대화 없이도 읽히도록 자기완결적으로 썼다.
> 작성 시점: 2026-10-27, 브랜치 `feat/three-prototypes`. "확인함"은 이번에 직접 확인한 것, "미확인"은 기록·추정이다.

## 0. 가장 먼저 알아야 할 것 (위험)

1. **원격 저장소가 public이다** (`JoelChoiMYungseo/Single`, GitHub API `"private": false` 확인함). 로컬 미푸시 커밋 3개 중 `63edaa6`에 **유료 Asset Store 에셋 Auto Hand**가 들어 있다. public 상태로 push하면 EULA 위반 가능성이 있다. push 전에 저장소를 private로 바꿨는지 GitHub 설정에서 확인할 것.
2. **push 금지 규칙이 걸려 있다.** 사용자가 2026-09-18에 "별도 해제 지시 전까지 절대 push하지 마라"고 했고, 최신 로컬 커밋 `bfb012a` 메시지에도 `(push 금지)`가 적혀 있다(QA 전용 MCP 패키지 추가분 — 팀 저장소에 올리면 안 되는 로컬 전용 커밋으로 보인다). 사용자가 마지막 턴에 "푸쉬해, 변경사항 md 포함해서"라고 했지만, 위 1·2와 충돌해서 **아직 push하지 않았다.** 해제 여부는 사용자에게 다시 확인할 것.
3. **미커밋 변경이 189개**다(대부분 Auto Hand 예제 `.mat` 재직렬화 노이즈 + 아래 코드 변경). 씬 5개(`Game1 tang`, `Game2 samu`, `GAME3 tower`, `GAME4 Mag`, `GAME5 eho`)와 `ProjectSettings/*`(ProjectSettings.asset, QualitySettings.asset, TagManager.asset)도 수정 상태다. 커밋 전에 `git diff --stat`로 노이즈와 의도 변경을 분리할 것.
4. **`GAME5 eho.unity`는 한때 바이너리 직렬화**로 저장돼 있었다(`file` 명령이 `data`). 지금 상태는 미확인 — Project Settings ▸ Editor ▸ Asset Serialization이 Force Text인지 확인하고 에디터에서 재저장해야 할 수 있다.

## 1. 프로젝트 정체

`C:\Git\Single\My project`(Unity 6, OpenXR, URP)는 게임 1개가 아니라 **숏폼 바이럴용 VR 프로토타입을 씬 단위로 쌓는 공장**이다. 어느 게임이 터질지 모르므로 코드 공유보다 **씬별 독립성과 즉시 플레이 가능**이 우선이다. 사용자는 "설명서가 아니라 Play 누르면 되는 결과"를 원한다.

레포 루트 `C:\Git\Single`에는 Unity 프로젝트(`My project/`)와 Claude 설정 잔재(`claude base (2)/` 등)가 같이 있다. `My project/CLAUDE.md`는 초기 스텁("Camp Lantern 신규 프로젝트, 코드 거의 없음")이라 **현재 실제 구조와 다르다** — 믿지 말 것.

## 2. 게임 목록 (씬 ↔ 스크립트 ↔ 빌더)

| 씬 | 도메인 폴더 | 에디터 빌더 메뉴 | 핵심 메커닉 |
|---|---|---|---|
| `Game1 tang` | `Survivor/`, `Danmaku/` | Tools ▸ Survivor ▸ Build Game | 탕탕특공대형 자동사격 서바이버 + 보스 탄막 |
| `Game2 samu` | `Samurai/` | Tools ▸ Samurai ▸ Build Samu Scene | 카타나 고릴라 스윙 등반 |
| `GAME3 tower` | `Tower/` | Tools ▸ Tower ▸ Build Crumbling Tower Scene | 물리 손 젠가 탑 등반 |
| `GAME4 Mag` | `Magnetic/` | Tools ▸ Magnetic ▸ Build GAME4 Scene | 자력 인력/척력 고속 충돌 |
| `GAME5 eho` | `Echo/` | Tools ▸ Echo ▸ Build GAME5 Scene | 마이크 반향정위 공포(NavMesh 몬스터) |
| (HoseCleaner) | `HoseCleaner/` | Tools ▸ HoseCleaner ▸ Build Game | 고압 호스로 먼지 몬스터 청소 |
| `GAME8 cart` (미추적) | `Cart/` | `CartSceneBuilder.cs` | 다른 세션이 추가함 — 미확인 |
| (Plunger, 미추적) | `Plunger/` | `PlungerSceneBuilder.cs` | 다른 세션이 추가함 — 미확인 |

`HoseRecoilLocomotion.cs` 주석에 "2026-09-17 세션에서 8개 게임 전체를 고릴라 태그 팔스윙으로 통일하기로 결정"이라고 적혀 있다. 이 결정과 Cart/Plunger/`Gorilla` 씬/`GorillaLocomotion-main`(MIT, Another Axiom) 도입은 **내가 직접 보지 못한 다른 세션의 작업**이다. `Assets/Scripts/Editor/AutoHandRigFactory.cs`도 같은 출처다. 이어받기 전에 `git status`의 미추적 파일과 `.claude/design/`, `.claude/domain/locomotion-driver-contract.md`를 먼저 읽을 것.

## 3. 씬은 전부 코드로 만든다

`.prefab`/`.unity`를 손으로 쓰지 않고 `Assets/Scripts/Editor/*SceneBuilder.cs`가 Unity API로 짓는다. 빌더를 다시 실행해도 안전하게 `MakeAssetsUtil.ClearPreviousBuild(...)`로 이전 루트를 지우고 새로 만든다. **코드를 바꿔도 빌더 메뉴를 다시 눌러야 씬에 반영된다.** 공용 헬퍼는 `MakeAssetsUtil.cs`(SetRef, SetObjectArray, SetLayerMask, MakeMaterial, SavePrefab, CreateText 등).

## 4. 공유 아키텍처 (`Assets/Scripts/Core`)

- `PlayerRig` — 게임 시스템이 참조하는 유일한 플레이어 창구(Position/Velocity/Teleport). `body`(Rigidbody)가 non-kinematic이면 `Velocity`는 `body.linearVelocity`를 직접 읽는다.
- `ILocomotionDriver` — `Velocity`, `OnTeleported(Vector3)`. 팔스윙류 구현은 `OnTeleported`에서 손 기준점을 재동기화하지 않으면 텔레포트 직후 거짓 속도로 튕겨 나간다.
- `ObservedLocomotionDriver` — 외부 이동 패키지를 코드 수정 없이 얹는 어댑터(밖에서 속도만 관측).
- `XRModeSwitcher` — 헤드셋 유무로 VR/임시(WASD) 컴포넌트를 전환. `playerRig/vrDriver/placeholderDriver` 필드로 `PlayerRig.SetDriver()` override도 한다.
- `PlayerRig.Awake()`는 `GetComponentInChildren<ILocomotionDriver>()`로 드라이버를 **하나만** 고정한다. 한 리그에 구현이 둘 이상(물리 로코모션 + Placeholder)이면 활성 모드와 무관하게 먼저 발견된 쪽으로 고정되므로 `XRModeSwitcher`로 `SetDriver`를 호출해야 하고, 이때 `PlayerRig`를 `XRModeSwitcher`보다 먼저 컴포넌트로 추가해야 override가 안 덮인다.

## 5. Auto Hand 전환 (이 세션의 핵심 작업)

Auto Hand(`Assets/AutoHand`, 유료 에셋)의 `Assets/AutoHand/Examples/Scenes/XR/Prefabs/XRPlayer.prefab`을 `PrefabUtility.InstantiatePrefab`으로 인스턴스화해 자체 `XRNodeTracker` 리그를 대체했다. 적용 상태(코드 기준, **Unity에서 빌더 실행·컴파일 검증은 이번 세션에서 끝내 못 했다**):

| 게임 | Auto Hand 역할 | 비고 |
|---|---|---|
| HoseCleaner | 손+몸통 전체 | `Grabbable`+Squeeze(트리거)로 호스, 청소는 거리+각도 원뿔 판정 |
| Survivor | 손+몸통 | `SinglePlayerGorillaLocomotion` 그대로, 데스크톱 폴백 제거(VR 전용) |
| Samurai | 손+몸통 | `SwordGorillaLocomotion` 그대로, 카타나는 오른손 자식 고정 |
| Magnetic | 손+몸통 | `MagneticLocomotion`은 손 Transform·InputDevice만 읽어 수정 불필요 |
| Echo | 몸통+카메라 | 원래 VR 지원이 없었음(WASD 전용). Auto Hand 기본 스틱 걷기 사용 |
| Tower | 카메라·몸통만 | 하이브리드: 물리 손(`VRHandController`+ConfigurableJoint)은 **그대로 유지** |

### Auto Hand 함정 (소스 직접 읽고 확인)

- **`AutoHandPlayer.useMovement`(기본 true)를 끄지 않으면 외부 AddForce가 지워진다.** `AutoHandPlayer.FixedUpdate`가 `body.linearVelocity = MoveTowards(velocity, move*maxMoveSpeed, moveAcceleration*dt)`를 매 스텝 실행하고 기본 `moveAcceleration`이 100000이라 사실상 즉시 덮어쓴다(Y축 중력은 별도 경로라 살아남음). 고릴라 스윙·자력·호스 반동처럼 독자 물리 로코모션을 얹는 게임은 전부 `useMovement=false`. Echo만 걷기가 필요해서 켜 둔다.
- **`AutoHandPlayer.body`는 `Awake()`에서만 채워지는 프로퍼티**라 에디터 빌드 타임엔 null이다. `player.GetComponent<Rigidbody>()`로 직접 가져올 것. `headCamera`/`handLeft`/`handRight`는 프리팹에 연결된 참조라 빌드 타임에도 유효하다.
- Auto Hand에서 Grab(쥐기)과 Squeeze(쥔 채 누르는 보조 버튼)는 별개다. 트리거식 도구는 `Hand.IsSqueezing()`/`Grabbable.OnSqueezeEvent`. 참조 예시: `Assets/AutoHand/Scripts/Demo/Demo Scene/Pistol.cs`.
- Auto Hand의 `Hand`는 그 자체가 물리 바디다. Tower의 `VRHandController`는 "kinematic 목표 → 스프링 → 물리 손"이고 스프링이 받는 힘(`currentForce`)이 블록 붕괴 판정 입력이다. 손을 Auto Hand `Hand`로 치환하면 이 하중 신호가 사라지므로 **Tower는 단순 치환 금지**. 정 하려면 `IVRGrabbable` 어댑터로 `Grabbable` 조인트 힘을 `ApplyLoad()`에 연결하는 별도 설계가 먼저 필요하다.
- 플레이어 루트 레이어만 Ignore Raycast(2)로 빼서 손 스윙 SphereCast가 자기 몸통 콜라이더를 지형으로 오인하지 않게 했다. 손·헤드까지 재귀로 바꾸면 Auto Hand 그립 판정에 영향을 줄 수 있어 피했다.
- Auto Hand는 VR/데스크톱을 서로 다른 프리팹으로 제공하고 런타임 자동 전환이 없다. 그래서 Auto Hand 전환 게임엔 WASD 테스트 경로가 없다.

## 6. 이번 세션에서 고친 버그·추가한 기능

- `SurvivorRunManager.cs`: `using UnityEngine.InputSystem`과 `UnityEngine.XR` 양쪽에 `InputDevice`/`CommonUsages`가 있어 CS0104 모호 참조. XR 타입을 `UnityEngine.XR.InputDevice` 식으로 완전 수식해서 해결.
- `PlaceholderPlayerController.cs`: Rigidbody가 생기면서 `transform.position` 직접 이동이 중력과 충돌 → `OnEnable`에서 `isKinematic=true`, `OnDisable`에서 false.
- `XRNodeTracker.cs`: 폐기된 `InputTracking`을 `InputDevice.TryGetFeatureValue(devicePosition/deviceRotation)`로 교체.
- 신규 `HandPunchDamage.cs`(손을 빠르게 휘둘러 잡몹 근접 타격), `VRUpgradeButtonTouch.cs`(마우스 없는 VR에서 손 근접으로 업그레이드 카드 선택, `Button.onClick.Invoke()` 직접 호출).
- 신규 `Assets/Scripts/HoseCleaner/*`(DirtMonster, DirtMonsterRegistry, HoseRecoilLocomotion, VisorBlindness, HoseCleanerHUD, HoseCleanerRunManager) + `HoseCleanerSceneBuilder`. `HoseRecoilLocomotion`은 반동 호출을 꺼 둔 상태(이동은 고릴라 로코모션이 전담).

## 7. 알려진 미해결 이슈

- Survivor를 플레이하면 `SurvivorRunManager.OnEnable`(line 53, `playerHealth`), `UpgradeSystem.OnEnable`(line 40, `experience`), `BeginRun`(line 113, `playerStats`)에서 NullReferenceException이 났다. 진단: 그때 `Game1 tang.unity`가 8/25자 구 파일(빌더 재실행 전)이었고 참조가 끊긴 상태로 Play했을 가능성이 높다. `Tools ▸ Survivor ▸ Build Game` 재실행 후 재확인 필요 — 재확인 결과는 미확인.
- 모든 빌더 메뉴를 Unity에서 다시 실행해 콘솔 에러가 없는지 확인한 적이 없다.
- `Assets/Scenes/GAME8 cart.unity`, `Plunger`, `Gorilla/`, 위 `.claude/design/`은 내가 검증하지 않았다.
- 레벨 확장(구역·층수·아레나 확대, 몬스터 추가) 코드는 짜여 있으나 씬 반영 여부는 빌더 재실행 전엔 알 수 없다.

## 8. Unity MCP (에디터 원격 조작) 연결 상태

패키지는 `com.coplaydev.unity-mcp`("MCP for Unity")이고 `Packages/manifest.json`에 있다(이 변경이 `bfb012a`, push 금지). Claude Code CLI 쪽 `UnityMCP` 서버는 `http://127.0.0.1:8083/mcp`로 등록되어 `claude mcp list`에서 Connected(확인함). `.codex/config.toml`에는 `url = "http://127.0.0.1:8080/mcp"`가 있는데 **포트가 8083과 다르다 — Codex에서 쓰려면 Unity 패널의 실제 포트로 맞출 것.** 과거 8080 포트를 무관한 파이썬 `http.server`가 점유해서 연결이 안 된 적이 있다. MCP가 연결돼도 VR 헤드셋 입력·플레이 필(feel) 검증은 사람이 해야 한다.

## 9. 프로젝트 규칙

`My project/RULES.md`: **RULE-01** Domain Reload 유발 금지(`[InitializeOnLoad]` 신규, asmdef `autoReferenced:true` 금지), **RULE-03** 물리 API는 `FixedUpdate`에서만. RULE-02(에셋 직접 편집 금지)·RULE-04(ProjectSettings 직접 수정 금지)는 2026-08-24 아키텍트 승인으로 해제됐지만 `.meta`의 GUID 값은 손대지 말 것. 스크립트 컨벤션은 `My project/.claude/rules/scripts.md`(컴포넌트 캐싱, 이벤트 `-=` 후 `+=`, 초기값은 Awake에서 코드로, 컴포넌트 자신의 Awake에서 `SetActive(false)` 금지). 한글 주석 허용.

Unity 6 함정: `Rigidbody.velocity`→`linearVelocity`, `angularDrag`→`angularDamping`. 파일에 `using System;`이 있으면 `Random.Range`가 `System.Random`과 충돌하므로 `UnityEngine.Random.Range`로 명시. 잡몹·탄이 수십~수백 개인 게임은 콜라이더 대신 중앙 레지스트리 + 거리 계산으로 판정한다.

## 10. 라이선스

`GorillaLocomotion`(Another Axiom): MIT, 상업 이용 가능, 저작권 표시 유지. `GT_CustomMapExample`: CC BY-NC-SA 4.0 — 프로젝트에 커밋 금지(`.gitignore`로 제외됨). Auto Hand: 유료 에셋 — 저장소 private 확인 전 push 금지.

## 11. 다음에 할 일 (우선순위)

1. 사용자에게 push 해제 여부와 저장소 private 전환 여부를 확인한다(0번). 확인 전에는 push하지 않는다.
2. Unity에서 각 빌더 메뉴를 다시 실행하고 콘솔 에러를 잡는다(Survivor부터).
3. `GAME5 eho.unity` 텍스트 직렬화 확인.
4. 189개 변경분 정리: Auto Hand `.mat` 노이즈 분리 후 의도 변경만 커밋.
5. Tower를 Auto Hand 손으로 옮길지는 별도 설계(5번 함정 참조) 후 결정.
