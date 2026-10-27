#if UNITY_EDITOR
using System.IO;
using Autohand;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRGame.Cart;
using VRGame.Core;

/// <summary>"Shopping Cart Downhill Panic"을 GAME8 씬에 통째로 구성한다. 고정된 긴 경사 슬래브
/// 위에 카트를 놓고, 그 위에 재활용되는 장애물을 얹은 뒤, 플레이어를 Auto Hand 리그로 스폰해
/// FixedJoint로 카트에 "태운다". HUD·점수 시스템은 이번 스코프에 없다(설계 문서
/// .claude/design/cart-downhill-panic 참고). 이 프로토타입은 다른 6개 게임과 달리 로코모션
/// 드라이버(ILocomotionDriver)가 필요 없다 — 카트가 조인트로 플레이어 Rigidbody를 직접 끌고
/// 가므로 PlayerRig.Velocity(= body.linearVelocity)가 이미 정확하다.</summary>
public static class CartSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/GAME8 cart.unity";
    private const string PrefabFolder = "Assets/Prefabs/Cart";
    private const string MaterialFolder = "Assets/Materials/Cart";

    private const float SlopeDegrees = 10f;
    private const float FloorSegmentLength = 20f;
    private const int FloorSegmentCount = 20; // 20 * 20m = 400m — 한 플레이 세션 동안은 사실상 무한하게 느껴지는 길이.
    private const float FloorWidth = 16f;
    private const float FloorThickness = 2f;

    [MenuItem("Tools/Cart/Build Game")]
    public static void BuildGame()
    {
        MakeAssetsUtil.EnsureFolder(PrefabFolder);
        MakeAssetsUtil.EnsureFolder(MaterialFolder);

        int rampLayer = MakeAssetsUtil.ResolveLayer("Cart", "Ramp");
        float slopeYPerZ = -Mathf.Tan(SlopeDegrees * Mathf.Deg2Rad);

        if (SceneManager.GetActiveScene().isDirty)
        {
            EditorSceneManager.SaveOpenScenes();
        }
        Scene scene = OpenOrCreateScene();

        MakeAssetsUtil.RemoveDefaultCamera();
        MakeAssetsUtil.ClearPreviousBuild("Slope", "Cart", "Player", "XRPlayer", "RampSpawner");

        BuildSlope(rampLayer, slopeYPerZ);
        GameObject obstaclePrefab = BuildObstaclePrefab();

        GameObject cart = BuildCart(rampLayer, out DownhillCart downhillCart, out CartBailout bailout,
            out Rigidbody cartBody);
        BuildRampSpawner(cart.transform, obstaclePrefab, slopeYPerZ);

        Vector3 seatPosition = cart.transform.position + Vector3.up * 1.1f;
        GameObject player = AutoHandRigFactory.Spawn("Cart", "Player", seatPosition,
            disableBuiltinMovement: true, out AutoHandPlayer autoHandPlayer, out Rigidbody playerBody);

        // root는 래퍼(player)가 아니라 autoHandPlayer.gameObject다 — 카트 조인트로 실제 물리를
        // 받는 게 그 오브젝트라, 래퍼를 넣으면 Position/Teleport가 스폰 위치에 얼어붙는다.
        var playerRig = player.AddComponent<PlayerRig>();
        var rigSo = new SerializedObject(playerRig);
        MakeAssetsUtil.SetRef(rigSo, "root", autoHandPlayer.transform);
        MakeAssetsUtil.SetRef(rigSo, "head", autoHandPlayer.headCamera.transform);
        MakeAssetsUtil.SetRef(rigSo, "leftHand", autoHandPlayer.handLeft.transform);
        MakeAssetsUtil.SetRef(rigSo, "rightHand", autoHandPlayer.handRight.transform);
        MakeAssetsUtil.SetRef(rigSo, "body", playerBody);
        rigSo.ApplyModifiedPropertiesWithoutUndo();

        // 카트↔플레이어 "탑승" — Transform 부모/자식이 아니라 물리 조인트로 묶는다(설계 문서 참고).
        // 이러면 PlayerRig.Body는 항상 플레이어 자신의 Rigidbody를 가리키는 기존 6개 게임의 규약이
        // 그대로 유지된다. 방출은 CartBailout이 이 조인트를 파괴하는 것으로 대체한다.
        // Joint는 반드시 그 Rigidbody와 같은 GameObject에 있어야 한다 — playerBody는 래퍼(player)가
        // 아니라 autoHandPlayer.gameObject에 있으므로 거기 붙인다.
        var seatJoint = autoHandPlayer.gameObject.AddComponent<FixedJoint>();
        seatJoint.connectedBody = cartBody;

        var bailoutSo = new SerializedObject(bailout);
        MakeAssetsUtil.SetRef(bailoutSo, "seatJoint", seatJoint);
        MakeAssetsUtil.SetRef(bailoutSo, "playerBody", playerBody);
        bailoutSo.ApplyModifiedPropertiesWithoutUndo();

        BuildSteeringStick(rampLayer, downhillCart, autoHandPlayer.handRight.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Cart] GAME8 씬 구성 완료. Ramp 레이어={rampLayer}. 카트/막대 위치는 근사치라 " +
                  "Play 후 어긋나 보이면 직접 눈대중으로 조정하세요. ▶ Play 하세요.");
    }

    private static Scene OpenOrCreateScene()
    {
        if (File.Exists(ScenePath))
        {
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Scene created = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(created, ScenePath);
        return created;
    }

    // ────────────────────────────── Slope ──────────────────────────────

    /// <summary>고정된 세그먼트 20개를 이어붙여 400m짜리 긴 내리막을 만든다 — 진짜 절차적 무한
    /// 지형이 아니라(설계 문서의 스코프 축소 결정 참고), 장애물만 DownhillRampSpawner로 재활용한다.</summary>
    private static void BuildSlope(int rampLayer, float slopeYPerZ)
    {
        var slopeRoot = new GameObject("Slope");
        Material slopeMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "SlopeMat", new Color(0.32f, 0.3f, 0.34f));

        for (int i = 0; i < FloorSegmentCount; i++)
        {
            float centerZ = i * FloorSegmentLength + FloorSegmentLength * 0.5f;
            float centerY = centerZ * slopeYPerZ;

            var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = $"SlopeSegment_{i}";
            segment.layer = rampLayer;
            segment.transform.SetParent(slopeRoot.transform, false);
            segment.transform.position = new Vector3(0f, centerY, centerZ);
            segment.transform.eulerAngles = new Vector3(SlopeDegrees, 0f, 0f);
            segment.transform.localScale = new Vector3(FloorWidth, FloorThickness, FloorSegmentLength);
            segment.GetComponent<MeshRenderer>().sharedMaterial = slopeMaterial;
        }
    }

    private static GameObject BuildObstaclePrefab()
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.name = "CartObstacle";
        root.transform.localScale = new Vector3(2f, 2f, 2f);
        root.GetComponent<MeshRenderer>().sharedMaterial =
            MakeAssetsUtil.MakeMaterial(MaterialFolder, "ObstacleMat", new Color(0.6f, 0.15f, 0.1f));

        return MakeAssetsUtil.SavePrefab(root, $"{PrefabFolder}/CartObstacle.prefab");
    }

    private static void BuildRampSpawner(Transform tracked, GameObject obstaclePrefab, float slopeYPerZ)
    {
        var spawnerGo = new GameObject("RampSpawner");
        var spawner = spawnerGo.AddComponent<DownhillRampSpawner>();

        var so = new SerializedObject(spawner);
        MakeAssetsUtil.SetRef(so, "tracked", tracked);
        MakeAssetsUtil.SetObjectArray(so, "segmentPrefabs", new Object[] { obstaclePrefab });
        MakeAssetsUtil.SetFloat(so, "slopeYPerZ", slopeYPerZ);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ────────────────────────────── Cart ──────────────────────────────

    private static GameObject BuildCart(int rampLayer, out DownhillCart downhillCart, out CartBailout bailout,
        out Rigidbody cartBody)
    {
        var cart = new GameObject("Cart", typeof(Rigidbody));
        cart.transform.position = new Vector3(0f, 0.9f, 0f);

        cartBody = cart.GetComponent<Rigidbody>();
        cartBody.mass = 40f; // 카트 + 짐 정도의 무게.

        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(cart.transform, false);
        visual.transform.localScale = new Vector3(1.2f, 0.9f, 1.6f);
        visual.GetComponent<MeshRenderer>().sharedMaterial =
            MakeAssetsUtil.MakeMaterial(MaterialFolder, "CartMat", new Color(0.55f, 0.55f, 0.6f));
        // 카트 몸통 콜라이더는 시각화 큐브의 것을 그대로 쓴다(BoxCollider 기본 생성분) — 충돌 감지는
        // 이 콜라이더로 충분하다.

        var wheelPoints = new Transform[4];
        wheelPoints[0] = CreateWheelPoint(cart.transform, "WheelFL", new Vector3(-0.55f, -0.45f, 0.7f));
        wheelPoints[1] = CreateWheelPoint(cart.transform, "WheelFR", new Vector3(0.55f, -0.45f, 0.7f));
        wheelPoints[2] = CreateWheelPoint(cart.transform, "WheelRL", new Vector3(-0.55f, -0.45f, -0.7f));
        wheelPoints[3] = CreateWheelPoint(cart.transform, "WheelRR", new Vector3(0.55f, -0.45f, -0.7f));

        downhillCart = cart.AddComponent<DownhillCart>();
        var cartSo = new SerializedObject(downhillCart);
        MakeAssetsUtil.SetObjectArray(cartSo, "wheelPoints", wheelPoints);
        MakeAssetsUtil.SetLayerMask(cartSo, "groundMask", 1 << rampLayer);
        cartSo.ApplyModifiedPropertiesWithoutUndo();

        bailout = cart.AddComponent<CartBailout>();

        return cart;
    }

    private static Transform CreateWheelPoint(Transform parent, string name, Vector3 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }

    // ────────────────────────────── Steering Stick ──────────────────────────────

    /// <summary>2026-09-17 세션부터 막대는 손에 고정 부착되지 않고 실제 Grabbable이다 — HoseCleaner의
    /// 호스, Samurai의 카타나와 동일한 관례. 플레이어 손 근처에 세워 두고 직접 쥐게 한다. 쥐고 있는
    /// 동안만 tip이 손을 따라 움직이므로 StickSteering도 막대를 쥐고 있을 때만 조향한다.
    /// StickSteering 자체는 막대가 아니라 손(handParent)에 둔다 — stickTip 참조만 있으면 되고,
    /// 막대를 놓쳤다가 다시 주워도 재배선할 필요가 없다.</summary>
    private static void BuildSteeringStick(int rampLayer, DownhillCart downhillCart, Transform handParent)
    {
        var stick = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        stick.name = "SteeringStick";
        stick.transform.position = handParent.position + handParent.forward * 0.3f + Vector3.down * 0.3f;
        stick.transform.localScale = new Vector3(0.05f, 0.6f, 0.05f);
        stick.GetComponent<MeshRenderer>().sharedMaterial =
            MakeAssetsUtil.MakeMaterial(MaterialFolder, "StickMat", new Color(0.4f, 0.28f, 0.15f));

        // 기본 CapsuleCollider(파괴 안 함)가 그립 콜라이더로 쓰인다.
        var body = stick.AddComponent<Rigidbody>();
        body.mass = 0.5f;
        stick.AddComponent<Grabbable>(); // handType 기본값(both)이면 양손 다 쥘 수 있어 그대로 둔다.

        var tipGo = new GameObject("Tip");
        tipGo.transform.SetParent(stick.transform, false);
        tipGo.transform.localPosition = new Vector3(0f, -1f, 0f); // 캡슐 로컬 스케일 기준 아래쪽 끝.

        var steering = handParent.gameObject.AddComponent<StickSteering>();
        var so = new SerializedObject(steering);
        MakeAssetsUtil.SetRef(so, "cart", downhillCart);
        MakeAssetsUtil.SetRef(so, "stickTip", tipGo.transform);
        MakeAssetsUtil.SetLayerMask(so, "groundMask", 1 << rampLayer);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
