using System.Collections.Generic;
using MenSharp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UdonSharpEditor;
using VRC.SDK3.Components;

public static class WorldIntegrationSetup
{
    [MenuItem("Tools/Twenty One/Configure World Session")]
    public static void Configure()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var game = GameObject.Find("TwentyOne");
        var menu = GameObject.Find("MenuSystem");
        var intro = Object.FindObjectOfType<IntroMenuController>(true);
        var dealer = Object.FindObjectOfType<CardDealer>(true);
        var router = menu.GetComponentInChildren<MenuRouter>(true);
        var lobby = menu.GetComponentInChildren<MatchLobby>(true);
        var runtime = GameObject.Find("WorldMatchRuntime") ?? new GameObject("WorldMatchRuntime");
        var session = runtime.GetComponent<WorldMatchSession>();
        if (session == null) session = UdonSharpComponentExtensions.AddUdonSharpComponent<WorldMatchSession>(runtime);
        var play1 = game.transform.Find("Play1");
        var play2 = game.transform.Find("Play2");
        play1.position = new Vector3(play1.position.x, 0.04f, play1.position.z);
        play2.position = new Vector3(play2.position.x, 0.04f, play2.position.z);
        var center = new Vector3(0.37f, 0.04f, 22.3f);
        play1.rotation = Quaternion.LookRotation(center - play1.position);
        play2.rotation = Quaternion.LookRotation(center - play2.position);
        var spawn = GameObject.Find("BasementSpawn");
        if (spawn == null) spawn = new GameObject("BasementSpawn");
        spawn.transform.SetPositionAndRotation(new Vector3(-8.65f, 0.24f, 12.7f), Quaternion.identity);
        menu.transform.SetPositionAndRotation(new Vector3(-8.65f, 1.65f, 15.68f), Quaternion.identity);
        intro.warehouseSpawn = spawn.transform;
        intro.fadeToBlackDuration = 0.4f;
        intro.fadeFromBlackDuration = 0.5f;
        intro.blackHoldDuration = 0.15f;
        dealer.preserveLobbySlots = true;
        session.gameRoot = game;
        session.playerSlots = new[] { dealer.slots[0].gameObject, dealer.slots[1].gameObject };
        session.play1 = play1; session.play2 = play2; session.lobbySpawn = spawn.transform;
        session.menuRouter = MenSharpProxy.FindPaired(router);
        session.introController = MenSharpProxy.FindPaired(intro);
        session.dealer = MenSharpProxy.FindPaired(dealer);
        var fade = runtime.transform.Find("MatchFadeVisual");
        if (fade == null)
        {
            var clone = Object.Instantiate(intro.fadeRenderer.gameObject, runtime.transform);
            clone.name = "MatchFadeVisual";
            fade = clone.transform;
        }
        session.fadeRenderer = fade.GetComponent<Renderer>();
        session.fadeRenderer.enabled = false;
        const string fadePath = "Assets/WorldSession/MatchFade.mat";
        var fadeMat = AssetDatabase.LoadAssetAtPath<Material>(fadePath);
        if (fadeMat == null) { fadeMat = new Material(intro.fadeRenderer.sharedMaterial); AssetDatabase.CreateAsset(fadeMat, fadePath); }
        fadeMat.SetFloat("_Alpha", 0f);
        session.fadeRenderer.sharedMaterial = fadeMat;
        lobby.dealer = dealer; lobby.previewOnly = false; lobby.session = UdonSharpEditorUtility.GetBackingUdonBehaviour(session);
        router.dealer = dealer;
        router.applyToTable = false;
        foreach (var v in game.GetComponentsInChildren<OwnerOnlyVisibility>(true)) v.assignedSlot = MenSharpProxy.FindPaired(dealer.slots[Mathf.Clamp(v.ownerPlayerIndex, 0, 1)]);
        foreach (var v in game.GetComponentsInChildren<OwnerOnlyUIVisibility>(true)) v.assignedSlot = MenSharpProxy.FindPaired(dealer.slots[Mathf.Clamp(v.ownerPlayerIndex, 0, 1)]);
        var targets = new List<GameObject>();
        foreach (var p in Object.FindObjectsOfType<MenSharpBehaviour>(true)) if (!targets.Contains(p.gameObject)) targets.Add(p.gameObject);
        MenSharpProxy.SyncThenTransfer(targets, false);
        UdonSharpEditorUtility.CopyProxyToUdon(session);
        foreach (var t in menu.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            if (t.name == "LobbyInfo" && t.transform.parent.name == "Screen_create") t.text = "Start when both players are ready.";
        }
        FixCubeCollision();
        EditorUtility.SetDirty(session); EditorUtility.SetDirty(lobby); EditorUtility.SetDirty(router); EditorUtility.SetDirty(dealer); EditorUtility.SetDirty(intro);
        // Root stays active in edit mode; the session hides it at runtime until Create.
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("World session configured: Intro -> Basement -> Play1 / Play2.");
    }
    private static void FixCubeCollision()
    {
        var cube = GameObject.Find("Cube");
        var box = cube.GetComponent<BoxCollider>();
        if (box != null) Object.DestroyImmediate(box);
        var collider = cube.GetComponent<MeshCollider>();
        if (collider == null) collider = cube.AddComponent<MeshCollider>();
        collider.convex = false; collider.sharedMesh = cube.GetComponent<MeshFilter>().sharedMesh;
    }
    private static Material Mat(string name, Color color, float metal = 0f, bool glow = false)
    {
        string folder = "Assets/WorldSession/Materials";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/WorldSession", "Materials");
        string path = folder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = color; m.SetFloat("_Metallic", metal); m.SetFloat("_Glossiness", metal > 0 ? 0.25f : 0.08f);
        if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 3f); }
        return m;
    }
    private static ProBuilderMesh Box(Transform root, string name, Vector3 position, Vector3 size, Material mat)
    {
        var mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
        mesh.name = name; mesh.transform.SetParent(root, true); mesh.transform.position = position;
        mesh.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return mesh;
    }
    [MenuItem("Tools/Twenty One/Improve Basement")]
    public static void ImproveBasement()
    {
        var cube = GameObject.Find("Cube");
        var old = GameObject.Find("WarehouseDetails");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("WarehouseDetails").transform;
        root.SetParent(cube.transform, true);
        var concrete = Mat("Concrete", new Color(0.30f,0.31f,0.32f));
        var floor = Mat("BasementFloor", new Color(0.105f,0.11f,0.12f));
        var steel = Mat("Steel", new Color(0.085f,0.095f,0.11f), 0.7f);
        var wood = Mat("Storage", new Color(0.20f,0.16f,0.12f));
        var lamp = Mat("Fluorescent", new Color(0.82f,0.89f,1f), 0f, true);
        var mesh = cube.GetComponent<ProBuilderMesh>();
        mesh.GetComponent<MeshRenderer>().sharedMaterials = new[] { concrete, floor, steel };
        foreach (var face in mesh.faces)
        {
            var normal = Vector3.Cross(mesh.positions[face.indexes[1]]-mesh.positions[face.indexes[0]], mesh.positions[face.indexes[2]]-mesh.positions[face.indexes[0]]).normalized;
            face.submeshIndex = normal.y > 0.7f ? 1 : (normal.y < -0.7f ? 2 : 0);
        }
        mesh.ToMesh(); mesh.Refresh();
        for (int z=8;z<=15;z+=2)
        {
            Box(root,"CeilingBeam_"+z,new Vector3(-8.65f,3.65f,z),new Vector3(8.7f,0.20f,0.14f),steel);
            Box(root,"LightHousing_"+z,new Vector3(-8.65f,3.46f,z),new Vector3(1.7f,0.10f,0.25f),steel);
            Box(root,"LightStrip_"+z,new Vector3(-8.65f,3.40f,z),new Vector3(1.5f,0.02f,0.16f),lamp);
            var light = new GameObject("WarehouseLight_"+z).AddComponent<Light>();
            light.transform.SetParent(root,true); light.transform.position = new Vector3(-8.65f,3.15f,z);
            light.type = LightType.Point; light.range=5.5f; light.intensity=1.1f; light.color=new Color(0.84f,0.90f,1f); light.shadows=LightShadows.None;
        }
        foreach(float x in new[] {-12.95f,-4.45f})
        {
            Box(root,"WallBase",new Vector3(x,0.4f,11.5f),new Vector3(0.10f,0.30f,8.1f),steel);
            foreach(float z in new[] {7.5f,11.3f,15.4f}) Box(root,"SteelColumn",new Vector3(x,2.0f,z),new Vector3(0.15f,3.5f,0.15f),steel);
        }
        foreach(float z in new[] {8.6f,10.8f})
        {
            foreach(float y in new[] {0.42f,1.2f,2.0f}) Box(root,"StorageShelf",new Vector3(-12.55f,y,z),new Vector3(0.7f,0.08f,1.5f),steel);
            foreach(float offset in new[] {-0.6f,0.6f}) Box(root,"ShelfPost",new Vector3(-12.55f,1.22f,z+offset),new Vector3(0.06f,2.12f,0.06f),steel);
            Box(root,"StorageCrate",new Vector3(-12.5f,0.76f,z),new Vector3(0.5f,0.55f,0.7f),wood);
        }
        for(int i=0;i<8;i++) Box(root,"FloorJoint",new Vector3(-8.6f,0.200f,7.5f+i),new Vector3(8.3f,0.005f,0.015f),steel);
        FixCubeCollision();
        EditorSceneManager.MarkSceneDirty(cube.scene); EditorSceneManager.SaveScene(cube.scene);
        Debug.Log("Basement improved with editable ProBuilder beams, shelving, lighting and materials.");
    }
}

