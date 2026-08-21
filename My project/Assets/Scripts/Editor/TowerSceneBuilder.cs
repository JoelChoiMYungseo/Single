#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using VRGame.Core;
using VRGame.Locomotion;
using VRGame.Survivor;
using VRGame.Tower;

/// <summary>"Crumbling Tower" 씬을 통째로 구성한다. 씬 파일이 없으면 새로 만든다.</summary>
public static class TowerSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/CrumblingTower.unity";
    private const string PrefabFolder = "Assets/Prefabs/Tower";
    private const string MaterialFolder = "Assets/Materials/Tower";
    private const string BlockPrefabPath = PrefabFolder + "/UnstableBlock.prefab";

    /// <summary>플레이어 리그를 Ignore Raycast(내장 레이어 2)에 두고 잡기 판정에서 제외한다 —
    /// 안 그러면 손이 자기 몸이나 다른 손을 붙잡는다.</summary>
    private const int IgnoreRaycastLayer = 2;
    private static int GrabbableMask => ~(1 << IgnoreRaycastLayer);

    /// <summary>Quest 2/3 기준 동시 시뮬레이션 블록 상한. 근거는 로그에 남긴다.</summary>
    private const int SimulatedBlockBudget = 64;

    [MenuItem("Tools/Tower/Build Crumbling Tower Scene")]
    public static void BuildScene()
    {
        MakeAssetsUtil.EnsureFolder(PrefabFolder);
        MakeAssetsUtil.EnsureFolder(MaterialFolder);

        if (SceneManager.GetActiveScene().isDirty)
        {
            EditorSceneManager.SaveOpenScenes();
        }

        Scene scene = OpenOrCreateScene();

        // 프리팹 생성은 임시 오브젝트를 만들므로 반드시 대상 씬을 연 뒤에 한다.
        GameObject blockPrefab = BuildBlockPrefab();

        RemoveDefaultCamera();
        BuildEnvironment();

        GameObject rig = BuildPlayerRig(out PlayerRig playerRig, out Transform head, out Transform startPoint,
            out VRHandController leftHand, out VRHandController rightHand);
        TowerHUD hud = BuildHud(head);
        BuildDirectors(rig, playerRig, startPoint, blockPrefab, leftHand, rightHand, hud);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Tower] 씬 구성 완료: {ScenePath} — 동시 시뮬레이션 블록 상한 {SimulatedBlockBudget}개. ▶ Play 하세요.");
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

    private static void RemoveDefaultCamera()
    {
        GameObject existing = GameObject.Find("Main Camera");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
    }

    // ────────────────────────────── Prefab ──────────────────────────────

    /// <summary>블록 하나. 콜라이더는 반드시 실체(비트리거)여야 한다 — 젠가처럼 서로 걸쳐 버티는
    /// 것이 이 게임의 물리 전부이기 때문이다.</summary>
    private static GameObject BuildBlockPrefab()
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.name = "UnstableBlock";
        root.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial("BlockMat", new Color(0.72f, 0.66f, 0.52f));

        var body = root.AddComponent<Rigidbody>();
        body.mass = 6f;
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        root.AddComponent<UnstableBlock>();

        return SavePrefab(root, BlockPrefabPath);
    }

    // ────────────────────────────── Environment ──────────────────────────────

    private static void BuildEnvironment()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -1f, 0f);
        ground.transform.localScale = new Vector3(40f, 2f, 40f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial("GroundMat", new Color(0.18f, 0.19f, 0.22f));

        // 타워 밑동을 받쳐 주는 단단한 대. 여기가 무너지면 게임이 시작조차 안 된다.
        var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pedestal.name = "Pedestal";
        pedestal.transform.position = new Vector3(0f, -0.15f, 0f);
        pedestal.transform.localScale = new Vector3(2.4f, 0.3f, 2.4f);
        pedestal.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial("PedestalMat", new Color(0.3f, 0.28f, 0.3f));

        if (GameObject.Find("Directional Light") == null)
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(48f, 32f, 0f);
        }
    }

    // ────────────────────────────── Player ──────────────────────────────

    private static GameObject BuildPlayerRig(out PlayerRig playerRig, out Transform head, out Transform startPoint,
        out VRHandController leftHand, out VRHandController rightHand)
    {
        var rig = new GameObject("PlayerRig");
        rig.transform.position = new Vector3(0f, 1.2f, 1.6f);

        Rigidbody playerBody = rig.AddComponent<Rigidbody>();
        playerBody.mass = 70f; // 사람 무게. 이 값이 곧 블록에 실리는 하중의 기준이다.
        playerBody.useGravity = true;
        playerBody.constraints = RigidbodyConstraints.FreezeRotation;
        playerBody.interpolation = RigidbodyInterpolation.Interpolate;
        playerBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var bodyCollider = rig.AddComponent<CapsuleCollider>();
        bodyCollider.radius = 0.25f;
        bodyCollider.height = 1.6f;
        bodyCollider.center = new Vector3(0f, 0.8f, 0f);

        head = BuildHead(rig.transform);
        Transform leftTarget = BuildHandTarget(rig.transform, "LeftHandTarget", new Vector3(-0.26f, 1.35f, 0.4f), XRNode.LeftHand);
        Transform rightTarget = BuildHandTarget(rig.transform, "RightHandTarget", new Vector3(0.26f, 1.35f, 0.4f), XRNode.RightHand);

        leftHand = BuildPhysicsHand(rig.transform, "LeftPhysicsHand", leftTarget, playerBody, XRNode.LeftHand);
        rightHand = BuildPhysicsHand(rig.transform, "RightPhysicsHand", rightTarget, playerBody, XRNode.RightHand);

        BuildRigModes(rig, head, leftTarget, rightTarget, leftHand, rightHand);

        // 게임 시스템이 참조할 유일한 플레이어 창구.
        playerRig = rig.AddComponent<PlayerRig>();
        var rigSo = new SerializedObject(playerRig);
        MakeAssetsUtil.SetRef(rigSo, "root", rig.transform);
        MakeAssetsUtil.SetRef(rigSo, "head", head);
        MakeAssetsUtil.SetRef(rigSo, "leftHand", leftTarget);
        MakeAssetsUtil.SetRef(rigSo, "rightHand", rightTarget);
        MakeAssetsUtil.SetRef(rigSo, "body", playerBody);
        rigSo.ApplyModifiedPropertiesWithoutUndo();

        // 타워는 물리 손이 곧 로코모션이라 별도 드라이버 컴포넌트가 없다. 리그 이동을 밖에서
        // 관측해 속도를 보고하게 한다 — 나중에 외부 로코모션 패키지를 얹어도 이 경로가 그대로 쓰인다.
        var observer = rig.AddComponent<ObservedLocomotionDriver>();
        var observerSo = new SerializedObject(observer);
        MakeAssetsUtil.SetRef(observerSo, "observed", rig.transform);
        MakeAssetsUtil.SetRef(observerSo, "observedBody", playerBody);
        observerSo.ApplyModifiedPropertiesWithoutUndo();

        // 리그 전체를 잡기 판정에서 제외한다. 자식을 다 만든 뒤에 적용해야 한다.
        SetLayerRecursively(rig, IgnoreRaycastLayer);

        var startGo = new GameObject("StartPoint");
        startGo.transform.position = rig.transform.position;
        startPoint = startGo.transform;

        return rig;
    }

    private static Transform BuildHead(Transform parent)
    {
        var head = new GameObject("Head") { tag = "MainCamera" };
        head.transform.SetParent(parent, false);
        head.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        var camera = head.AddComponent<Camera>();
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 300f;
        head.AddComponent<AudioListener>();

        var tracker = head.AddComponent<XRNodeTracker>();
        SetEnum(new SerializedObject(tracker), "node", (int)XRNode.Head);
        return head.transform;
    }

    /// <summary>추적된 컨트롤러 포즈만 담는 빈 트랜스폼. 물리 손이 스프링으로 이 지점을 따라간다.</summary>
    private static Transform BuildHandTarget(Transform parent, string name, Vector3 localPosition, XRNode node)
    {
        var target = new GameObject(name);
        target.transform.SetParent(parent, false);
        target.transform.localPosition = localPosition;

        var tracker = target.AddComponent<XRNodeTracker>();
        SetEnum(new SerializedObject(tracker), "node", (int)node);
        return target.transform;
    }

    /// <summary>실제로 충돌하는 손. 리그의 자식이지만 위치는 물리로만 움직인다 — 부모 트랜스폼에
    /// 끌려가지 않도록 조인트가 몸통과의 관계를 담당한다.</summary>
    private static VRHandController BuildPhysicsHand(Transform parent, string name, Transform target,
        Rigidbody playerBody, XRNode node)
    {
        var hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hand.name = name;
        hand.transform.SetParent(parent, false);
        hand.transform.position = target.position;
        hand.transform.localScale = Vector3.one * 0.11f;
        hand.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial("HandMat", new Color(0.9f, 0.75f, 0.6f));

        var handBody = hand.AddComponent<Rigidbody>();
        handBody.useGravity = false;

        var controller = hand.AddComponent<VRHandController>();
        var so = new SerializedObject(controller);
        MakeAssetsUtil.SetRef(so, "trackedTarget", target);
        MakeAssetsUtil.SetRef(so, "playerBody", playerBody);
        SetEnum(so, "handNode", (int)node);
        SetLayerMask(so, "grabbableMask", GrabbableMask);
        so.ApplyModifiedPropertiesWithoutUndo();

        return controller;
    }

    private static void BuildRigModes(GameObject rig, Transform head, Transform leftTarget, Transform rightTarget,
        VRHandController leftHand, VRHandController rightHand)
    {
        var placeholder = rig.AddComponent<PlaceholderClimbRig>();
        var placeholderSo = new SerializedObject(placeholder);
        MakeAssetsUtil.SetRef(placeholderSo, "head", head);
        MakeAssetsUtil.SetRef(placeholderSo, "leftHandTarget", leftTarget);
        MakeAssetsUtil.SetRef(placeholderSo, "rightHandTarget", rightTarget);
        MakeAssetsUtil.SetRef(placeholderSo, "leftHand", leftHand);
        MakeAssetsUtil.SetRef(placeholderSo, "rightHand", rightHand);
        placeholderSo.ApplyModifiedPropertiesWithoutUndo();

        // 임시 모드에서는 손 목표를 이 스크립트가 옮기므로 XR 추적을 꺼야 한다.
        var trackers = rig.GetComponentsInChildren<XRNodeTracker>(true);

        var switcher = rig.AddComponent<XRModeSwitcher>();
        var switcherSo = new SerializedObject(switcher);
        SetObjectArray(switcherSo, "vrOnly", trackers);
        SetObjectArray(switcherSo, "placeholderOnly", new Object[] { placeholder });
        switcherSo.ApplyModifiedPropertiesWithoutUndo();

        // 그립 입력 출처는 PlaceholderClimbRig가 자기 OnEnable/OnDisable에서 전환하므로
        // 여기서 따로 설정할 것이 없다 — 모드와 입력 경로가 항상 함께 움직인다.
    }

    // ────────────────────────────── HUD ──────────────────────────────

    private static TowerHUD BuildHud(Transform head)
    {
        var canvasGo = new GameObject("TowerHUD", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(head, false);
        canvasGo.transform.localPosition = new Vector3(0f, -0.3f, 1.25f);
        canvasGo.transform.localScale = Vector3.one * 0.0015f;

        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1000f, 560f);

        canvasGo.AddComponent<RuntimeKoreanFont>(); // 내장 폰트에 한글 글리프가 없다

        var hud = canvasGo.AddComponent<TowerHUD>();

        Text height = CreateText(rect, "HeightLabel", "0.0 m", 60, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(28f, -18f));
        Text best = CreateText(rect, "BestLabel", "최고 0.0 m", 34, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(28f, -92f));
        Text state = CreateText(rect, "StateLabel", string.Empty, 54, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 24f));
        Text budget = CreateText(rect, "BudgetLabel", "물리 블록 0/0", 26, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-28f, -18f));

        var so = new SerializedObject(hud);
        MakeAssetsUtil.SetRef(so, "heightLabel", height);
        MakeAssetsUtil.SetRef(so, "bestLabel", best);
        MakeAssetsUtil.SetRef(so, "stateLabel", state);
        MakeAssetsUtil.SetRef(so, "budgetLabel", budget);
        so.ApplyModifiedPropertiesWithoutUndo();

        return hud;
    }

    private static Text CreateText(Transform parent, string name, string content, int fontSize,
        TextAnchor alignment, Vector2 anchor, Vector2 anchoredPosition)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(520f, 80f);

        var text = go.GetComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return text;
    }

    // ────────────────────────────── Directors ──────────────────────────────

    private static void BuildDirectors(GameObject rig, PlayerRig playerRig, Transform startPoint,
        GameObject blockPrefab, VRHandController leftHand, VRHandController rightHand, TowerHUD hud)
    {
        var directors = new GameObject("TowerDirectors");
        var handArray = new Object[] { leftHand, rightHand };

        var tower = directors.AddComponent<TowerManager>();
        var towerSo = new SerializedObject(tower);
        MakeAssetsUtil.SetRef(towerSo, "blockPrefab", blockPrefab.GetComponent<UnstableBlock>());
        MakeAssetsUtil.SetRef(towerSo, "player", rig.transform);
        SetInt(towerSo, "maxSimulatedBlocks", SimulatedBlockBudget);
        towerSo.ApplyModifiedPropertiesWithoutUndo();

        var fall = directors.AddComponent<FallManager>();
        var fallSo = new SerializedObject(fall);
        MakeAssetsUtil.SetRef(fallSo, "player", playerRig);
        SetObjectArray(fallSo, "hands", handArray);
        fallSo.ApplyModifiedPropertiesWithoutUndo();

        var run = directors.AddComponent<TowerRunManager>();
        var runSo = new SerializedObject(run);
        MakeAssetsUtil.SetRef(runSo, "player", playerRig);
        MakeAssetsUtil.SetRef(runSo, "startPoint", startPoint);
        MakeAssetsUtil.SetRef(runSo, "tower", tower);
        MakeAssetsUtil.SetRef(runSo, "fallManager", fall);
        SetObjectArray(runSo, "hands", handArray);
        MakeAssetsUtil.SetRef(runSo, "hud", hud);
        SetInt(runSo, "simulatedBudgetForDisplay", SimulatedBlockBudget);
        runSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // ────────────────────────────── Helpers ──────────────────────────────

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static void SetObjectArray(SerializedObject so, string field, Object[] values)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogError($"[Tower] 필드 없음: {field}");
            return;
        }

        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void SetLayerMask(SerializedObject so, string field, int mask)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogError($"[Tower] 필드 없음: {field}");
            return;
        }
        prop.intValue = mask;
    }

    private static void SetInt(SerializedObject so, string field, int value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogError($"[Tower] 필드 없음: {field}");
            return;
        }
        prop.intValue = value;
    }

    private static void SetEnum(SerializedObject so, string field, int value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogError($"[Tower] 필드 없음: {field}");
            return;
        }
        prop.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material MakeMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.color = color;
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        var material = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject SavePrefab(GameObject root, string path)
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }
}
#endif
