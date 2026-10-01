using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using VRC.SDK3.Components;
using VRC.Udon;
using System.IO;
using System.Collections.Generic;

public static class IntroMenuSetup
{
 const string Root = "Assets/IntroMenuVRChat";
 [MenuItem("Tools/Intro Menu VRChat/Create System")]
 public static void CreateSystem()
 {
    if (GameObject.Find("IntroSystem") != null) throw new System.InvalidOperationException("IntroSystem already exists. Refusing to duplicate.");
    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    if (scene.path != "Assets/Scenes/Menu.unity") throw new System.InvalidOperationException("Expected Menu scene.");
    foreach (string folder in new[]{"Materials","UI","Prefabs","Backups"}) Directory.CreateDirectory(Root+"/"+folder);
    AssetDatabase.Refresh();
    // Preserve the complete pre-install scene, including unsaved user edits.
    EditorSceneManager.SaveScene(scene,Root+"/Backups/Menu_BeforeIntro.unity",true);
    GameObject root = new GameObject("IntroSystem");
    Undo.RegisterCreatedObjectUndo(root,"Create Intro System");
    Transform spawn = Child("IntroSpawn",root.transform).transform;
    spawn.position = new Vector3(0,0.2f,-30);
    Transform destination = Child("WarehouseSpawnPlaceholder",root.transform).transform;
    destination.position = new Vector3(0,0.15f,12);
    var controller = Child("IntroMenuController",root.transform).AddComponent<IntroMenuController>();
    controller.introSpawn=spawn; controller.warehouseSpawn=destination;
    GameObject menu = Child("IntroMenuRoot",root.transform);
    menu.transform.position=spawn.position+new Vector3(0,1.6f,2);
    controller.introMenuRoot=menu;
    var canvasObject = new GameObject("MenuCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(VRCUiShape),typeof(CanvasGroup));
    canvasObject.transform.SetParent(menu.transform,false);
    var canvas = canvasObject.GetComponent<Canvas>();
    canvas.renderMode=RenderMode.WorldSpace;
    canvas.sortingOrder=32766;
    var canvasRect=canvasObject.GetComponent<RectTransform>();
    canvasRect.sizeDelta=new Vector2(2560,1080);
    canvasRect.localScale=Vector3.one*(2.5f/2560f);
    canvasObject.GetComponent<GraphicRaycaster>().blockingObjects=GraphicRaycaster.BlockingObjects.None;
    controller.menuGroup=canvasObject.GetComponent<CanvasGroup>();
    var uiMat = new Material(Shader.Find("IntroMenuVRChat/MenuTypography"));
    AssetDatabase.CreateAsset(uiMat,Root+"/Materials/MenuTypography.mat");
    Font font = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.vrchat.worlds/Runtime/Udon/Fonts/Lato-Bold.ttf");
    var logo = TextAt("Logo",canvasObject.transform,"TWENTY ONE",font,uiMat,new Vector2(-768,281),new Vector2(1100,220),130,TextAnchor.MiddleCenter);
    logo.rectTransform.localScale=new Vector3(0.75f,1,1);
    var buttonObject=new GameObject("PlayButton",typeof(RectTransform),typeof(Image),typeof(Button));
    buttonObject.transform.SetParent(canvasObject.transform,false);
    var buttonRect=buttonObject.GetComponent<RectTransform>();
    buttonRect.anchorMin=buttonRect.anchorMax=new Vector2(0.5f,0.5f);
    buttonRect.pivot=new Vector2(0,0.5f);
    buttonRect.anchoredPosition=new Vector2(-1170,-32);
    buttonRect.sizeDelta=new Vector2(620,200);
    var hit=buttonObject.GetComponent<Image>(); hit.color=Color.clear; hit.material=uiMat;
    var button=buttonObject.GetComponent<Button>(); button.transition=Selectable.Transition.None;
    button.navigation=new Navigation{mode=Navigation.Mode.None}; controller.playButton=button;
    var text=TextAt("PlayText",buttonObject.transform,"JOGAR",font,uiMat,new Vector2(30,0),new Vector2(820,200),136,TextAnchor.MiddleLeft);
    text.rectTransform.anchorMin=text.rectTransform.anchorMax=new Vector2(0,0.5f);
    text.rectTransform.pivot=new Vector2(0,0.5f);
    text.rectTransform.localScale=new Vector3(0.64f,1,1);
    // Small soft dot; no button panel or glow effect.
    Texture2D dotTexture=new Texture2D(64,64,TextureFormat.RGBA32,false);
    var pixels=new Color[4096];
    for(int y=0;y<64;y++) for(int x=0;x<64;x++){
        float r=Vector2.Distance(new Vector2(x+0.5f,y+0.5f),new Vector2(32,32))/32f;
        pixels[y*64+x]=new Color(1,1,1,1-Mathf.SmoothStep(0.65f,0.96f,r));
    }
    dotTexture.SetPixels(pixels);dotTexture.Apply();
    File.WriteAllBytes(Root+"/UI/SelectionDot.png",dotTexture.EncodeToPNG());
    Object.DestroyImmediate(dotTexture);
    AssetDatabase.ImportAsset(Root+"/UI/SelectionDot.png");
    var importer=(TextureImporter)AssetImporter.GetAtPath(Root+"/UI/SelectionDot.png");
    importer.textureType=TextureImporterType.Sprite; importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
    var dot=new GameObject("SelectionIndicator",typeof(RectTransform),typeof(Image));dot.transform.SetParent(buttonObject.transform,false);
    var dotRect=dot.GetComponent<RectTransform>();dotRect.anchorMin=dotRect.anchorMax=new Vector2(0,0.5f);dotRect.anchoredPosition=new Vector2(0,0);dotRect.sizeDelta=new Vector2(16,16);
    var dotImage=dot.GetComponent<Image>();dotImage.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/UI/SelectionDot.png");dotImage.material=uiMat;dotImage.raycastTarget=false;
    controller.selectionIndicator=dot;dot.SetActive(false);
    MenSharpProxy.SyncThenTransfer(new List<GameObject>{controller.gameObject},false);
    var udon=controller.GetComponent<UdonBehaviour>();
    UnityEventTools.AddStringPersistentListener(button.onClick,udon.SendCustomEvent,"Play");
    var trigger=buttonObject.AddComponent<EventTrigger>();
    AddEvent(trigger,EventTriggerType.PointerEnter,udon,"PlayHover");AddEvent(trigger,EventTriggerType.PointerExit,udon,"PlayExit");
    AddEvent(trigger,EventTriggerType.Select,udon,"PlayHover");AddEvent(trigger,EventTriggerType.Deselect,udon,"PlayExit");
    var mesh=new Mesh();mesh.name="FullViewportQuad";
    mesh.vertices=new[]{new Vector3(-0.5f,-0.5f,0),new Vector3(-0.5f,0.5f,0),new Vector3(0.5f,0.5f,0),new Vector3(0.5f,-0.5f,0)};
    mesh.triangles=new[]{0,1,2,0,2,3};mesh.bounds=new Bounds(Vector3.zero,Vector3.one*100000f);
    AssetDatabase.CreateAsset(mesh,Root+"/UI/FullViewportQuad.asset");
    var black=Overlay("BlackBackdrop",root.transform,mesh,1,4997);
    controller.blackoutRoot=black.gameObject;
    var fadeRoot=Child("FadeSystem",root.transform);
    controller.fadeRenderer=Overlay("FadeVisual",fadeRoot.transform,mesh,0,5000);
    controller.hoverAudio=Audio("PlayHover",controller.transform);
    controller.clickAudio=Audio("PlayClick",controller.transform);
    controller.transitionAudio=Audio("TransitionWhoosh",controller.transform);
    var support=GameObject.CreatePrimitive(PrimitiveType.Cube);
    support.name="IntroSafetyFloor";support.transform.SetParent(root.transform);
    support.transform.position=new Vector3(0,-0.1f,-30);support.transform.localScale=new Vector3(8,0.2f,8);
    support.GetComponent<Renderer>().enabled=false;
    var temp=Child("TEMP_WarehouseTest",root.transform);
    var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="TestFloor";floor.transform.SetParent(temp.transform);floor.transform.position=new Vector3(0,-0.15f,12);floor.transform.localScale=new Vector3(8,0.3f,8);
    var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="TestWall";wall.transform.SetParent(temp.transform);wall.transform.position=new Vector3(0,1.5f,15.8f);wall.transform.localScale=new Vector3(8,3,0.25f);
    var testMat=new Material(Shader.Find("Standard"));testMat.color=new Color(0.25f,0.25f,0.25f);AssetDatabase.CreateAsset(testMat,Root+"/Materials/TestArea.mat");
    floor.GetComponent<Renderer>().sharedMaterial=testMat;wall.GetComponent<Renderer>().sharedMaterial=testMat;
    var light=Child("TestLight",temp.transform).AddComponent<Light>();light.type=LightType.Point;light.transform.position=new Vector3(0,2.8f,12);light.range=7;light.intensity=1;
    var descriptor=Object.FindObjectOfType<VRCSceneDescriptor>();
    Undo.RecordObject(descriptor,"Set Intro Spawn");
    descriptor.spawns=new[]{spawn};EditorUtility.SetDirty(descriptor);
    EditorUtility.SetDirty(controller);
    MenSharpProxy.SyncThenTransfer(new List<GameObject>{controller.gameObject},false);
    PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/IntroSystem.prefab");
    AssetDatabase.SaveAssets();
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    Selection.activeGameObject=root;
    Debug.Log("IntroMenuVRChat: created and saved in "+scene.path);
 }
 static GameObject Child(string name,Transform parent){var o=new GameObject(name);o.transform.SetParent(parent,false);return o;}
 static Text TextAt(string name,Transform parent,string value,Font font,Material material,Vector2 pos,Vector2 size,int sizePx,TextAnchor alignment){
    var o=new GameObject(name,typeof(RectTransform),typeof(Text));o.transform.SetParent(parent,false);
    var t=o.GetComponent<Text>();t.font=font;t.text=value;t.fontSize=sizePx;t.color=Color.white;t.material=material;t.alignment=alignment;t.raycastTarget=false;
    t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;
    t.rectTransform.anchoredPosition=pos;t.rectTransform.sizeDelta=size;return t;
 }
 static MeshRenderer Overlay(string name,Transform parent,Mesh mesh,float alpha,int queue){
    var o=Child(name,parent);o.AddComponent<MeshFilter>().sharedMesh=mesh;
    var r=o.AddComponent<MeshRenderer>();
    var m=new Material(Shader.Find("IntroMenuVRChat/LocalBlackout"));m.SetFloat("_Alpha",alpha);m.renderQueue=queue;
    AssetDatabase.CreateAsset(m,Root+"/Materials/"+name+".mat");r.sharedMaterial=m;
    r.sortingOrder = queue == 5000 ? 32767 : 32765;
    r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;r.allowOcclusionWhenDynamic=false;
    r.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;r.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;
    return r;
 }
 static AudioSource Audio(string name,Transform parent){var a=Child(name,parent).AddComponent<AudioSource>();a.playOnAwake=false;a.spatialBlend=0;return a;}
 static void AddEvent(EventTrigger trigger,EventTriggerType type,UdonBehaviour udon,string method){
    var entry=new EventTrigger.Entry{eventID=type};UnityEventTools.AddStringPersistentListener(entry.callback,udon.SendCustomEvent,method);trigger.triggers.Add(entry);
 }
}
