using UnityEngine;
namespace EchoCharacters
{
    /// <summary>Model-space m/s and direction. Motor owns translation; running is explicit.</summary>
    [DefaultExecutionOrder(-20),DisallowMultipleComponent]
    public sealed class EchoLocomotion:MonoBehaviour
    {
        public static float RunReference=>2.0625f;
        public float walkSpeed=0.818181818f,briskSpeed=1.071428571f,powerSpeed=1.375000000f,runSpeed=RunReference;
        public float accelerationTime=.12f,decelerationTime=.14f,turnTime=.12f,maxPlaybackRate=1.2f;
        public bool useTransitionClips=true;
        [Tooltip("Use true when SetLocalVelocity already contains the motor's smoothed velocity.")]
        public bool inputAlreadySmoothed=true;
        Vector2 stopExitVelocity;float stopHoldUntil;
        public float CurrentSpeed {get;private set;}
        public float TargetSpeed=>targetVelocity.magnitude;
        public float WorldYaw {get;private set;}
        public float VisualYawOffset {get;private set;}
        public float PlaybackRate {get;private set;}=1;
        public Vector2 LocalVelocity=>velocity;
        public bool MotorSpeedMismatch=>TargetSpeed>(Running?RunReference:PowerReference(targetVelocity))*1.2f+.01f;
        public Vector2 RecommendedMotorVelocity=>Vector2.ClampMagnitude(targetVelocity,(Running?RunReference:PowerReference(targetVelocity))*1.2f);
        public bool Running=>running&&targetVelocity.y>Mathf.Abs(targetVelocity.x);
        public bool GaitTransitioning=>gaitTransition;
        bool gaitRunning,gaitTransition,gaitTransitionToRun,lastGaitToRun;float gaitTransitionAt,gaitEntryPhase,lastGaitChangeAt=-1000;
        public float RunBlend=>Time.time-lastGaitChangeAt<.30f?Mathf.Lerp(lastGaitToRun?0:1,lastGaitToRun?1:0,Ease((Time.time-lastGaitChangeAt)/.30f)):(gaitRunning?1:0);
        public float RecommendedGaitReference=>Mathf.Lerp(PowerReference(targetVelocity),RunReference,RunBlend);
        public float LocomotionPhase
        {
            get
            {
                if(!animator)return 0;
                if(jump&&jump.IsLanding)return jump.LandingPhase;
                if(startClip!=null&&Time.time-startAt<=startDuration)
                {if(startClip=="RunStart")return .5f*Ease((Time.time-startAt)/startDuration);float active=Mathf.Clamp01((Time.time-startAt-.06f)/Mathf.Max(.01f,startDuration-.06f)),tangent=(startDuration-.06f)/(startClip.EndsWith("Power")?.8f:1.1f);return Mathf.Repeat((startClip.StartsWith("StartLeft")?.5f:0)+(tangent-1)*active*active*active+(1.5f-tangent)*active*active,1);}
                var state=animator.GetCurrentAnimatorStateInfo(0);if(animator.IsInTransition(0)){var next=animator.GetNextAnimatorStateInfo(0);if(next.IsName("Locomotion")||next.IsName("RunLocomotion"))state=next;}
                if(state.IsName("RunStart"))return .5f*Ease(state.normalizedTime);
                return gaitTransition?Mathf.Repeat(gaitEntryPhase+(Time.time-gaitTransitionAt)/.8f,1):Mathf.Repeat(state.normalizedTime,1);
            }
        }
        public float GaitCycleSeconds
        {get{if(gaitRunning||Running)return .8f;float n=DirectionalReference(velocity,false),b=DirectionalReference(velocity,true),p=PowerReference(velocity);return CurrentSpeed<=n?1.1f:CurrentSpeed<=b?Mathf.Lerp(1.1f,56f/60,Mathf.InverseLerp(n,b,CurrentSpeed)):Mathf.Lerp(56f/60,.8f,Mathf.InverseLerp(b,p,CurrentSpeed));}}
        public void SuspendGaitTransition(){gaitTransition=false;startClip=null;pendingStart=pendingStop=false;}
        public void ResumeGait(float phase,bool run,float fade=.12f)
        {if(!gaitTransition)lastGaitChangeAt=-1000;gaitTransition=false;gaitRunning=run;startRelease=Time.time+fade;if(animator)animator.CrossFade(run?"RunLocomotion":"Locomotion",fade/Mathf.Max(.01f,animator.GetCurrentAnimatorStateInfo(0).length),0,Mathf.Repeat(phase,1));}
        public bool Turning=>turning;
        string startClip;float startAt,startDuration;Vector2 startExitVelocity;
        public string StartClip=>startClip;
        public bool Starting=>startClip!=null&&Time.time<startAt+startDuration&&(!jump||!jump.IsActive)&&(!ending||!ending.IsReacting);
        public float StartElapsed=>Mathf.Clamp(Time.time-startAt,0,startDuration);
        public Vector2 RecommendedStartVelocity=>Starting?EchoStartMotorProfiles.Velocity(startClip,StartElapsed):Vector2.zero;
        public bool Pivoting=>pivotActive;
        public event System.Action<float> MotorYawDelta;
        bool pivotActive;float pivotAt,pivotApplied,pivotAngle;
        public float TurnProgress=>Mathf.Clamp01(turnElapsed/turnDuration);
        public float TurnDurationSeconds=>turnDuration;
        public float TurnAmount {get;private set;}
        public Vector3 TurnPivot {get;private set;}
        public int TurnSerial {get;private set;}
        Animator animator;Vector2 targetVelocity,velocity,smoothVelocity;bool facing,turning,running;
        float targetYaw,yawVelocity,turnStartYaw,turnElapsed,turnDuration=1.2f,rateVelocity,startRelease,plantedYaw;bool pendingStart,pendingStop,externalYaw;
        EchoJumpMotion jump;EchoEndingMotion ending;
        void Awake(){animator=GetComponentInChildren<Animator>();jump=GetComponent<EchoJumpMotion>();ending=GetComponent<EchoEndingMotion>();WorldYaw=transform.eulerAngles.y;}
        /// <param name="modelMetersPerSecond">Local X/right, Y/forward; divide world m/s by model scale before calling.</param>
        public void SetLocalVelocity(Vector2 modelMetersPerSecond)
        {var value=float.IsFinite(modelMetersPerSecond.x)&&float.IsFinite(modelMetersPerSecond.y)?Vector2.ClampMagnitude(modelMetersPerSecond,15):Vector2.zero;if(!Starting&&targetVelocity.magnitude<.08f&&value.magnitude>=.08f)pendingStart=true;if(!Starting&&targetVelocity.magnitude>=.08f&&value.magnitude<.08f)pendingStop=true;targetVelocity=value;}
        public void SetVelocity(Vector3 worldVelocity)
        {var local=transform.InverseTransformDirection(worldVelocity)/Mathf.Max(.01f,Mathf.Abs(transform.lossyScale.y));SetLocalVelocity(new Vector2(local.x,local.z));}
        /// <summary>Legacy 0 idle, .5 normal walk, 1 brisk; never selects Run.</summary>
        public void SetSpeed(float normalized)
        {float n=float.IsFinite(normalized)?Mathf.Clamp01(normalized):0;SetLocalVelocity(new Vector2(0,n<=.5f?n*2*walkSpeed:Mathf.Lerp(walkSpeed,briskSpeed,(n-.5f)*2)));}
        public void SetRunning(bool value){running=value;if(animator)animator.SetBool("Running",value);}
        public void SetFacing(Vector3 direction)
        {direction.y=0;if(direction.sqrMagnitude<.0001f)return;targetYaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;facing=true;externalYaw=false;}
        /// <summary>Motor already owns root yaw. Only pose/foot stepping is added.</summary>
        public void SetFacingYaw(float yaw){if(!facing)plantedYaw=transform.eulerAngles.y;targetYaw=yaw;facing=externalYaw=true;}
        public void Pivot(bool left){if(animator){pivotActive=true;pivotAt=Time.time;pivotApplied=0;pivotAngle=left?-180:180;animator.CrossFadeInFixedTime(left?"PivotLeft":"PivotRight",.06f);startRelease=Time.time+.52f;}}
        public void CancelStart(){startClip=null;pendingStart=false;pendingStop=true;}
        public void ReleaseFacing(){facing=turning=false;yawVelocity=VisualYawOffset=0;}
        public void ResetMotion()
        {targetVelocity=velocity=smoothVelocity=Vector2.zero;CurrentSpeed=0;PlaybackRate=1;rateVelocity=yawVelocity=VisualYawOffset=0;startRelease=0;startClip=null;pendingStart=pendingStop=turning=facing=running=pivotActive=gaitRunning=gaitTransition=false;WorldYaw=plantedYaw=transform.eulerAngles.y;if(animator){animator.SetFloat("Speed",0);animator.SetFloat("MoveX",0);animator.SetFloat("MoveY",0);animator.SetFloat("LocomotionRate",1);animator.SetBool("Running",false);}}
        static readonly float[] NormalReferences={0.818181818f,0.800000000f,0.290909091f,0.581818182f,0.581818182f};
        static readonly float[] BriskReferences={1.071428571f,1.050000000f,0.407142857f,0.771428571f,0.771428571f};
        static readonly float[] PowerReferences={1.375000000f,1.350000000f,0.550000000f,1.000000000f,1.000000000f};
        static float Reference(Vector2 direction,float[] values){float angle=Mathf.Abs(Mathf.Atan2(direction.x,direction.y)*Mathf.Rad2Deg);int sector=Mathf.Min(3,Mathf.FloorToInt(angle/45));return Mathf.Lerp(values[sector],values[sector+1],(angle-sector*45)/45);}
        public static float DirectionalReference(Vector2 direction,bool brisk)=>Reference(direction,brisk?BriskReferences:NormalReferences);
        public static float PowerReference(Vector2 direction)=>Reference(direction,PowerReferences);
        void Update()
        {
            if(!animator||!animator.isActiveAndEnabled)return;float dt=Time.deltaTime;if(dt<=0)return;
            Vector2 previousVelocity=velocity;
            if(inputAlreadySmoothed){velocity=targetVelocity;smoothVelocity=Vector2.zero;}
            else velocity=Vector2.SmoothDamp(velocity,targetVelocity,ref smoothVelocity,TargetSpeed>CurrentSpeed?accelerationTime:decelerationTime,Mathf.Infinity,dt);
            if(TargetSpeed<.001f&&velocity.magnitude<.005f)velocity=Vector2.zero;CurrentSpeed=velocity.magnitude;
            float reference=PowerReference(velocity);var state=animator.GetCurrentAnimatorStateInfo(0);
            bool rush=animator.GetBool("Rushing");float rate=(gaitRunning||rush)?CurrentSpeed/Mathf.Max(.01f,runSpeed):CurrentSpeed>reference?CurrentSpeed/reference:1;
            bool free=(!jump||!jump.IsActive)&&(!ending||!ending.IsReacting)&&!rush;
            if(free&&gaitTransition&&Time.time-gaitTransitionAt>=.18f)ResumeGait(gaitEntryPhase+(Time.time-gaitTransitionAt)/.8f,gaitTransitionToRun,.12f);
            if(free&&!gaitTransition&&CurrentSpeed>.08f&&Running!=gaitRunning&&(state.IsName("Locomotion")||state.IsName("RunLocomotion"))&&!animator.IsInTransition(0))
            {
                float phase=LocomotionPhase,remainder=phase%.5f;
                if(remainder<.08f){gaitTransition=true;lastGaitToRun=gaitTransitionToRun=Running;gaitEntryPhase=phase<.5f?0:.5f;lastGaitChangeAt=gaitTransitionAt=Time.time;animator.CrossFadeInFixedTime((Running?"WalkToRun":"RunToWalk")+(phase<.5f?"Left":"Right"),.12f,0,0);}
            }
            if(pendingStart)
            {
                pendingStart=false;
                if(useTransitionClips&&!running&&!rush&&(!jump||!jump.IsActive)&&(!ending||!ending.IsReacting))
                {
                    string[] names={"StartForward","StartForwardRight","StartRight","StartBackwardRight","StartBackward","StartBackwardLeft","StartLeft","StartForwardLeft"};
                    int sector=((Mathf.RoundToInt(Mathf.Atan2(targetVelocity.x,targetVelocity.y)*Mathf.Rad2Deg/45)%8)+8)%8;string name=names[sector]+(TargetSpeed>DirectionalReference(targetVelocity,true)?"Power":"");
                    if(animator.HasState(0,Animator.StringToHash("Base Layer."+name))){animator.CrossFadeInFixedTime(name,.06f,0,0);startRelease=Time.time+(name.EndsWith("Power")?.55f:.65f);startClip=name;startAt=Time.time;startDuration=name.EndsWith("Power")?.5f:.6f;startExitVelocity=targetVelocity.normalized*(name.EndsWith("Power")?PowerReference(targetVelocity):DirectionalReference(targetVelocity,false));}
                }
                else if(useTransitionClips&&Running&&free){animator.CrossFadeInFixedTime("RunStart",.12f,0,0);gaitRunning=true;startRelease=Time.time+.55f;startClip="RunStart";startAt=Time.time;startDuration=.5f;startExitVelocity=Vector2.up*RunReference;}
            }
            if(pendingStop){startClip=null;pendingStop=false;if(useTransitionClips&&free){gaitTransition=false;bool left=LocomotionPhase<.5f;string[] sectors={"","ForwardRight","StrafeRight","BackwardRight","Backward","BackwardLeft","StrafeLeft","ForwardLeft"};int sector=((Mathf.RoundToInt(Mathf.Atan2(previousVelocity.x,previousVelocity.y)*Mathf.Rad2Deg/45)%8)+8)%8;string stop=gaitRunning?(left?"RunStopLeft":"RunStopRight"):(left?"WalkStopLeft":"WalkStopRight")+sectors[sector];stopExitVelocity=previousVelocity;stopHoldUntil=Time.time+.12f;animator.CrossFadeInFixedTime(stop,.12f,0,0);gaitRunning=false;startRelease=Time.time+.55f;}}
            if(CurrentSpeed<.01f)rate=1;
            PlaybackRate=Mathf.SmoothDamp(PlaybackRate,Mathf.Clamp(rate,gaitRunning?.55f:.8f,Mathf.Min(1.2f,maxPlaybackRate)),ref rateVelocity,.10f,Mathf.Infinity,dt);
            var parameter=velocity;if(parameter.magnitude>reference)parameter=parameter.normalized*reference;
            // The start owns an acceleration profile. Its low early motor speed must
            // not shrink the destination stride by blending that gait back to Idle.
            if(startClip!=null&&Time.time<startRelease+.1f)
            {
                // Keep the outgoing Idle at Idle during the fade into a start.
                // Applying the destination stride to that outgoing tree caused a large first-step knee pop.
                parameter=state.IsName("Locomotion")&&Time.time-startAt<.12f?Vector2.zero:Vector2.Lerp(startExitVelocity,parameter,Ease((Time.time-startAt-startDuration)/.12f));
            }
            if(Time.time<stopHoldUntil&&(state.IsName("Locomotion")||state.IsName("RunLocomotion")))parameter=stopExitVelocity;
            animator.SetFloat("Speed",CurrentSpeed);animator.SetFloat("MoveX",parameter.x);animator.SetFloat("MoveY",parameter.y);animator.SetFloat("LocomotionRate",PlaybackRate);
            if(CurrentSpeed>.08f&&Time.time>startRelease&&free&&!gaitTransition&&!animator.IsInTransition(0)&&!state.IsName("Locomotion")&&!state.IsName("RunLocomotion"))ResumeGait(LocomotionPhase,gaitRunning,.14f);
        }
        // Steering is independent of the selected animation state, including custom states.
        void LateUpdate()
        {
            if(pivotActive){float progress=Mathf.Clamp01((Time.time-pivotAt)/.5f),angle=pivotAngle*(Ease(progress*2)+Ease(progress*2-1))*.5f;MotorYawDelta?.Invoke(angle-pivotApplied);pivotApplied=angle;if(progress>=1)pivotActive=false;}
            if(!facing){WorldYaw=transform.eulerAngles.y;return;}float dt=Time.deltaTime;if(dt<=0)return;
            float current=transform.eulerAngles.y,error=Mathf.DeltaAngle(externalYaw?plantedYaw:current,targetYaw);
            if(useTransitionClips&&CurrentSpeed<.08f&&TargetSpeed<.08f&&!Starting&&Time.time<startRelease&&(!jump||!jump.IsActive)&&(!ending||!ending.IsReacting))
            {WorldYaw=current;VisualYawOffset=0;return;}
            bool canStep=CurrentSpeed<.08f&&TargetSpeed<.08f&&!Starting&&Time.time>startRelease&&(!jump||!jump.IsActive)&&(!ending||!ending.IsReacting);
            if(!turning&&useTransitionClips&&canStep&&Mathf.Abs(error)>35)
            {turning=true;turnElapsed=0;turnStartYaw=externalYaw?plantedYaw:current;float size=Mathf.Abs(error)<67.5f?45:Mathf.Abs(error)<135?90:180;TurnAmount=error;turnDuration=size==45?.8f:size==90?1.2f:1.6f;TurnPivot=transform.position;TurnSerial++;}
            float yaw;
            if(turning&&canStep)
            {
                int steps=Mathf.Abs(TurnAmount)>135?3:2;int before=Mathf.FloorToInt(TurnProgress*steps);turnElapsed+=dt;float t=TurnProgress;int after=Mathf.FloorToInt(t*steps);float part=0;for(int k=0;k<steps;k++)part+=Ease(t*steps-k)/steps;yaw=turnStartYaw+TurnAmount*part;
                if(after>before&&Mathf.Abs(Mathf.DeltaAngle(turnStartYaw+TurnAmount,targetYaw))>30)
                {
                    var feet=animator?animator.GetComponent<EchoTurnFootIK>():null;if(feet)feet.PrepareRetarget();
                    turnStartYaw=yaw;TurnAmount=Mathf.DeltaAngle(yaw,targetYaw);turnElapsed=0;float size=Mathf.Abs(TurnAmount);turnDuration=size<67.5f?.8f:size<135?1.2f:1.6f;TurnPivot=transform.position;TurnSerial++;
                }
                else if(t>=1){turning=false;plantedYaw=yaw;}yawVelocity=0;
            }
            else{turning=false;yaw=Mathf.SmoothDampAngle(current,targetYaw,ref yawVelocity,turnTime,540,dt);}
            if(!externalYaw)transform.rotation=Quaternion.Euler(0,yaw,0);WorldYaw=transform.eulerAngles.y;VisualYawOffset=externalYaw&&turning?Mathf.DeltaAngle(WorldYaw,yaw):0;if(!canStep)plantedYaw=WorldYaw;
        }
        public static float Ease(float t){t=Mathf.Clamp01(t);return t*t*t*(10+t*(-15+6*t));}
        void OnDisable(){smoothVelocity=Vector2.zero;yawVelocity=VisualYawOffset=0;turning=pivotActive=false;}
    }
}
