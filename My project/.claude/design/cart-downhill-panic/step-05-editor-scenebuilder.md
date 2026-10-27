# Step 05: CartSceneBuilder — Tools/Cart/Build Game

- **영역:** `editor` (`Assets/Scripts/Editor/`)
- **선행 단계:** step-01, 02, 03, 04 전부 완료 필요
- **후행 단계:** 없음 (이 계획의 마지막 단계) — 완료되면 Unity에서 `Tools/Cart/Build Game` → Play로 실제 검증

---

## 목적
카트 프리팹(바퀴 4점 + `DownhillCart` + `CartBailout`)과 램프 세그먼트 프리팹 여러 종, `DownhillRampSpawner`, AutoHand 플레이어 리그를 조립하고, 카트↔플레이어를 묶는 `Joint`를 만들어 `CartBailout.seatJoint`/`playerBody`에 연결한다.

---

## 에이전트 실행 지침

`/task-start`를 먼저 호출해 범위를 확정한 뒤, 아래 지시를 수행한다.

### 생성 파일
- `Assets/Scripts/Editor/CartSceneBuilder.cs` — 신규
- 씬 파일 `Assets/Scenes/GAME8 cart.unity` — 없으면 새로 만든다. [TowerSceneBuilder.OpenOrCreateScene()](../../../Assets/Scripts/Editor/TowerSceneBuilder.cs#L63)를 그대로 참고해 동일한 헬퍼를 작성한다 (`File.Exists` → 있으면 `OpenScene`, 없으면 `EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single)` 후 `SaveScene(created, ScenePath)`).

### 핵심 심볼
```csharp
public static class CartSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/GAME8 cart.unity";
    private const string PrefabFolder = "Assets/Prefabs/Cart";
    private const string MaterialFolder = "Assets/Materials/Cart";

    [MenuItem("Tools/Cart/Build Game")]
    public static void BuildGame();
}
```

### 필수 처리 순서 (다른 빌더들과 동일한 관례)
1. `MakeAssetsUtil.EnsureFolder(PrefabFolder)` / `EnsureFolder(MaterialFolder)`
2. 대상 씬을 명시적으로 연다/만든다 (위 `OpenOrCreateScene` 패턴).
3. `MakeAssetsUtil.RemoveDefaultCamera()`
4. **카트 조립**:
   ```csharp
   var cartGo = new GameObject("Cart", typeof(Rigidbody));
   var cartBody = cartGo.GetComponent<Rigidbody>();
   var cart = cartGo.AddComponent<DownhillCart>();
   // SetRef: wheelPoints(4개 자식 Transform 새로 생성), groundMask = "Ramp" 레이어 등
   var bailout = cartGo.AddComponent<CartBailout>(); // [RequireComponent(typeof(DownhillCart))]라 자동으로 같이 붙어도 되지만 명시적으로 순서 보장
   ```
5. **"Ramp" 레이어 확인**: `int rampLayer = LayerMask.NameToLayer("Ramp");`가 `-1`이면 `Debug.LogError`로 "Project Settings > Tags and Layers에서 'Ramp' 레이어를 먼저 추가하세요" 남기고 중단 — [MagneticSceneBuilder.ResolveMetalLayer](../../../Assets/Scripts/Editor/MagneticSceneBuilder.cs#L62)와 동일 관례.
6. **플레이어 리그**:
   ```csharp
   GameObject player = AutoHandRigFactory.Spawn("Cart", "Player", seatPosition,
       disableBuiltinMovement: true, out AutoHandPlayer autoHandPlayer, out Rigidbody playerBody);
   var playerRig = player.AddComponent<PlayerRig>();
   // SetRef: root/head/leftHand/rightHand/body = playerBody (기존 6개 게임과 동일)
   ```
7. **좌석 Joint 연결** (README 아키텍처 결정 — Transform 부모/자식이 아니라 물리 조인트):
   ```csharp
   var seatJoint = player.AddComponent<FixedJoint>(); // 또는 ConfigurableJoint 전축 고정 — 필요 시 진동 완화를 위해 후자 선택
   seatJoint.connectedBody = cartBody;
   seatJoint.autoConfigureConnectedAnchor = true;
   ```
   `CartBailout`의 `seatJoint`/`playerBody` 필드에 `SerializedObject`로 연결한다.
8. **막대 조향**: 플레이어 손(오른손 또는 왼손, `autoHandPlayer.handRight`) 자식으로 막대 오브젝트(콜라이더 포함, tip 위치)를 만들고 `StickSteering`을 플레이어 리그(또는 손 오브젝트)에 붙여 `cart`/`stickTip` 참조를 연결한다.
9. **램프 스포너**: `DownhillRampSpawner`를 씬 매니저 오브젝트에 붙이고 `tracked = cartGo.transform`, `segmentPrefabs`는 이번 스텝에서 만들 1~2종의 간단한 경사+장애물 프리팹으로 채운다 (경사면은 회전된 Plane/Cube, 장애물은 `Ramp` 레이어가 아닌 별도 충돌용 큐브 몇 개 — `CartBailout`이 감지할 정면 충돌 대상).
10. `Debug.Log($"[Cart] 구성 완료. ▶ Play 하세요.")`, `EditorSceneManager.MarkSceneDirty` + `SaveScene` + `AssetDatabase.SaveAssets()`.

### 선행 산출물 의존성
- `AutoHandRigFactory.Spawn` — 기존 (`Assets/Scripts/Editor/AutoHandRigFactory.cs`)
- `DownhillCart`, `DownhillRampSpawner`, `StickSteering`, `CartBailout` — step-01~04
- `MakeAssetsUtil`, `PlayerRig` — 기존 공용 코드, 수정 없이 그대로 사용

### 완료 판정
- [ ] `Grep "Tools/Cart" Assets/Scripts/Editor/` 로 메뉴 아이템 등록 확인
- [ ] Unity 컴파일 통과
- [ ] `Tools/Cart/Build Game` 실행 시 콘솔 에러 없음
- [ ] Play 모드(VR 헤드셋 연결 시)에서 카트가 경사를 따라 굴러 내려가고, 막대로 바닥을 찍으면 방향이 바뀌고, 장애물에 정면으로 박으면 튕겨 나가는지 실제 확인 (에이전트가 헤드셋으로 직접 확인할 수 없으므로 사용자 확인 필요로 보고에 남긴다)

---

## 금지 사항
- 다른 7개 게임(기존 6개 + Plunger 설계)의 SceneBuilder 파일을 수정하지 않는다.
- 카트/도구 스킨, 수익화 UI를 추가하지 않는다 (README의 스코프 결정 참고).
- `.prefab`/`.unity`/`.asset` 파일을 텍스트로 직접 편집하지 않는다 — Unity가 Editor 스크립트를 통해 생성하게 한다.
