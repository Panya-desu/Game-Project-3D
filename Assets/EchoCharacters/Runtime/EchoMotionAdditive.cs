using UnityEngine;
namespace EchoCharacters
{
    /// <summary>Pose offsets after authored FK; never translates or rotates the motor root.</summary>
    [DefaultExecutionOrder(9992),RequireComponent(typeof(Animator))]
    public sealed class EchoMotionAdditive:MonoBehaviour
    {
        Animator animator;EchoLocomotion motion;EchoJumpMotion jump;Transform chest,head,hips;
        float previousYaw,yawRate,lean,airLag,turnLead,turnLeadVelocity;
        void OnEnable(){previousYaw=transform.eulerAngles.y;yawRate=lean=airLag=turnLead=turnLeadVelocity=0;}
        void Awake(){animator=GetComponent<Animator>();motion=GetComponentInParent<EchoLocomotion>();jump=GetComponentInParent<EchoJumpMotion>();if(animator.isHuman){chest=animator.GetBoneTransform(HumanBodyBones.Chest);head=animator.GetBoneTransform(HumanBodyBones.Head);hips=animator.GetBoneTransform(HumanBodyBones.Hips);}previousYaw=transform.eulerAngles.y;}
        void LateUpdate()
        {
            if(!motion||!chest||Time.deltaTime<=0)return;var root=motion.transform;float yaw=root.eulerAngles.y,rate=Mathf.DeltaAngle(previousYaw,yaw)/Time.deltaTime;previousYaw=yaw;yawRate=Mathf.Lerp(yawRate,rate,1-Mathf.Exp(-Time.deltaTime*12));
            hips.rotation=Quaternion.AngleAxis(motion.VisualYawOffset,root.up)*hips.rotation;
            bool inAir=jump&&jump.Airborne;float limit=inAir?3:5;
            float target=motion.CurrentSpeed>.1f?Mathf.Clamp(-yawRate*.025f,-limit,limit):0;lean=Mathf.Lerp(lean,target,1-Mathf.Exp(-Time.deltaTime*9));chest.rotation=Quaternion.AngleAxis(lean,root.forward)*chest.rotation;
            float leadTarget=motion.Turning?Mathf.Sin(Mathf.PI*motion.TurnProgress)*Mathf.Clamp(motion.TurnAmount*.12f,-15,15):0;
            turnLead=Mathf.SmoothDamp(turnLead,leadTarget,ref turnLeadVelocity,.10f);head.rotation=Quaternion.AngleAxis(turnLead,root.up)*head.rotation;chest.rotation=Quaternion.AngleAxis(turnLead*.4f,root.up)*chest.rotation;
            airLag=Mathf.LerpAngle(airLag,inAir?Mathf.Clamp(-yawRate*.045f,-12,12):0,1-Mathf.Exp(-Time.deltaTime*(inAir?5:12)));
            hips.rotation=Quaternion.AngleAxis(airLag,root.up)*hips.rotation;chest.rotation=Quaternion.AngleAxis(-airLag*.75f,root.up)*chest.rotation;
            if(inAir)
            {
                Vector2 d=jump.JumpVelocity.normalized;chest.rotation=Quaternion.AngleAxis(-d.x*4,root.forward)*Quaternion.AngleAxis(d.y*3,root.right)*chest.rotation;
            }
        }
    }
}
