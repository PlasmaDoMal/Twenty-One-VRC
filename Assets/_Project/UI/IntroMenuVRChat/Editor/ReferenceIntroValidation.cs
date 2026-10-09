using UnityEditor;
using UnityEngine;
using VRC.Udon;
using System.Collections.Generic;
[InitializeOnLoad]
public static class ReferenceIntroValidation
{
 static readonly List<string> results=new List<string>();static int stage;static Vector3 menuPosition,playerPosition;static Quaternion menuRotation;
 static ReferenceIntroValidation(){EditorApplication.update+=Tick;}
 public static void Start(){results.Clear();stage=0;SessionState.SetBool("ReferenceIntroValidation.Run",true);EditorApplication.isPlaying=true;}
 static void Check(bool ok,string name){results.Add((ok?"PASS ":"FAIL ")+name);System.IO.Directory.CreateDirectory("Temp/IntroMenuQA");System.IO.File.WriteAllLines("Temp/IntroMenuQA/reference-validation.txt",results);}
 static void Tick(){
 if(!SessionState.GetBool("ReferenceIntroValidation.Run",false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
 var c=IntroMenuEditorUtility.FindProgram("IntroMenuController");if(c==null||IntroMenuEditorUtility.Read<UdonBehaviour>(c,"logoAnimator")==null||VRC.SDKBase.Networking.LocalPlayer==null)return;
 var a=IntroMenuEditorUtility.Read<UdonBehaviour>(c,"logoAnimator");var cu=c;var u=a;if(u==null)return;
 var player=VRC.SDKBase.Networking.LocalPlayer;
 var value=u.GetProgramVariable("currentTime");if(value==null)return;float t=(float)value;
 if(stage==0&&t>.2f){Check(!IntroMenuEditorUtility.Read<UnityEngine.UI.Button>(c,"playButton").interactable&&IntroMenuEditorUtility.Read<UnityEngine.UI.RawImage>(a,"logo").color.a==0&&IntroMenuEditorUtility.Read<UnityEngine.UI.Text>(a,"loadingHeading").color.a==0,"Initial black pause");
 Check(!VRC.SDK3.ClientSim.ClientSimExtensions.GetClientSimPlayer(player).locomotionData.GetImmobilized(),"Player is free to move during menu");
 Check(!IntroMenuEditorUtility.Read<GameObject>(c,"blackoutRoot").activeSelf&&GameObject.Find("BlackLobbyRoom")!=null,"Physical black room replaces fullscreen blackout");
 Check(!IntroMenuEditorUtility.Read<bool>(c,"followPlayer")&&IntroMenuEditorUtility.Read<Transform>(c,"fixedMenuAnchor")!=null,"Fixed menu mode configured");
 Check(Object.FindObjectOfType<VRC.SDK3.Components.VRCSceneDescriptor>().spawns.Length==6,"Six distinct lobby spawn points configured");
 menuPosition=IntroMenuEditorUtility.Read<GameObject>(c,"introMenuRoot").transform.position;menuRotation=IntroMenuEditorUtility.Read<GameObject>(c,"introMenuRoot").transform.rotation;playerPosition=player.GetPosition();
 player.TeleportTo(playerPosition+Vector3.right*.7f,Quaternion.Euler(0,20,0));cu.SendCustomEvent("Play");Check((int)cu.GetProgramVariable("state")==0,"Early custom Play event rejected");stage++;}
 if(stage==1&&t>3f){Check(Vector3.Distance(menuPosition,IntroMenuEditorUtility.Read<GameObject>(c,"introMenuRoot").transform.position)<.001f&&Quaternion.Angle(menuRotation,IntroMenuEditorUtility.Read<GameObject>(c,"introMenuRoot").transform.rotation)<.01f,"Menu stays fixed after player translates and turns");
 Check(Vector3.Distance(player.GetPosition(),playerPosition)>.5f,"Player moved independently of menu");
 player.TeleportTo(playerPosition,Quaternion.identity);
 Check(IntroMenuEditorUtility.Read<UnityEngine.UI.Text>(a,"loadingHeading").color.a>0&&IntroMenuEditorUtility.Read<UnityEngine.UI.Text>(a,"loadingCaption").color.a>0&&IntroMenuEditorUtility.Read<UnityEngine.UI.RawImage>(a,"logo").color.a==0,"Loading only during loading phase");stage++;}
 if(stage==2&&t>5.5f){Check(IntroMenuEditorUtility.Read<UnityEngine.UI.RawImage>(a,"logo").color.a>0&&IntroMenuEditorUtility.Read<UnityEngine.UI.Text>(a,"loadingHeading").color.a==0&&!IntroMenuEditorUtility.Read<UnityEngine.UI.Button>(c,"playButton").interactable,"Logo entrance replaces loading, input still locked");Check(u.SyncMethod==VRC.SDKBase.Networking.SyncType.None,"Logo animator has no network synchronization");stage++;}
 if(stage==3&&t>9f){Check(IntroMenuEditorUtility.Read<CanvasGroup>(a,"playGroup").alpha==1&&!IntroMenuEditorUtility.Read<UnityEngine.UI.Button>(c,"playButton").interactable&&IntroMenuEditorUtility.Read<GameObject>(a,"tablePreview").activeSelf,"Play and reference table appear after entrance");stage++;}
 if(stage==4&&t>13.5f){Check(IntroMenuEditorUtility.Read<UnityEngine.UI.Button>(c,"playButton").interactable&&(bool)u.GetProgramVariable("readyForPlay"),"Button enables after complete introduction");Check(IntroMenuEditorUtility.Read<UnityEngine.UI.Image>(a,"indicator").color.a>0,"Selected Play indicator visible");
 Check(IntroMenuEditorUtility.Read<UnityEngine.UI.Button>(c,"playButton").GetComponent<RectTransform>().rect.width>=470&&IntroMenuEditorUtility.Read<UnityEngine.UI.Button>(c,"playButton").transform.Find("PlayText").GetComponent<UnityEngine.UI.Text>().fontSize==128,"Larger centered Play target");
 Check(Mathf.Abs(IntroMenuEditorUtility.Read<UnityEngine.UI.RawImage>(a,"logo").canvas.GetComponent<RectTransform>().rect.width*IntroMenuEditorUtility.Read<UnityEngine.UI.RawImage>(a,"logo").canvas.transform.lossyScale.x-1.6f)<.01f,"Compact 1.6 metre menu width");SessionState.SetBool("ReferenceIntroValidation.Run",false);IntroMenuValidation.Begin();stage++;}
 }
}
