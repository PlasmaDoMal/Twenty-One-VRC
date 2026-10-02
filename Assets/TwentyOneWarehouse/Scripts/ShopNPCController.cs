using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ShopNPCController : UdonSharpBehaviour {
 public Transform npcRoot,torso,neck,head;
 public float lookDistance=13,headMaxYaw=55,torsoMaxYaw=30,bodyTurnThreshold=75;
 public float headSmoothSpeed=5,torsoSmoothSpeed=3,bodySmoothSpeed=1.8f;
 public bool lookAtLocalPlayer=true;
 public Transform previewTarget;
 private bool turningBody; private Quaternion neutralRoot; private Vector3 neutralTorso; private float elapsed;
 public float currentHeadYaw,currentTorsoYaw,currentBodyYaw;
 void Start(){neutralRoot=npcRoot.rotation;neutralTorso=torso.localPosition;}
 void Update(){if(npcRoot==null||head==null||torso==null||neck==null)return;elapsed+=Time.deltaTime;if(elapsed<.033f)return;float dt=Mathf.Min(elapsed,.1f);elapsed=0;
 Vector3 target=Vector3.zero;bool valid=false;var player=Networking.LocalPlayer;
 if(lookAtLocalPlayer&&Utilities.IsValid(player)){target=player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;valid=true;}
 if(previewTarget!=null){target=previewTarget.position;valid=true;}
 valid=valid&&(target-head.position).sqrMagnitude<lookDistance*lookDistance;
 float yaw=0,pitch=0;
 if(valid){Vector3 delta=target-head.position;Vector3 local=npcRoot.InverseTransformDirection(delta);yaw=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;pitch=-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg;
 if(Mathf.Abs(yaw)>bodyTurnThreshold)turningBody=true; if(Mathf.Abs(yaw)<15)turningBody=false; if(turningBody){Quaternion desired=Quaternion.LookRotation(new Vector3(delta.x,0,delta.z));npcRoot.rotation=Quaternion.Slerp(npcRoot.rotation,desired,1-Mathf.Exp(-bodySmoothSpeed*dt));local=npcRoot.InverseTransformDirection(delta);yaw=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;}}
 else{turningBody=false;npcRoot.rotation=Quaternion.Slerp(npcRoot.rotation,neutralRoot,1-Mathf.Exp(-bodySmoothSpeed*.35f*dt));yaw=Mathf.Sin(Time.time*.37f)*3;pitch=Mathf.Sin(Time.time*.43f);}
 float torsoTarget=Mathf.Clamp(Mathf.Sign(yaw)*Mathf.Max(0,Mathf.Abs(yaw)-30),-torsoMaxYaw,torsoMaxYaw);
 currentTorsoYaw=Mathf.Lerp(currentTorsoYaw,torsoTarget,1-Mathf.Exp(-torsoSmoothSpeed*dt));
 currentHeadYaw=Mathf.Lerp(currentHeadYaw,Mathf.Clamp(yaw-currentTorsoYaw,-headMaxYaw,headMaxYaw),1-Mathf.Exp(-headSmoothSpeed*dt));
 torso.localRotation=Quaternion.Euler(0,currentTorsoYaw,Mathf.Sin(Time.time*.7f)*.35f);torso.localPosition=neutralTorso+Vector3.up*Mathf.Sin(Time.time*1.3f)*.007f;
 neck.localRotation=Quaternion.Euler(0,currentHeadYaw*.25f,0);head.localRotation=Quaternion.Slerp(head.localRotation,Quaternion.Euler(Mathf.Clamp(pitch,-25,25),currentHeadYaw*.75f,0),1-Mathf.Exp(-headSmoothSpeed*dt));currentBodyYaw=npcRoot.eulerAngles.y;
 }
}

