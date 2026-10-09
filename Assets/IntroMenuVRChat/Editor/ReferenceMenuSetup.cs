using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public static class ReferenceMenuSetup
{
 const string Root="Assets/IntroMenuVRChat";
 [Serializable] public class Pose {public float t,x,y,s;}
 [Serializable] public class Slide {public float t,x;}
 [Serializable] public class Motion {public Pose[] logo; public Slide[] menu;}
 [MenuItem("Tools/Intro Menu VRChat/Apply Measured Reference")]
 public static void Apply()
 {
    if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
    var controller=UnityEngine.Object.FindObjectOfType<IntroMenuController>();
    if(controller==null)throw new InvalidOperationException("Install the intro system first.");
    var canvas=controller.introMenuRoot.GetComponentInChildren<Canvas>(true);
    if(canvas.transform.Find("LogoTrails")!=null)throw new InvalidOperationException("Reference UI already installed.");
    var anim=controller.gameObject.AddComponent<LogoIntroAnimator>();
    var motion=JsonUtility.FromJson<Motion>(File.ReadAllText(Root+"/UI/ReferenceMotion.json"));
    anim.logoX=Curve(motion,0);anim.logoY=Curve(motion,1);anim.logoScale=Curve(motion,2);
    var keys=new Keyframe[motion.menu.Length];
    for(int i=0;i<keys.Length;i++)keys[i]=new Keyframe(motion.menu[i].t,motion.menu[i].x-3f);
    anim.playX=new AnimationCurve(keys);Smooth(anim.playX);
    var oldLogo=canvas.transform.Find("Logo").gameObject;
    oldLogo.name="LegacyTextLogo";oldLogo.SetActive(false);
    var importer=(TextureImporter)AssetImporter.GetAtPath(Root+"/UI/LogoReference.png");
    importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
    var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/UI/LogoReference.png");
    var mat=new Material(Shader.Find("IntroMenuVRChat/ReferenceLogo"));
    AssetDatabase.CreateAsset(mat,Root+"/Materials/ReferenceLogo.mat");
    var trails=new GameObject("LogoTrails",typeof(RectTransform));trails.transform.SetParent(canvas.transform,false);
    anim.echoes=new RawImage[8];
    // Furthest echoes behind the nearer ones and the leading image.
    for(int i=7;i>=0;i--)anim.echoes[i]=Image("Echo_"+(i+1).ToString("00"),trails.transform,tex,mat);
    anim.logo=Image("Logo",canvas.transform,tex,mat);
    var typography=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/MenuTypography.mat");
    var font=AssetDatabase.LoadAssetAtPath<Font>(Root+"/UI/Fonts/Oswald-Regular.ttf");
    var loading=new GameObject("Loading",typeof(RectTransform));loading.transform.SetParent(canvas.transform,false);
    anim.loadingHeading=Text("LoadingHeading",loading.transform,"CARREGANDO",font,typography,new Vector2(5,-32),new Vector2(200,40),20,TextAnchor.MiddleCenter);
    var mono=AssetDatabase.LoadAssetAtPath<Font>("Packages/com.vrchat.worlds/Runtime/Udon/Fonts/Inconsolata-Bold.ttf");
    anim.loadingCaption=Text("LoadingCaption",loading.transform,"não vai demorar muito",mono,typography,new Vector2(4,-84),new Vector2(240,30),16,TextAnchor.MiddleCenter);
    var play=controller.playButton.GetComponent<RectTransform>();
    play.pivot=new Vector2(0,0.5f);play.sizeDelta=new Vector2(155,82);play.localScale=Vector3.one;
    anim.playRoot=play;
    var group=play.gameObject.AddComponent<CanvasGroup>();anim.playGroup=group;
    var label=play.Find("PlayText").GetComponent<TextMeshProUGUI>();
    label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MenuVR/Fonts/Oswald SDF.asset");label.fontSize=36;label.fontStyle=FontStyles.Normal;label.alignment=TextAlignmentOptions.MidlineLeft;
    label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(0,0.5f);
    label.rectTransform.pivot=new Vector2(0,0.5f);label.rectTransform.anchoredPosition=new Vector2(0,0);
    label.rectTransform.sizeDelta=new Vector2(130,70);label.rectTransform.localScale=Vector3.one;
    label.color=Color.white;
    var dot=controller.selectionIndicator.GetComponent<Image>();
    dot.rectTransform.anchoredPosition=new Vector2(-31,1);
    dot.rectTransform.sizeDelta=new Vector2(28,28);
    dot.gameObject.SetActive(true);anim.indicator=dot;
    anim.logoEntryAudio=Audio("LogoEntry",controller.transform);
    anim.logoMoveAudio=Audio("LogoRelocation",controller.transform);
    controller.logoAnimator=anim;
    controller.menuDistance=2f;
    // Reference composition at a virtual 45 degree vertical viewport.
    // This is UI size, never a camera or FOV change.
    canvas.GetComponent<RectTransform>().localScale=Vector3.one*(3.9274f/2560f);
    canvas.sortingOrder=32766;
    controller.menuGroup.alpha=1f;
    controller.menuGroup.interactable=true;
    controller.playButton.interactable=false;
    anim.Sample(0f);
    EditorUtility.SetDirty(anim);EditorUtility.SetDirty(controller);
    MenSharpProxy.SyncThenTransfer(new List<GameObject>{controller.gameObject},false);
    PrefabUtility.SaveAsPrefabAsset(GameObject.Find("IntroSystem"),Root+"/Prefabs/IntroSystem.prefab");
    AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
 }
 static AnimationCurve Curve(Motion m,int field){
    var keys=new Keyframe[m.logo.Length];
    for(int i=0;i<keys.Length;i++)keys[i]=new Keyframe(m.logo[i].t,field==0?m.logo[i].x:field==1?m.logo[i].y:m.logo[i].s);
    var c=new AnimationCurve(keys);Smooth(c);return c;
 }
 static void Smooth(AnimationCurve c){
    c.preWrapMode=WrapMode.ClampForever;c.postWrapMode=WrapMode.ClampForever;
    for(int i=0;i<c.length;i++)AnimationUtility.SetKeyLeftTangentMode(c,i,AnimationUtility.TangentMode.ClampedAuto);
    for(int i=0;i<c.length;i++)AnimationUtility.SetKeyRightTangentMode(c,i,AnimationUtility.TangentMode.ClampedAuto);
 }
 static RawImage Image(string name,Transform parent,Texture2D tex,Material mat){
    var o=new GameObject(name,typeof(RectTransform),typeof(RawImage));o.transform.SetParent(parent,false);
    var r=o.GetComponent<RawImage>();r.texture=tex;r.material=mat;r.raycastTarget=false;
    r.rectTransform.sizeDelta=new Vector2(1080,540);r.color=new Color(1,1,1,0);return r;
 }
 static TextMeshProUGUI Text(string name,Transform parent,string value,Font font,Material material,Vector2 position,Vector2 size,int fontSize,TextAnchor alignment){
    var o=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));o.transform.SetParent(parent,false);
    var t=o.GetComponent<TextMeshProUGUI>();t.text=value;t.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MenuVR/Fonts/Oswald SDF.asset");t.fontSize=fontSize;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;
    t.enableWordWrapping=false;t.overflowMode=TextOverflowModes.Overflow;t.rectTransform.anchoredPosition=position;t.rectTransform.sizeDelta=size;return t;
 }
 static AudioSource Audio(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);var a=go.AddComponent<AudioSource>();a.playOnAwake=false;a.spatialBlend=0;return a;}
}
