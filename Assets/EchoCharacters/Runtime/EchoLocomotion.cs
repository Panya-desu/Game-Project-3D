using UnityEngine;
namespace EchoCharacters
{
    /// <summary>Visual motion adapter. The game motor still owns movement/collision.
    /// Feed ACTUAL speed after motor acceleration, or SetSpeed for existing callers.
    /// Facing is opt-in: never call SetFacing if the motor already rotates the actor.</summary>
    [DefaultExecutionOrder(-20),DisallowMultipleComponent]
    public sealed class EchoLocomotion:MonoBehaviour
    {
        public float accelerationTime=.20f,decelerationTime=.25f,turnTime=.22f;
        public float walkSpeed=.61828f,runSpeed=2.0625f;
        public bool useTransitionClips=true;
        // Game: the player's body walks at the game's pace with a capped cadence (2.6x looked like a sprint)
        public float maxPlaybackRate=2.6f;
        public float CurrentSpeed {get;private set;}
        public float TargetSpeed {get;private set;}
        public float WorldYaw {get;private set;}
        public float PlaybackRate {get;private set;}=1;
        Vector3 previousPosition;float suppliedSpeed=-1,rateVelocity;
        Animator animator;float speedVelocity,yawVelocity,targetYaw;bool facing;
        float previousTarget;int mode;float modeTime;bool cached;
        float turnStartYaw,turnAmount;
        public bool Turning=>mode==3;
        public float TurnProgress=>Mathf.Clamp01(modeTime/1.2f);
        public float TurnAmount=>turnAmount;
        public Vector3 TurnPivot {get;private set;}
        public int TurnSerial {get;private set;}
        static readonly int Speed=Animator.StringToHash("Speed");
        void Awake(){animator=GetComponentInChildren<Animator>();previousPosition=transform.position;WorldYaw=transform.eulerAngles.y;}
        public void SetSpeed(float speed){suppliedSpeed=-1;TargetSpeed=float.IsFinite(speed)?Mathf.Clamp01(speed):0;}
        public void SetVelocity(Vector3 actualWorldVelocity)
        {
            float speed=Vector3.ProjectOnPlane(actualWorldVelocity,Vector3.up).magnitude/Mathf.Max(.01f,Mathf.Abs(transform.lossyScale.y));
            SetSpeed(speed<=walkSpeed?.5f*speed/Mathf.Max(.01f,walkSpeed):Mathf.Lerp(.5f,1,Mathf.InverseLerp(walkSpeed,Mathf.Max(walkSpeed+.01f,runSpeed),speed)));
            suppliedSpeed=speed;
        }
        public void SetFacing(Vector3 worldDirection)
        {
            worldDirection.y=0;if(worldDirection.sqrMagnitude<.0001f)return;
            targetYaw=Mathf.Atan2(worldDirection.x,worldDirection.z)*Mathf.Rad2Deg;facing=true;
        }
        public void ReleaseFacing(){facing=false;}
        public void ResetMotion()
        {CurrentSpeed=TargetSpeed=previousTarget=0;speedVelocity=yawVelocity=rateVelocity=0;suppliedSpeed=-1;PlaybackRate=1;previousPosition=transform.position;WorldYaw=transform.eulerAngles.y;mode=0;facing=false;if(animator){animator.SetFloat(Speed,0);animator.SetFloat("LocomotionRate",1);}}
        bool Play(string state,float fade)
        {int hash=Animator.StringToHash("Base Layer."+state);if(!animator.HasState(0,hash))return false;animator.CrossFadeInFixedTime(hash,fade,0,0);modeTime=0;return true;}
        void Update()
        {
            if(!animator||!animator.isActiveAndEnabled||!animator.runtimeAnimatorController)return;
            if(!cached){cached=true;CurrentSpeed=animator.GetFloat(Speed);}
            float dt=Time.deltaTime;if(dt<=0)return;
            var state=animator.GetCurrentAnimatorStateInfo(0);
            Vector3 delta=transform.position-previousPosition;previousPosition=transform.position;
            float observed=delta.sqrMagnitude>1?0:Vector3.ProjectOnPlane(delta,Vector3.up).magnitude/dt/Mathf.Max(.01f,Mathf.Abs(transform.lossyScale.y));
            bool ordinary=state.IsName("Locomotion")||state.IsName("WalkStart")||state.IsName("RunStart")||state.IsName("WalkStopLeft")||state.IsName("WalkStopRight")||state.IsName("RunStopLeft")||state.IsName("RunStopRight")||state.IsName("TurnLeft")||state.IsName("TurnRight");
            CurrentSpeed=Mathf.SmoothDamp(CurrentSpeed,TargetSpeed,ref speedVelocity,TargetSpeed>CurrentSpeed?accelerationTime:decelerationTime,Mathf.Infinity,dt);
            if(TargetSpeed==0&&CurrentSpeed<.001f)CurrentSpeed=0;
            animator.SetFloat(Speed,CurrentSpeed);
            float reference=state.IsName("Rush")?runSpeed:CurrentSpeed<=.5f?CurrentSpeed*2*walkSpeed:Mathf.Lerp(walkSpeed,runSpeed,(CurrentSpeed-.5f)*2);
            float actual=suppliedSpeed>=0?suppliedSpeed:observed;
            float rate=reference>.04f&&actual>.04f?Mathf.Clamp(actual/reference,.55f,maxPlaybackRate):1;
            PlaybackRate=Mathf.SmoothDamp(PlaybackRate,rate,ref rateVelocity,.12f,Mathf.Infinity,dt);
            animator.SetFloat("LocomotionRate",PlaybackRate);
            modeTime+=dt;
            if(ordinary&&useTransitionClips)
            {
                if(TargetSpeed>.04f&&previousTarget<=.04f&&CurrentSpeed<.15f)
                {if(Play(TargetSpeed>.75f?"RunStart":"WalkStart",.12f))mode=1;}
                else if(TargetSpeed<=.01f&&previousTarget>.01f&&CurrentSpeed>.06f)
                {
                    float phase=Mathf.Repeat(state.normalizedTime,1);
                    if(Play((CurrentSpeed>.72f?"Run":"Walk")+"Stop"+(phase<.25f||phase>.75f?"Left":"Right"),.18f))mode=2;
                }
                else if(mode==2&&TargetSpeed>.04f){animator.CrossFadeInFixedTime("Locomotion",.12f);mode=0;}
                if(modeTime>(mode==3?1.2f:1.05f))mode=0;
            }
            if(facing&&ordinary)
            {
                float current=transform.eulerAngles.y,error=Mathf.DeltaAngle(current,targetYaw);
                if(useTransitionClips&&TargetSpeed<.02f&&CurrentSpeed<.04f&&Mathf.Abs(error)>12&&mode==0)
                {if(Play(error>0?"TurnRight":"TurnLeft",.12f)){mode=3;turnStartYaw=current;turnAmount=Mathf.Clamp(error,-90,90);TurnPivot=transform.position;TurnSerial++;}}
                float yaw;
                if(mode==3){float t=TurnProgress;yaw=turnStartYaw+turnAmount*(.5f*Ease(t*2)+.5f*Ease(t*2-1));yawVelocity=0;}
                else yaw=Mathf.SmoothDampAngle(current,targetYaw,ref yawVelocity,turnTime,360,dt);
                transform.rotation=Quaternion.Euler(0,yaw,0);
            }
            previousTarget=TargetSpeed;
            WorldYaw=transform.eulerAngles.y;
        }
        void OnDisable(){speedVelocity=yawVelocity=0;cached=false;}
        public static float Ease(float t){t=Mathf.Clamp01(t);return t*t*t*(t*(t*6-15)+10);}
    }
}
