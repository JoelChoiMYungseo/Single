#if UNITY_EDITOR
using Autohand;
using GorillaLocomotion;
using UnityEditor;
using UnityEngine;
using VRGame.Core;

/// <summary>6개 SceneBuilder(Echo/Magnetic/Samurai/Survivor/Tower/HoseCleaner)가 각자 복사해 갖고
/// 있던 "Auto Hand XRPlayer 프리팹 스폰" 블록을 승격한 것. Auto Hand Examples가 미리 구성해 둔 리그
/// (헤드 카메라·양손 Hand·AutoHandPlayer)를 그대로 가져다 쓰고, 그 위에 이 프로젝트의 PlayerRig/
/// ILocomotionDriver 계약을 얹을 수 있게 AutoHandPlayer와 Rigidbody 참조를 돌려준다.
///
/// <b>중요 — AutoHandPlayer는 인스턴스화한 루트(반환값 GameObject) 위에 있지 않다.</b> XRPlayer
/// 프리팹의 최상위 오브젝트는 XRHandOffset 두 개만 들고 있는 래퍼고, AutoHandPlayer·그 Rigidbody·
/// CapsuleCollider는 그 밑에 중첩된 프리팹 인스턴스(자식) 쪽에 있다. 2026-09-17 세션에서 Unity를
/// 처음 실제로 켜서 Play해보고서야 발견됨 — 그전까지 6개 게임의 SceneBuilder 전부(리팩토링 이전
/// 원본 코드 포함) <c>rig.GetComponent&lt;AutoHandPlayer&gt;()</c>로 루트에서 바로 찾으려 했는데,
/// 한 번도 컴파일 후 실제로 Play해본 적이 없어서 이 버그가 안 걸렸다. 그래서 여기서는 반드시
/// <c>GetComponentInChildren</c>로 찾고, Rigidbody도 래퍼가 아니라 AutoHandPlayer 자신의
/// GameObject에서 가져온다. <b>[RequireComponent(typeof(Rigidbody))]가 붙은 로코모션 스크립트
/// (SinglePlayerGorillaLocomotion, SwordGorillaLocomotion, MagneticLocomotion, CrashHandler,
/// PlungerLocomotion 등)와 FixedJoint 같은 Joint 계열은 반환된 래퍼 GameObject가 아니라
/// <c>autoHandPlayer.gameObject</c>에 붙여야 한다</b> — 안 그러면 래퍼에 새 빈 Rigidbody가 따로
/// 생겨서 AutoHandPlayer가 실제로 움직이는 물리 바디와 완전히 분리된, 아무 효과 없는 로코모션이
/// 된다.</summary>
public static class AutoHandRigFactory
{
    public const string XRPlayerPrefabPath = "Assets/AutoHand/Examples/Scenes/XR/Prefabs/XRPlayer.prefab";

    /// <summary>프리팹을 인스턴스화해 이름·위치를 잡고 AutoHandPlayer/Rigidbody를 돌려준다.
    /// <paramref name="disableBuiltinMovement"/>는 이 리그 위에 독자적인 물리 로코모션(고릴라 스윙,
    /// 호스 반동, 자력 반동 등)을 얹을 게임에서 true로 준다 — Auto Hand의 기본 스틱 이동을 안 끄면
    /// 매 FixedUpdate마다 velocity가 스냅되어 그 반동을 그 즉시 지운다. Echo처럼 Auto Hand의 기본
    /// 이동을 그대로 로코모션으로 쓰는 게임만 false를 준다.
    /// 반환값(래퍼 루트)은 이름표·전체 배치·레이어 세팅용이고, 실제 물리·로코모션 컴포넌트는
    /// <paramref name="autoHandPlayer"/><c>.gameObject</c>에 붙여야 한다 — 클래스 주석 참고.</summary>
    public static GameObject Spawn(string gameTag, string rigName, Vector3 position, bool disableBuiltinMovement,
        out AutoHandPlayer autoHandPlayer, out Rigidbody body)
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(XRPlayerPrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError($"[{gameTag}] Auto Hand 플레이어 프리팹을 찾을 수 없습니다: {XRPlayerPrefabPath}");
        }

        GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
        rig.name = rigName;
        rig.transform.position = position;

        // AutoHandPlayer는 루트가 아니라 중첩된 자식 프리팹 인스턴스 쪽에 있다 — 클래스 주석 참고.
        autoHandPlayer = rig.GetComponentInChildren<AutoHandPlayer>(true);
        if (autoHandPlayer == null)
        {
            Debug.LogError($"[{gameTag}] {rigName} 안에서 AutoHandPlayer를 찾을 수 없습니다 — " +
                            "XRPlayer 프리팹 구조가 바뀌었을 수 있습니다.");
            body = null;
            return rig;
        }

        if (disableBuiltinMovement)
        {
            autoHandPlayer.useMovement = false;
        }

        // AutoHandPlayer.body는 프로퍼티라 실제 값은 자신의 Awake()에서 GetComponent<Rigidbody>()로
        // 채워진다 — 에디터 빌드 타임(Play 이전)엔 아직 null이므로 직접 GetComponent로 가져온다.
        // 반드시 AutoHandPlayer 자신의 GameObject에서 가져와야 한다(래퍼 루트가 아님).
        body = autoHandPlayer.GetComponent<Rigidbody>();

