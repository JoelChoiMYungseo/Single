#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;
using VRGame.Locomotion;
using VRGame.Weapons;

public static class PlayerRigFactory
{
    public const string PrefabPath = "Assets/Prefabs/Player/PlayerRig.prefab";

    [MenuItem("Tools/Make Assets/Player Rig")]
    public static void Create() => CreateInternal(force: false);

    [MenuItem("Tools/Make Assets/Player Rig (Force Recreate)")]
    public static void ForceRecreate() => CreateInternal(force: true);

    public static void CreateInternal(bool force)
    {
        if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            Debug.LogWarning($"[MakeAssets] 이미 존재합니다: {PrefabPath}");
            return;
        }

        MakeAssetsUtil.EnsureFolder("Assets/Prefabs/Player");

        var root = new GameObject("PlayerRig");
        var locomotion = root.AddComponent<SinglePlayerGorillaLocomotion>(); // Rigidbody 자동 부착

        var bodyCollider = root.AddComponent<CapsuleCollider>();
        bodyCollider.radius = 0.3f;
        bodyCollider.height = 1.7f;
        bodyCollider.center = new Vector3(0f, 0.85f, 0f);

        var leftHand = new GameObject("LeftHand");
        leftHand.transform.SetParent(root.transform, false);
        leftHand.transform.localPosition = new Vector3(-0.3f, 1.1f, 0.2f);
        leftHand.AddComponent<XRNodeTracker>();
        SetXRNode(leftHand, XRNode.LeftHand);

        var rightHand = new GameObject("RightHand");
        rightHand.transform.SetParent(root.transform, false);
        rightHand.transform.localPosition = new Vector3(0.3f, 1.1f, 0.2f);
        rightHand.AddComponent<XRNodeTracker>();
        SetXRNode(rightHand, XRNode.RightHand);

        var gun = new GameObject("Gun");
        gun.transform.SetParent(rightHand.transform, false);
        var gunComponent = gun.AddComponent<OneHandedGun>();

        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(gun.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0f, 0.15f);

        var gunSo = new SerializedObject(gunComponent);
        MakeAssetsUtil.SetRef(gunSo, "muzzle", muzzle.transform);
        gunSo.ApplyModifiedPropertiesWithoutUndo();

        var locomotionSo = new SerializedObject(locomotion);
        MakeAssetsUtil.SetRef(locomotionSo, "leftHand", leftHand.transform);
        MakeAssetsUtil.SetRef(locomotionSo, "rightHand", rightHand.transform);
        locomotionSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        Debug.Log($"[MakeAssets] PlayerRig 생성 완료: {PrefabPath}");
    }

    private static void SetXRNode(GameObject handObject, XRNode node)
    {
        var tracker = handObject.GetComponent<XRNodeTracker>();
        var so = new SerializedObject(tracker);
        SerializedProperty prop = so.FindProperty("node");
        if (prop == null)
        {
            Debug.LogError("[MakeAssets] XRNodeTracker.node 필드를 찾을 수 없습니다.");
            return;
        }
        // enumValueIndex는 enumNames 배열 내 표시 순서 인덱스라 XRNode처럼 값이 불연속이면 어긋난다.
        // 실제 저장값은 intValue(원본 정수값)로 설정해야 정확하다.
        prop.intValue = (int)node;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
