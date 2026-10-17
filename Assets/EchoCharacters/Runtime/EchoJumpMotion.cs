using UnityEngine;
namespace EchoCharacters
{
    public enum EchoFoot { None,Left,Right }
    /// <summary>In-place poses only. The caller owns launch delay, translation and landing.</summary>
    [DefaultExecutionOrder(-15),DisallowMultipleComponent]
    public sealed class EchoJumpMotion:MonoBehaviour
    {
        Animator animator;EchoLocomotion motion;
        bool airborne,requested,apex,moving,runJump,landing;float launchY,launchYaw,requestedAt,takeoffAt,landAt,landDuration,landPhase,previousVertical;
        Vector2 jumpVelocity;string riseState,apexState,fallState;
        readonly Transform[] movingHem=new Transform[8];
        public EchoFoot JumpLeadFoot {get;private set;}=EchoFoot.None;
        public float VerticalVelocity {get;private set;}
        public bool Airborne=>airborne;
        public bool IsActive {get;private set;}
        public bool IsLanding=>landing;
        public float LandingPhase=>Mathf.Repeat(landPhase+(Time.time-landAt)/.8f,1);
        public Vector2 JumpVelocity=>jumpVelocity;
        public const float LaunchDelay=.10f,LocomotionExitFade=.12f;
        public void CacheHemBones(Transform[] bones)
        {int count=0;if(bones==null)return;foreach(var bone in bones)if(bone&&bone.name.StartsWith("SkirtChain",System.StringComparison.Ordinal)&&bone.name.EndsWith("_03",System.StringComparison.Ordinal)&&count<movingHem.Length)movingHem[count++]=bone;}
        void Awake(){animator=GetComponentInChildren<Animator>();motion=GetComponent<EchoLocomotion>();}
        void Play(string name,float fade=.12f)
        {if(animator&&animator.HasState(0,Animator.StringToHash("Base Layer."+name)))animator.CrossFadeInFixedTime(name,fade,0,0);}
        public void BeginJump(){BeginJump(motion?motion.LocalVelocity:Vector2.zero);}
        public void BeginJump(Vector2 localVelocity)
        {
            if(airborne||requested)return;
            float entryPhase=motion?motion.LocomotionPhase:0;
            jumpVelocity=localVelocity;requested=IsActive=true;landing=apex=false;launchY=transform.position.y;launchYaw=transform.eulerAngles.y;requestedAt=Time.time;
            moving=localVelocity.sqrMagnitude>.0225f;runJump=moving&&motion&&(motion.Running||motion.RunBlend>=.5f)&&localVelocity.y>Mathf.Abs(localVelocity.x);
            if(motion)motion.SuspendGaitTransition();
            if(moving)
            {
                // Phase zero is LEFT heel strike in every gait; the opposite leg leads the leap.
                float phase=entryPhase;JumpLeadFoot=phase<.5f?EchoFoot.Right:EchoFoot.Left;
                if(runJump&&phase>=.36f&&phase<.5f)JumpLeadFoot=EchoFoot.Left;
                else if(runJump&&phase>=.86f)JumpLeadFoot=EchoFoot.Right;
                string foot=JumpLeadFoot.ToString(),prefix=runJump?"RunStride":"Stride";riseState=prefix+"Rise"+foot;apexState=prefix+"Apex"+foot;fallState=prefix+"Fall"+foot;
                Play((runJump?"RunJump":"JumpFromStride")+foot,LocomotionExitFade);
            }
            else{JumpLeadFoot=EchoFoot.None;riseState="JumpRise";apexState="JumpApex";fallState="FallLoop";Play("JumpAnticipation",LocomotionExitFade);}
        }
        public void SetAirborne(bool value,float verticalVelocity=0)
        {
            VerticalVelocity=verticalVelocity;
            if(!value){if(airborne)Landed(Mathf.Max(0,launchY-transform.position.y));return;}
            if(!airborne)
            {
                if(!requested){launchY=transform.position.y;launchYaw=transform.eulerAngles.y;moving=runJump=false;JumpLeadFoot=EchoFoot.None;riseState="JumpRise";apexState="JumpApex";fallState="FallLoop";}
                takeoffAt=Time.time;airborne=IsActive=true;landing=apex=false;
                if(!moving)Play(verticalVelocity>0?"JumpTakeoff":"FallLoop",.05f);
                requested=false;
            }
            bool pastTakeoff=!moving||Time.time-requestedAt>=.30f;
            if(pastTakeoff)
            {
                if(!apex&&(Mathf.Abs(verticalVelocity)<.6f||(previousVertical>0&&verticalVelocity<=0))){apex=true;Play(apexState,.08f);}
                else if(verticalVelocity<-.6f)PlayIfDifferent(fallState,.10f);
                else if(verticalVelocity>.6f&&Time.time-takeoffAt>.22f)PlayIfDifferent(riseState,.10f);
            }
            previousVertical=verticalVelocity;
        }
        void PlayIfDifferent(string state,float fade)
        {if(!animator.GetCurrentAnimatorStateInfo(0).IsName(state)&&!(animator.IsInTransition(0)&&animator.GetNextAnimatorStateInfo(0).IsName(state)))Play(state,fade);}
        public void Landed(float fallHeight){Landed(fallHeight,motion?motion.LocalVelocity:Vector2.zero);}
        public void Landed(float fallHeight,Vector2 localVelocity)
        {
            if(!IsActive&&!airborne)return;
            jumpVelocity=localVelocity;VerticalVelocity=previousVertical=0;airborne=requested=apex=false;
            bool continuing=localVelocity.sqrMagnitude>.0225f&&fallHeight<=1.5f;
            if(continuing)
            {
                if(JumpLeadFoot==EchoFoot.None)JumpLeadFoot=EchoFoot.Left;
                runJump=motion&&motion.Running&&localVelocity.y>Mathf.Abs(localVelocity.x);
                string name=(runJump?"LandIntoRun":"LandIntoStride")+JumpLeadFoot;
                if(!runJump){int sector=((Mathf.RoundToInt(Mathf.Atan2(localVelocity.x,localVelocity.y)*Mathf.Rad2Deg/45)%8)+8)%8;string[] suffix={"","ForwardRight","StrafeRight","BackwardRight","Backward","BackwardLeft","StrafeLeft","ForwardLeft"};name+=suffix[sector];}
                landPhase=JumpLeadFoot==EchoFoot.Left?0:.5f;landDuration=runJump?14f/60:16f/60;landAt=Time.time;landing=true;Play(name,.12f);
            }
            else{landing=false;Play(fallHeight>1.5f?"LandHeavy":Mathf.Abs(Mathf.DeltaAngle(launchYaw,transform.eulerAngles.y))>25?"LandTurn":"LandSoft",.10f);}
        }
        void Update()
        {
            if(!animator||airborne||requested)return;
            if(landing&&Time.time-landAt>=landDuration-.12f)
            {
                float elapsed=Time.time-landAt;float phase=Mathf.Repeat(landPhase+elapsed/.8f,1);
                landing=false;if(motion)motion.ResumeGait(phase,runJump,.12f);
            }
            var state=animator.GetCurrentAnimatorStateInfo(0);
            if(IsActive&&!landing&&!animator.IsInTransition(0)&&(state.IsName("Locomotion")||state.IsName("RunLocomotion")))IsActive=false;
        }
        public void ApplyMovingAnticipation()
        {
            float flight=Time.time-takeoffAt;if(!airborne||!moving||flight>=.22f||!animator||!animator.isHuman)return;
            var hips=animator.GetBoneTransform(HumanBodyBones.Hips);float amount=.006f*Mathf.Abs(transform.lossyScale.y)*EchoLocomotion.Ease(flight/.03f)*EchoLocomotion.Ease((.22f-flight)/.06f);
            foreach(var bone in movingHem)if(bone){var radial=Vector3.ProjectOnPlane(bone.position-hips.position,transform.up).normalized;bone.position+=radial*amount;}
        }
        void OnDisable(){airborne=requested=apex=moving=runJump=landing=IsActive=false;VerticalVelocity=previousVertical=0;JumpLeadFoot=EchoFoot.None;}
    }
}
