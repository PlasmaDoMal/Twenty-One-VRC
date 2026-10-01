using UnityEngine;
using UnityEditor;
using VRC.SDKBase;
using VRC.SDK3.ClientSim;
using VRC.Udon;
using System.Collections.Generic;
using System.IO;

public static class IntroMenuValidation
{
 static UdonBehaviour c;
 static UdonBehaviour u;
 static VRCPlayerApi p;
 static float start, completeAt;
 static int previous, lastFrame;
 static Vector3 initial, movedTarget;
 static bool teleported, blackSeen, respawnRequested, running;
 static List<string> checks, transitions, failures;
 public static string LastReport = "Not run";
 public static void Begin()
 {
    c=IntroMenuEditorUtility.FindProgram("IntroMenuController");
    u=c;p=Networking.LocalPlayer;
    if(!EditorApplication.isPlaying || p==null || !IntroMenuEditorUtility.Read<UnityEngine.UI.Button>(c,"playButton").interactable) throw new System.InvalidOperationException("Start a fresh ClientSim session and wait for initialization.");
    checks=new List<string>();failures=new List<string>();transitions=new List<string>();
    Check((int)u.GetProgramVariable("state")==0,"Waiting for play");
    Check(IntroMenuEditorUtility.Read<GameObject>(c,"introMenuRoot").activeSelf && IntroMenuEditorUtility.Read<GameObject>(c,"blackoutRoot").activeSelf==IntroMenuEditorUtility.Read<bool>(c,"useFullscreenBackdrop"),"Menu active and backdrop matches room mode");
    Check(ClientSimExtensions.GetClientSimPlayer(p).locomotionData.GetImmobilized()==IntroMenuEditorUtility.Read<bool>(c,"immobilizePlayer"),"Initial movement matches free-walk configuration");
    Check(u.SyncMethod==Networking.SyncType.None,"Udon sync mode None");
    start=Time.time;initial=p.GetPosition();previous=-1;lastFrame=-1;completeAt=-1;
    teleported=false;blackSeen=false;respawnRequested=false;running=true;
    IntroMenuEditorUtility.Read<UnityEngine.UI.Button>(c,"playButton").onClick.Invoke();
    Check(!IntroMenuEditorUtility.Read<UnityEngine.UI.Button>(c,"playButton").interactable,"Button disabled immediately");
    Check((int)u.GetProgramVariable("state")==1,"Button event starts fade");
    IntroMenuEditorUtility.Read<UnityEngine.UI.Button>(c,"playButton").onClick.Invoke();
    Check((int)u.GetProgramVariable("state")==1,"Second click does not restart or skip fade");
    EditorApplication.update-=Tick;EditorApplication.update+=Tick;
 }
 static void Check(bool ok,string label){if(ok)checks.Add(label);else failures.Add(label);}
 static void Tick()
 {
    if(!running)return;
    if(!EditorApplication.isPlaying || c==null){Finish("Interrupted");return;}
    if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
    int state=(int)u.GetProgramVariable("state");
    float alpha=IntroMenuEditorUtility.Read<Renderer>(c,"fadeRenderer").material.GetFloat("_Alpha");
    if(state!=previous){transitions.Add((Time.time-start).ToString("F3")+"s: state="+state+" alpha="+alpha.ToString("F3"));previous=state;}
    if((state==2 || state==3) && alpha==1f)blackSeen=true;
    if(state==2 && !checks.Contains("Rendered frame is completely black"))
    {
        var cam=Object.FindObjectsOfType<Camera>();
        Camera playerCamera=null;foreach(var camera in cam)if(camera.name=="PlayerCamera")playerCamera=camera;
        if(playerCamera!=null)
        {
            var rt=RenderTexture.GetTemporary(256,128,24);var old=playerCamera.targetTexture;var active=RenderTexture.active;
            playerCamera.targetTexture=rt;playerCamera.Render();RenderTexture.active=rt;
            var tex=new Texture2D(256,128,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,256,128),0,0);tex.Apply();
            bool black=true;foreach(var pixel in tex.GetPixels32())if(pixel.r!=0 || pixel.g!=0 || pixel.b!=0){black=false;break;}
            Check(black,"Rendered frame is completely black");
            Directory.CreateDirectory("Temp/IntroMenuQA");File.WriteAllBytes("Temp/IntroMenuQA/fully-black.png",tex.EncodeToPNG());
            playerCamera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);
        }
    }
    if(!teleported && Vector3.Distance(p.GetPosition(),initial)>2f)
    {
        teleported=true;Check(blackSeen && alpha==1f,"Teleport observed only after full black, with alpha exactly 1");
        Check(!IntroMenuEditorUtility.Read<GameObject>(c,"introMenuRoot").activeSelf && !IntroMenuEditorUtility.Read<GameObject>(c,"blackoutRoot").activeSelf,"Menu disabled after teleport");
        Check(ClientSimExtensions.GetClientSimPlayer(p).locomotionData.GetImmobilized(),"Player stays immobilized through transition");
    }
    if(state==5 && completeAt<0)
    {
        Check(teleported,"Teleport occurred");
        Check((bool)u.GetProgramVariable("introCompleted"),"Completion is retained locally");
        Check(!IntroMenuEditorUtility.Read<Renderer>(c,"fadeRenderer").enabled && alpha==0f,"Fade ends transparent and renderer is disabled");
        Check(!ClientSimExtensions.GetClientSimPlayer(p).locomotionData.GetImmobilized(),"Movement released after fade");
        Check(Vector3.Distance(p.GetPosition(),IntroMenuEditorUtility.Read<Transform>(c,"warehouseSpawn").position)<0.5f,"Arrived at referenced destination");
        completeAt=Time.time;
        movedTarget=IntroMenuEditorUtility.Read<Transform>(c,"warehouseSpawn").position+Vector3.right;
        IntroMenuEditorUtility.Read<Transform>(c,"warehouseSpawn").position=movedTarget;
        p.Respawn();respawnRequested=true;
    }
    if(respawnRequested && Time.time-completeAt>0.4f)
    {
        Check(Vector3.Distance(p.GetPosition(),movedTarget)<0.5f,"Respawn uses moved destination without changing code");
        Check((int)u.GetProgramVariable("state")==5 && !IntroMenuEditorUtility.Read<GameObject>(c,"introMenuRoot").activeSelf,"Respawn does not reopen menu");
        Finish("Completed");
    }
    if(Time.time-start>15)Finish("Timed out");
 }
 static void Finish(string status)
 {
    running=false;EditorApplication.update-=Tick;
    LastReport=status+"\nPASS:\n"+string.Join("\n",checks)+"\nFAIL:\n"+string.Join("\n",failures)+"\nTIMELINE:\n"+string.Join("\n",transitions);
    Directory.CreateDirectory("Temp/IntroMenuQA");File.WriteAllText("Temp/IntroMenuQA/validation.txt",LastReport);
    Debug.Log("IntroMenu validation: "+status+", "+checks.Count+" passed, "+failures.Count+" failed.");
 }
}
