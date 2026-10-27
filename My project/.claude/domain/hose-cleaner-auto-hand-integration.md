# Auto Hand 통합 (GAME6 Hose Cleaner, Survivor)

> **2026-09-17 세션 추가**: "손에 고정 부착" 대신 "실제 Grabbable로 만들어서 플레이어 근처에 두고 직접 쥐게 하기" 패턴이 이제 HoseCleaner(호스)·Samurai(카타나)·Cart(조향 막대) 3곳에서 검증됨 — `Rigidbody` + 그립용 `Collider` + `Autohand.Grabbable`을 붙이고, 손 Transform에 `SetParent`로 고정하지 않는다. 쥐고 있는 동안만 그 오브젝트의 하위 Transform(칼끝, 손잡이 등)이 손을 따라 움직이므로, 그 값에 의존하는 로코모션(SwordGorillaLocomotion, StickSteering)도 "쥐고 있을 때만 작동"하게 자연히 바뀐다 — 의도된 동작.



## 한 줄 요약
Auto Hand의 `XRPlayer` 프리팹을 자체 리그 대신 그대로 가져다 쓰는 패턴. GAME6(Hose Cleaner)에서 처음 썼고, 이후 Survivor(GAME1)의 고릴라 태그 로코모션도 같은 방식으로 갈아 끼웠다 — 이동 방식은 서로 완전히 다르지만(호스 반동 vs 팔스윙) 리그를 가져오는 절차는 동일하다.

## 핵심 타입 / 진입점
- `HoseRecoilLocomotion` — `Assets/Scripts/HoseCleaner/HoseRecoilLocomotion.cs` — 호스 Grabbable. Squeeze(트리거) 입력을 읽어 `AutoHandPlayer.Instance.body`에 반동 AddForce.
- `DirtMonster` / `DirtMonsterRegistry` — `Assets/Scripts/HoseCleaner/` — NavMeshAgent 추적 + Dirtiness(IDamageable). Echo의 `SoundAttractedAI`/`EnemyRegistry` 패턴 재사용.
- `VisorBlindness` — 근접 시 화면 스플래터.
- `HoseCleanerSceneBuilder` — `Assets/Scripts/Editor/HoseCleanerSceneBuilder.cs` — `Tools/HoseCleaner/Build Game`. `Assets/AutoHand/Examples/Scenes/XR/Prefabs/XRPlayer.prefab`을 `PrefabUtility.InstantiatePrefab`으로 그대로 가져다 쓴다.

## 시스템 간 관계
- `HoseRecoilLocomotion`은 `PlayerRig`를 전혀 모른다 — `AutoHandPlayer.Instance`(Auto Hand 자체 싱글톤)를 직접 참조해 반동을 준다. 이동의 "원인"은 PlayerRig 바깥에 있다.
- `ObservedLocomotionDriver`는 그 결과(Rigidbody 속도)만 관측해서 `PlayerRig.Velocity`로 보고한다. 즉 이 게임에서 `PlayerRig`는 "이동 수단을 모르는 관전자"이지 "이동을 일으키는 주체"가 아니다 — Samurai/Survivor의 자체 로코모션(로코모션 스스로 `ILocomotionDriver` 구현)과 다른 배선이다.
- `DirtMonster`는 `player: Transform`을 스포너가 주입받아 쫓을 뿐, `PlayerRig`도 `AutoHandPlayer`도 모른다 — Echo의 몬스터와 동일하게 결합도를 낮췄다.