        return rig;
    }

    /// <summary><see cref="Spawn"/> + 8개 게임 중 5개가 쓰는 "진짜 고릴라 태그" 로코모션 배선을 한
    /// 번에 처리한다. 2026-09-17 세션에서 우리가 직접 짠 근사치(<c>SinglePlayerGorillaLocomotion</c>,
    /// 스윙 순간 AddForce)를 걷어내고, 프로젝트에 이미 들어있던 Another Axiom(Gorilla Tag 제작사)의
    /// 공식 MIT 로코모션(<c>Assets/GorillaLocomotion-main/</c>, <see cref="Player"/>)으로 교체했다 —
    /// 사용자가 명시적으로 요청. 이 스크립트는 남의 코드라 손 안 대고 그대로 쓴다(RULE-03은 우리
    /// 코드가 대상이라 여기 적용 안 됨 — <c>ObservedLocomotionDriver</c>가 정확히 이 상황을 위한
    /// 어댑터). Samurai(카타나 변형)와 Plunger/Cart(로코모션 자체가 게임 컨셉)는 이 헬퍼를 쓰지
    /// 않고 <see cref="Spawn"/>을 직접 호출한다.</summary>
    public static GameObject SpawnWithGorillaLocomotion(string gameTag, string rigName, Vector3 position,
        out AutoHandPlayer autoHandPlayer, out Rigidbody body)
    {
        const int ignoreRaycastLayer = 2; // Unity 내장 "Ignore Raycast" 레이어.

        GameObject rig = Spawn(gameTag, rigName, position, disableBuiltinMovement: true, out autoHandPlayer, out body);
        if (autoHandPlayer == null)
        {
            return rig;
        }

        GameObject playerGo = autoHandPlayer.gameObject;
        body.constraints = RigidbodyConstraints.FreezeRotation;

        // 헤드 콜라이더 — Another Axiom 데모 프리팹(Gorilla Rig.prefab)의 실제 값을 그대로 옮겼다
        // (SphereCollider, radius 0.15, 헤드 카메라 오브젝트 자신에게).
        SphereCollider headCollider = autoHandPlayer.headCamera.gameObject.GetComponent<SphereCollider>();
        if (headCollider == null)
        {
            headCollider = autoHandPlayer.headCamera.gameObject.AddComponent<SphereCollider>();
        }
        headCollider.radius = 0.15f;
        headCollider.center = Vector3.zero;
        // 몸통과 마찬가지로 헤드 콜라이더도 Ignore Raycast — 안 그러면 손 스윙이 자기 머리를
        // "지형"으로 잡아버릴 수 있다.
        autoHandPlayer.headCamera.gameObject.layer = ignoreRaycastLayer;

        // 손이 "붙잡은 지점"을 표시하는 팔로워 — 원본은 여기에 시각적 손 모델을 달지만, 이번 통합은
        // 이동 로직만 가져온다. Auto Hand 자신의 손 모델은 실제 트래킹 위치를 그대로 렌더링한다.
        Transform leftFollower = new GameObject("GorillaLeftHandFollower").transform;
        leftFollower.SetParent(rig.transform, false);
        Transform rightFollower = new GameObject("GorillaRightHandFollower").transform;
        rightFollower.SetParent(rig.transform, false);

        // Player.Awake()가 이 필드들을 즉시 참조하는데(GetComponent<Rigidbody>는 상관없지만
        // leftHandFollower.transform.position 등은 아직 null이면 NRE) AddComponent는 오브젝트가
        // 비활성 상태면 Awake를 미룬다 — 그래서 꺼둔 채로 붙이고 다 배선한 뒤에 켠다.
        bool wasActive = playerGo.activeSelf;
        playerGo.SetActive(false);

        var player = playerGo.AddComponent<Player>();
        player.headCollider = headCollider;
        player.bodyCollider = playerGo.GetComponent<CapsuleCollider>();
        player.leftHandFollower = leftFollower;
        player.rightHandFollower = rightFollower;
        player.leftHandTransform = autoHandPlayer.handLeft.transform;
        player.rightHandTransform = autoHandPlayer.handRight.transform;
        // 아래 튜닝값은 Another Axiom의 실제 데모 프리팹(Gorilla Rig.prefab)에서 그대로 가져온
        // 검증된 값이다 — 특히 velocityHistorySize는 기본값이 0이라 그대로 두면 나눗셈 0 에러가 난다.
        player.velocityHistorySize = 8;
        player.maxArmLength = 1.5f;
        player.unStickDistance = 1f;
        player.velocityLimit = 0.4f;
        player.maxJumpSpeed = 6.5f;
        player.jumpMultiplier = 1.1f;
        player.minimumRaycastDistance = 0.05f;
        player.defaultSlideFactor = 0.03f;
        player.defaultPrecision = 0.995f;
        player.locomotionEnabledLayers = ~(1 << ignoreRaycastLayer);

        playerGo.SetActive(wasActive);

        // ILocomotionDriver를 모르는 외부 패키지라 관측 어댑터로 PlayerRig에 속도를 보고한다 —
        // HoseCleaner/Tower와 동일한 패턴.
        var observed = rig.AddComponent<ObservedLocomotionDriver>();
        var observedSo = new SerializedObject(observed);
        MakeAssetsUtil.SetRef(observedSo, "observedBody", body);
        observedSo.ApplyModifiedPropertiesWithoutUndo();

        // 몸통만 Ignore Raycast로 빼서 손 스윙 SphereCast가 자기 몸을 지형으로 잘못 감지하지 않게
        // 한다. 실제 CapsuleCollider는 래퍼(rig)가 아니라 autoHandPlayer.gameObject 쪽에 있다.
        playerGo.layer = ignoreRaycastLayer;

        return rig;
    }
}
#endif
