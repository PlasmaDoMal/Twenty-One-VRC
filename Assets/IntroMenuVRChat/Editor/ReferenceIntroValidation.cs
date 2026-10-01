using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
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
 var c=Object.FindObjectOfType<IntroMenuController>();if(c==null||c.logoAnimator==null||VRC.SDKBase.Networking.LocalPlayer==null)return;
 var a=c.logoAnimator;var u=UdonSharpEditorUtility.GetBackingUdonBehaviour(a);var cu=UdonSharpEditorUtility.GetBackingUdonBehaviour(c);if(u==null||cu==null)return;
 var player=VRC.SDKBase.Networking.LocalPlayer;
 var value=u.GetProgramVariable("currentTime");if(value==null)return;float t=(float)value;
 if(stage==0&&t>.2f){Check(!c.playButton.interactable&&a.logo.color.a==0&&a.loadingHeading.color.a==0,"Initial black pause");
 Check(!VRC.SDK3.ClientSim.ClientSimExtensions.GetClientSimPlayer(player).locomotionData.GetImmobilized(),"Player is free to move during menu");
 Check(!c.blackoutRoot.activeSelf&&GameObject.Find("BlackLobbyRoom")!=null,"Physical black room replaces fullscreen blackout");
 Check(!c.followPlayer&&c.fixedMenuAnchor!=null,"Fixed menu mode configured");
 Check(Object.FindObjectOfType<VRC.SDK3.Components.VRCSceneDescriptor>().spawns.Length==6,"Six distinct lobby spawn points configured");
 menuPosition=c.introMenuRoot.transform.position;menuRotation=c.introMenuRoot.transform.rotation;playerPosition=player.GetPosition();
 player.TeleportTo(playerPosition+Vector3.right*.7f,Quaternion.Euler(0,20,0));cu.SendCustomEvent("Play");Check((int)cu.GetProgramVariable("state")==0,"Early custom Play event rejected");stage++;}
 if(stage==1&&t>3f){Check(Vector3.Distance(menuPosition,c.introMenuRoot.transform.position)<.001f&&Quaternion.Angle(menuRotation,c.introMenuRoot.transform.rotation)<.01f,"Menu stays fixed after player translates and turns");
 Check(Vector3.Distance(player.GetPosition(),playerPosition)>.5f,"Player moved independently of menu");
 player.TeleportTo(playerPosition,Quaternion.identity);
 Check(a.loadingHeading.color.a>0&&a.loadingCaption.color.a>0&&a.logo.color.a==0,"Loading only during loading phase");stage++;}
 if(stage==2&&t>5.5f){Check(a.logo.color.a>0&&a.loadingHeading.color.a==0&&!c.playButton.interactable,"Logo entrance replaces loading, input still locked");Check(u.SyncMethod==VRC.SDKBase.Networking.SyncType.None,"Logo animator has no network synchronization");stage++;}
 if(stage==3&&t>9f){Check(a.playGroup.alpha==1&&!c.playButton.interactable&&a.tablePreview.activeSelf,"Play and reference table appear after entrance");stage++;}
 if(stage==4&&t>13.5f){Check(c.playButton.interactable&&(bool)u.GetProgramVariable("readyForPlay"),"Button enables after complete introduction");Check(a.indicator.color.a>0,"Selected Play indicator visible");
 Check(c.playButton.GetComponent<RectTransform>().rect.width>=470&&c.playButton.transform.Find("PlayText").GetComponent<UnityEngine.UI.Text>().fontSize==128,"Larger centered Play target");
 Check(Mathf.Abs(a.logo.canvas.GetComponent<RectTransform>().rect.width*a.logo.canvas.transform.lossyScale.x-1.6f)<.01f,"Compact 1.6 metre menu width");SessionState.SetBool("ReferenceIntroValidation.Run",false);IntroMenuValidation.Begin();stage++;}
 }
}
