using UnityEngine;
namespace EchoCharacters
{
    /// <summary>Two-step foot planting after the authored FK pose has been restored.</summary>
    [DefaultExecutionOrder(9995),RequireComponent(typeof(Animator))]
    public sealed class EchoTurnFootIK:MonoBehaviour
    {
        Animator animator;EchoLocomotion locomotion;int serial=-1;
        readonly Transform[] upper=new Transform[2],lower=new Transform[2],foot=new Transform[2];
        readonly Vector3[] start=new Vector3[2];readonly Quaternion[] rotation=new Quaternion[2];
        void Awake()
        {
            animator=GetComponent<Animator>();locomotion=GetComponentInParent<EchoLocomotion>();if(!animator.isHuman)return;
            for(int side=0;side<2;side++)
            {upper[side]=animator.GetBoneTransform(side==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);lower[side]=animator.GetBoneTransform(side==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);foot[side]=animator.GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);}
        }
        void LateUpdate()
        {
            if(!locomotion||!locomotion.Turning||!animator.isHuman)return;
            if(serial!=locomotion.TurnSerial)
            {
                serial=locomotion.TurnSerial;for(int side=0;side<2;side++){start[side]=foot[side].position;rotation[side]=foot[side].rotation;}
            }
            float t=locomotion.TurnProgress,weight=EchoLocomotion.Ease(t/.06f)*EchoLocomotion.Ease((1-t)/.06f);
            var yaw=Quaternion.Euler(0,locomotion.TurnAmount,0);float scale=Mathf.Abs(locomotion.transform.lossyScale.y);
            for(int side=0;side<2;side++)
            {
                bool first=side==0?locomotion.TurnAmount<0:locomotion.TurnAmount>=0;
                float u=Mathf.Clamp01(t*2-(first?0:1)),ease=EchoLocomotion.Ease(u);
                Vector3 end=locomotion.TurnPivot+yaw*(start[side]-locomotion.TurnPivot);
                Vector3 goal=Vector3.Lerp(start[side],end,ease)+Vector3.up*(.055f*scale*Mathf.Pow(Mathf.Sin(Mathf.PI*u),2));
                Solve(side,goal,Quaternion.Slerp(rotation[side],yaw*rotation[side],ease),weight);
            }
        }
        void Solve(int side,Vector3 target,Quaternion targetRotation,float weight)
        {
            var a=upper[side];var b=lower[side];var c=foot[side];if(!a||!b||!c)return;
            Quaternion qa=a.rotation,qb=b.rotation,qc=c.rotation;Vector3 head=a.position,delta=target-head;
            float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,c.position),distance=Mathf.Clamp(delta.magnitude,.001f,l1+l2-.0001f);
            Vector3 axis=delta.normalized,pole=Vector3.ProjectOnPlane(locomotion.transform.forward,axis).normalized;
            float along=(l1*l1+distance*distance-l2*l2)/(2*distance);
            Vector3 knee=head+axis*along+pole*Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
            a.rotation=Quaternion.FromToRotation(b.position-head,knee-head)*a.rotation;
            b.rotation=Quaternion.FromToRotation(c.position-b.position,target-b.position)*b.rotation;
            Quaternion solvedA=a.rotation,solvedB=b.rotation;
            a.rotation=Quaternion.Slerp(qa,solvedA,weight);b.rotation=Quaternion.Slerp(qb,solvedB,weight);c.rotation=Quaternion.Slerp(qc,targetRotation,weight);
        }
    }
}
