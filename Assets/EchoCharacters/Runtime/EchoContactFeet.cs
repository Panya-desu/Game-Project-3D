using UnityEngine;
namespace EchoCharacters
{
    /// <summary>World-space support-foot anchors blended by authored contact curves; preserves sole pitch.</summary>
    [DefaultExecutionOrder(9994),RequireComponent(typeof(Animator))]
    public sealed class EchoContactFeet:MonoBehaviour
    {
        [HideInInspector] public float leftContact,rightContact,leftPivot=.5f,rightPivot=.5f;
        [HideInInspector] public Vector3 leftHeel,leftToe,rightHeel,rightToe,leftForward,rightForward;
        Animator animator;EchoLocomotion locomotion;EchoJumpMotion jump;Transform root;
        readonly Transform[] upper=new Transform[2],lower=new Transform[2],foot=new Transform[2];
        readonly Vector3[] heelAnchor=new Vector3[2],toeAnchor=new Vector3[2];readonly bool[] ready=new bool[2];Vector3 previousRoot;
        public float LastSupportSlip {get;private set;}
        public float LastAnchorRelease {get;private set;}
        void Awake()
        {
            animator=GetComponent<Animator>();root=GetComponentInParent<EchoCharacterView>().transform;locomotion=root.GetComponent<EchoLocomotion>();jump=root.GetComponent<EchoJumpMotion>();previousRoot=root.position;
            if(!animator.isHuman)return;
            for(int s=0;s<2;s++){upper[s]=animator.GetBoneTransform(s==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);lower[s]=animator.GetBoneTransform(s==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);foot[s]=animator.GetBoneTransform(s==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);}
        }
        void OnEnable(){ready[0]=ready[1]=false;}
        void LateUpdate()
        {
            LastSupportSlip=LastAnchorRelease=0;if(!animator.isHuman)return;if(jump&&jump.Airborne){ready[0]=ready[1]=false;return;}float scale=Mathf.Abs(root.lossyScale.y);
            bool teleport=(root.position-previousRoot).sqrMagnitude>scale*scale;previousRoot=root.position;
            if(locomotion&&locomotion.Turning){ready[0]=ready[1]=false;return;}
            for(int side=0;side<2;side++)
            {
                float weight=Mathf.Clamp01(side==0?leftContact:rightContact);
                if(locomotion&&locomotion.enabled&&animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"))
                {
                    float amount=Mathf.Clamp01(locomotion.CurrentSpeed/Mathf.Max(.01f,EchoLocomotion.DirectionalReference(locomotion.LocalVelocity,false)));
                    weight=locomotion.CurrentSpeed<.08f?1:Mathf.Clamp01(weight/Mathf.Max(.001f,amount));
                }
                if(weight<.001f||teleport){ready[side]=false;continue;}
                var a=upper[side];var b=lower[side];var c=foot[side];if(!a||!b||!c)continue;
                float pivot=Mathf.Clamp01(side==0?leftPivot:rightPivot);
                Vector3 heel=side==0?leftHeel:rightHeel,toe=side==0?leftToe:rightToe;
                if(!ready[side])
                {
                    var direction=Vector3.ProjectOnPlane(c.TransformDirection(side==0?leftForward:rightForward),Vector3.up).normalized;
                    var point=c.TransformPoint(Vector3.Lerp(heel,toe,pivot));float length=.205f*scale;
                    heelAnchor[side]=point-direction*(length*pivot);toeAnchor[side]=heelAnchor[side]+direction*length;ready[side]=true;
                    heelAnchor[side].y=toeAnchor[side].y=root.position.y;
                }
                // Motor acceleration or state blending can ask for a planted point beyond this pose's reach.
                // Release the anchor gradually instead of snapping a nearly straight knee.
                Vector3 currentPoint=c.TransformPoint(Vector3.Lerp(heel,toe,pivot));
                Vector3 desiredPoint=Vector3.Lerp(heelAnchor[side],toeAnchor[side],pivot);
                Vector3 error=Vector3.ProjectOnPlane(desiredPoint-currentPoint,Vector3.up);float limit=.002f*scale;
                if(error.magnitude>limit){Vector3 release=error.normalized*(error.magnitude-limit);heelAnchor[side]-=release;toeAnchor[side]-=release;LastAnchorRelease=Mathf.Max(LastAnchorRelease,release.magnitude);}
                Vector3 offset=c.TransformVector(Vector3.Lerp(heel,toe,pivot));
                Vector3 contact=Vector3.Lerp(heelAnchor[side],toeAnchor[side],pivot),target=contact-offset;
                float verticalError=target.y-c.position.y,verticalRelease=verticalError-Mathf.Clamp(verticalError,-limit,limit);
                if(Mathf.Abs(verticalRelease)>.000001f){heelAnchor[side].y-=verticalRelease;toeAnchor[side].y-=verticalRelease;contact.y-=verticalRelease;target.y-=verticalRelease;LastAnchorRelease=Mathf.Max(LastAnchorRelease,Mathf.Abs(verticalRelease));}
                Quaternion qa=a.rotation,qb=b.rotation,qc=c.rotation;Vector3 head=a.position,delta=target-head;
                float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,c.position);
                float sourceBend=Vector3.Angle(b.position-a.position,c.position-b.position),minimumBend=Mathf.Min(14,sourceBend);
                float reach=Mathf.Sqrt(l1*l1+l2*l2+2*l1*l2*Mathf.Cos(minimumBend*Mathf.Deg2Rad));
                if(delta.magnitude>reach){Vector3 shift=head+delta.normalized*reach-target;target+=shift;heelAnchor[side]+=shift;toeAnchor[side]+=shift;contact+=shift;delta=target-head;LastAnchorRelease=Mathf.Max(LastAnchorRelease,shift.magnitude);}
                float d=Mathf.Clamp(delta.magnitude,.001f,reach);
                Vector3 axis=delta.normalized,pole=Vector3.ProjectOnPlane(b.position-head,axis).normalized;
                if(pole.sqrMagnitude<.1f)pole=Vector3.ProjectOnPlane(root.forward,axis).normalized;
                float along=(l1*l1+d*d-l2*l2)/(2*d);
                Vector3 knee=head+axis*along+pole*Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
                a.rotation=Quaternion.FromToRotation(b.position-head,knee-head)*a.rotation;b.rotation=Quaternion.FromToRotation(c.position-b.position,target-b.position)*b.rotation;
                Quaternion sa=a.rotation,sb=b.rotation;a.rotation=Quaternion.Slerp(qa,sa,weight);b.rotation=Quaternion.Slerp(qb,sb,weight);c.rotation=qc;
                if(weight>.99f)LastSupportSlip=Mathf.Max(LastSupportSlip,Vector3.ProjectOnPlane(c.TransformPoint(Vector3.Lerp(heel,toe,pivot))-contact,Vector3.up).magnitude);
            }
        }
    }
}