## 기획 의도 / 역사적 맥락
"Auto Hand를 최대한 활용해서 만들어달라"는 요청이 있었다. 다른 5개 게임처럼 `XRNodeTracker`(코어 XR 모듈만 쓰는 자체 손 추적)로 새로 지을 수도 있었지만, 그러면 Grab/Squeeze/손 IK를 전부 직접 구현해야 해서 "Auto Hand 활용"이라는 요청의 본질을 놓친다. 대신 Auto Hand가 이미 완성해 둔 `XRPlayer` 프리팹(핸드 IK, 그립 조인트, 이동·중력·점프)을 그대로 쓰고, 그 위에 이 프로젝트의 `PlayerRig`/`ILocomotionDriver` 계약을 **어댑터로 얹었다** — `ObservedLocomotionDriver`가 정확히 이 상황(소스를 수정할 수 없는 외부 이동 패키지)을 위해 이미 존재했다(주석에 "Another Axiom의 MIT GorillaLocomotion" 예시로 명시돼 있었음). 이번이 그 설계 의도가 실제로 검증된 첫 사례.

## 숨은 규칙 / 암묵지
- **`AutoHandPlayer.body`는 프로퍼티이고 자기 자신의 `Awake()`에서 `GetComponent<Rigidbody>()`로 채워진다.** 씬 빌더(에디터, Play 이전)에서 이 값을 읽으면 **null**이다. 프리팹을 인스턴스화한 직후엔 `player.GetComponent<Rigidbody>()`로 직접 가져와야 한다 — `headCamera`/`handLeft`/`handRight`는 (Inspector에 미리 연결된 참조라) 이 문제가 없다. **다른 Auto Hand 필드를 빌드 타임에 읽을 때도 "런타임에만 채워지는 프로퍼티인가"를 먼저 의심할 것.**
- **`AutoHandPlayer.useMovement`(기본 true)를 끄지 않으면 외부 AddForce가 매 프레임 지워진다.** `AutoHandPlayer.FixedUpdate`가 `body.linearVelocity = Vector3.MoveTowards(body.linearVelocity, move * maxMoveSpeed, moveAcceleration * Time.fixedDeltaTime)`를 매 스텝 실행하는데, 기본 `moveAcceleration`이 100000이라 사실상 그 프레임에 velocity를 `move*maxMoveSpeed`(스틱 입력 없으면 0)로 스냅해버린다. 그래서 호스 반동이든 고릴라 스윙 반동이든, **AutoHandPlayer 위에 독자적인 물리 로코모션을 얹는 게임은 전부 `useMovement = false`가 필요하다.** (Y축 중력은 별도 경로라 안 죽는다.) 이 프로젝트에서 HoseCleaner와 Survivor 둘 다 이 문제를 겪었다 — **AutoHandPlayer.body에 AddForce를 주는 새 게임을 만들 때 이 설정을 빠뜨리지 말 것.**
- Auto Hand에서 "쥐기(Grab)"와 "방아쇠(Squeeze)"는 별개의 입력이다. 총/호스처럼 쥔 채로 누르는 보조 입력은 `Hand.IsSqueezing()`/`GetSqueezeAxis()`와 `Grabbable.OnSqueezeEvent`/`OnUnsqueezeEvent`로 받는다 — `OnGrabEvent`(쥠 자체)와 혼동하지 말 것. 데모의 `Assets/AutoHand/Scripts/Demo/Demo Scene/Pistol.cs`가 정확한 참조 예시.
- 호스를 플레이어 손에 코드로 강제로 쥐여주지 않았다(`ForceGrab` 파라미터 의미를 이번 세션에서 검증 못 함). 대신 스폰 지점 앞에 세워 둬서 플레이어가 직접 집게 했다 — Auto Hand 데모들의 표준적인 온보딩과 동일.
- **Survivor를 Auto Hand로 옮긴 뒤엔 데스크톱(비VR) 폴백이 없다.** 기존 `PlaceholderPlayerController`/`XRModeSwitcher`(헤드셋 유무로 WASD ↔ VR 전환)를 걷어내고 Auto Hand의 `XRPlayer`만 남겼다 — Auto Hand 자체가 VR/데스크톱을 각각 다른 프리팹(`XRPlayer` vs `Auto Hand Player Container`)으로 나눠 제공하지, 런타임 자동 전환을 안 해주기 때문이다. WASD로 테스트하고 싶으면 데스크톱용 Auto Hand 프리팹으로 별도 빌더 분기가 필요하다 — 아직 안 만듦.
- `SinglePlayerGorillaLocomotion`/`HoseRecoilLocomotion`처럼 손 **Transform 참조만** 필요한 로코모션은 코드 수정 없이 얹힌다 — `leftHand`/`rightHand` 필드에 `autoHandPlayer.handLeft.transform`/`handRight.transform`을 넣어주기만 하면 된다. 반대로 Tower의 `VRHandController`처럼 **손 자체가 물리 오브젝트(ConfigurableJoint)** 이고 그 조인트가 받는 힘을 블록 붕괴 판정 입력으로 쓰는 시스템은, Auto Hand의 위치 기반 손으로 바꾸면 그 하중 신호가 사라진다 — Tower는 아직 이 전환을 안 했고, 하려면 `IVRGrabbable` 어댑터로 `Grabbable`의 조인트 힘을 `ApplyLoad()`에 연결하는 별도 설계가 먼저 필요하다(단순 치환 금지).
- **`PlayerRig.root`은 반드시 `autoHandPlayer.gameObject.transform`이어야 한다 — 래퍼(`Spawn`/`SpawnWithGorillaLocomotion`이 반환하는 GameObject)를 넣으면 안 된다.** Unity에서 자식 오브젝트의 Rigidbody/Transform이 물리로 움직여도 부모 Transform은 전혀 안 움직인다 — 그런데 로코모션·Another Axiom Player 전부 실제로는 `autoHandPlayer.gameObject`(래퍼의 자식)를 움직인다. 8개 게임 SceneBuilder가 전부 `root`에 래퍼를 넣고 있었던 걸 2026-09-17 세션에서 발견해 전부 고쳤다 — 그 전까지는 `PlayerRig.Position`/`Teleport()`가 스폰 시점 위치에 얼어붙어 있었을 것이다(`FallManager`/`TowerRunManager`의 높이·추락 판정, 리스폰 등이 전부 영향받는 심각한 버그였다 — 아무도 실제 Play로 검증 안 해서 이것도 안 걸렸다).
- **`AutoHandRigFactory.SpawnWithGorillaLocomotion(...)`**(`Assets/Scripts/Editor/AutoHandRigFactory.cs`)이 `Spawn` 위에 "고릴라 태그 팔스윙 배선"(손 참조·환경 마스크·Ignore Raycast)까지 얹은 상위 헬퍼다. Survivor/Magnetic/Tower/HoseCleaner/Echo가 이걸 쓴다. Samurai(`SwordGorillaLocomotion` 변형)와 Plunger/Cart(로코모션 자체가 게임 컨셉)는 여전히 `Spawn`을 직접 호출하고 각자 배선한다.
- **2026-09-17 세션에서 8개 게임 전체의 로코모션을 고릴라 태그 팔스윙(`SinglePlayerGorillaLocomotion`)으로 통일했다** (사용자 명시적 지시). Survivor/Samurai는 원래부터 이 계열(Samurai는 katana-tip 변형)이라 그대로 뒀고, Magnetic/Tower/HoseCleaner/Echo는 기존 로코모션을 걷어내고 교체했다. **부작용(사용자가 명시적으로 승인한 트레이드오프):** Magnetic의 자력 발사·하자드 견인, Tower의 물리 손 등반과 그에 의존하던 `UnstableBlock` 붕괴 판정(`IVRGrabbable.ApplyLoad` 호출 경로)이 전부 죽었다 — 관련 컴포넌트(`MagneticLocomotion`, `CrashHandler`는 유지, `VRHandController`, `FallManager`/`TowerRunManager`의 `hands` 필드)는 삭제하지 않고 "더 이상 씬에서 안 쓰임" 상태로 남겨뒀다(되돌리기 쉽게). HoseCleaner는 호스의 반동 추진(`HoseRecoilLocomotion.ApplyRecoil`) 호출만 껐고 청소 기능은 그대로다. Plunger/Cart(2026-09-17에 새로 만든 프로토타입)는 이 통일 대상에서 제외했다 — 각자의 로코모션 자체가 게임 컨셉이라(흡착 발사, 카트 탑승) 팔스윙으로 바꾸면 게임이 성립하지 않는다.
- **`AutoHandPlayer`는 `XRPlayer.prefab`을 인스턴스화한 바로 그 루트 GameObject 위에 있지 않다.** 루트는 `XRHandOffset` 컴포넌트 두 개(디바이스별 손 오프셋 보정용)만 들고 있는 래퍼고, `AutoHandPlayer`·그 `Rigidbody`·`CapsuleCollider`는 그 밑에 **중첩된 프리팹 인스턴스(자식)** 쪽에 있다. 2026-09-17 세션에서 Unity를 처음 실제로 켜서 Play해보고서야 발견됨 — 그전까지 6개 게임의 SceneBuilder가 전부(이 문서 작성 시점 포함) `rig.GetComponent<AutoHandPlayer>()`로 루트에서 바로 찾으려 했는데, 컴파일은 통과해도 런타임엔 `null` → `NullReferenceException`이었다. 아무도 실제로 Play를 안 눌러봐서 몇 주간 안 걸렸다. **반드시 `rig.GetComponentInChildren<AutoHandPlayer>(true)`로 찾고, Rigidbody도 그 `AutoHandPlayer` 자신의 GameObject(`autoHandPlayer.GetComponent<Rigidbody>()`)에서 가져와야 한다** — 지금은 `AutoHandRigFactory.Spawn`(`Assets/Scripts/Editor/AutoHandRigFactory.cs`) 안에 이 로직이 있어 6개 게임 전부 자동으로 고쳐진다. **`[RequireComponent(typeof(Rigidbody))]`가 붙은 로코모션(`SinglePlayerGorillaLocomotion`, `SwordGorillaLocomotion`, `MagneticLocomotion`, `CrashHandler`, `PlungerLocomotion`)이나 `FixedJoint` 같은 Joint 계열을 새로 붙일 땐 반환된 래퍼가 아니라 `autoHandPlayer.gameObject`에 붙여야 한다** — 안 그러면 래퍼에 새 빈 Rigidbody가 따로 생겨 그 로코모션이 실제 리그와 완전히 분리된, 아무 효과 없는 상태가 된다. `.layer = IgnoreRaycastLayer`처럼 몸통 콜라이더 하나만 겨냥하는 코드도 마찬가지로 `autoHandPlayer.gameObject`를 대상으로 해야 한다(래퍼에 걸면 실제 콜라이더엔 적용 안 됨).

## 수정 시 주의
- 이 게임의 플레이어 이동을 바꾸려면 `AutoHandPlayer`(Auto Hand 소스, `Assets/AutoHand/Scripts/Movement/AutoHandPlayer.cs`) 쪽을 건드리는 게 아니라 `HoseRecoilLocomotion`의 `recoilForce`/`recoilForceMode`를 튜닝하는 것이 먼저다 — 이동 로직 자체는 Auto Hand가 소유한다.
- 다른 게임에 Auto Hand를 붙일 때(예: 핸드오프 문서에 있던 Tower의 VRHandController → Auto Hand 교체) 이 세션이 검증한 API(`Hand.IsSqueezing/GetSqueezeAxis`, `Grabbable.OnGrabEvent/OnReleaseEvent/OnSqueezeEvent`, `AutoHandPlayer.Instance/handLeft/handRight/headCamera`, `body`는 런타임 전용)를 재검증 없이 그대로 신뢰해도 된다.
- `Tools/HoseCleaner/Build Game`을 아직 Unity 에디터에서 실제로 실행/컴파일 확인 안 함 — 다음 세션이 제일 먼저 할 일.
