using UnityEngine;
namespace EchoCharacters
{
    /// <summary>Authored lower-body and garment turn poses, followed by world-space foot planting.</summary>
    [DefaultExecutionOrder(9995),RequireComponent(typeof(Animator))]
    public sealed class EchoTurnFootIK:MonoBehaviour
    {
        [System.Serializable] public sealed class ClothProfile {public float angle;public int frames;public Quaternion[] rotations;public Vector3[] positions;}
        [SerializeField,HideInInspector] Transform[] clothBones=new Transform[0];
        [SerializeField,HideInInspector] ClothProfile[] clothProfiles=new ClothProfile[0];
        public void ConfigureCloth(Transform[] bones,ClothProfile[] profiles){clothBones=bones;clothProfiles=profiles;}
        Animator animator;EchoLocomotion locomotion;EchoAnimationEvents events;int serial=-1;readonly bool[] strikes=new bool[3];
        readonly Transform[] upper=new Transform[2],lower=new Transform[2],foot=new Transform[2];
        readonly Vector3[] start=new Vector3[2];readonly Quaternion[] rotation=new Quaternion[2];
        Quaternion[] retargetRotations;Vector3[] retargetPositions;bool retargetPending,retargeting;
        public void PrepareRetarget()
        {
            if(retargetRotations==null)return;
            for(int i=0;i<clothBones.Length;i++)if(clothBones[i]){retargetRotations[i]=clothBones[i].localRotation;retargetPositions[i]=clothBones[i].localPosition;}
            for(int side=0;side<2;side++)if(foot[side]){start[side]=foot[side].position;rotation[side]=foot[side].rotation;}
            retargetPending=true;
        }
        void Awake()
        {
            animator=GetComponent<Animator>();events=GetComponent<EchoAnimationEvents>();locomotion=GetComponentInParent<EchoLocomotion>();retargetRotations=new Quaternion[clothBones.Length];retargetPositions=new Vector3[clothBones.Length];if(!animator.isHuman)return;
            for(int side=0;side<2;side++)
            {upper[side]=animator.GetBoneTransform(side==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);lower[side]=animator.GetBoneTransform(side==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);foot[side]=animator.GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);}
        }
        void LateUpdate()
        {
            if(!locomotion||!locomotion.Turning||!animator.isHuman)return;
            if(serial!=locomotion.TurnSerial)
            {
                serial=locomotion.TurnSerial;System.Array.Clear(strikes,0,strikes.Length);retargeting=retargetPending;
                if(!retargetPending)for(int side=0;side<2;side++){start[side]=foot[side].position;rotation[side]=foot[side].rotation;}
                retargetPending=false;
            }
            float t=locomotion.TurnProgress,weight=EchoLocomotion.Ease(t/.06f)*EchoLocomotion.Ease((1-t)/.06f);
            float retargetBlend=EchoLocomotion.Ease(t*locomotion.TurnDurationSeconds/.20f);
            if(retargeting&&retargetBlend<1)for(int i=0;i<clothBones.Length;i++)if(clothBones[i]){clothBones[i].localRotation=Quaternion.Slerp(retargetRotations[i],clothBones[i].localRotation,retargetBlend);clothBones[i].localPosition=Vector3.Lerp(retargetPositions[i],clothBones[i].localPosition,retargetBlend);}
            if(retargeting)weight=Mathf.Min(weight,retargetBlend);
            ApplyCloth(t,weight);
            int count=Mathf.Abs(locomotion.TurnAmount)>135?3:2;
            for(int k=0;k<count;k++)if(!strikes[k]&&t*count-k>=.94f){strikes[k]=true;bool left=(k%2==0)==(locomotion.TurnAmount<0);if(events){if(left)events.FootL();else events.FootR();}}
            var yaw=Quaternion.Euler(0,locomotion.TurnAmount,0);float scale=Mathf.Abs(locomotion.transform.lossyScale.y);
            for(int side=0;side<2;side++)
            {
                bool first=side==0?locomotion.TurnAmount<0:locomotion.TurnAmount>=0;
                int steps=Mathf.Abs(locomotion.TurnAmount)>135?3:2;float theta=0,lift=0;
                for(int k=0;k<steps;k++)if((k%2==0)==first&&t*steps>=k){float u=Mathf.Clamp01(t*steps-k);float from=k<2?0:locomotion.TurnAmount*.5f,to=steps==3&&k==0?locomotion.TurnAmount*.5f:locomotion.TurnAmount;theta=Mathf.Lerp(from,to,EchoLocomotion.Ease(u));lift=.045f*scale*Mathf.Pow(Mathf.Sin(Mathf.PI*u),2);}
                var heading=Quaternion.Euler(0,theta,0);Vector3 goal=locomotion.TurnPivot+heading*(start[side]-locomotion.TurnPivot)+Vector3.up*lift;
                Solve(side,goal,heading*rotation[side],weight);
            }
        }
        void ApplyCloth(float t,float weight)
        {
            if(clothBones.Length==0||clothProfiles.Length==0)return;
            float amount=locomotion.TurnAmount,wanted=Mathf.Abs(amount)<67.5f?45:Mathf.Abs(amount)<135?90:180;ClothProfile profile=null,second=null;float angleBlend=0;
            foreach(var p in clothProfiles)if(Mathf.Sign(p.angle)==Mathf.Sign(amount)&&Mathf.Abs(p.angle)==wanted){profile=p;break;}
            if(Mathf.Abs(amount)>45&&Mathf.Abs(amount)<90)
            {foreach(var p in clothProfiles)if(Mathf.Sign(p.angle)==Mathf.Sign(amount)){if(Mathf.Abs(p.angle)==45)profile=p;else if(Mathf.Abs(p.angle)==90)second=p;}angleBlend=(Mathf.Abs(amount)-45)/45;}
            if(profile==null||profile.frames<2)return;int count=clothBones.Length;
            for(int i=0;i<count;i++)if(clothBones[i])
            {
                Sample(profile,t,count,i,out var q,out var p);var firstQ=profile.rotations[i];var firstP=profile.positions[i];
                if(second!=null){Sample(second,t,count,i,out var q2,out var p2);q=Quaternion.Slerp(q,q2,angleBlend);p=Vector3.Lerp(p,p2,angleBlend);firstQ=Quaternion.Slerp(firstQ,second.rotations[i],angleBlend);firstP=Vector3.Lerp(firstP,second.positions[i],angleBlend);}
                if(clothBones[i].name=="Hips")
                {q=clothBones[i].localRotation*Quaternion.Inverse(firstQ)*q;p=clothBones[i].localPosition+p-firstP;}
                clothBones[i].localRotation=Quaternion.Slerp(clothBones[i].localRotation,q,weight);clothBones[i].localPosition=Vector3.Lerp(clothBones[i].localPosition,p,weight);
            }
        }
        static void Sample(ClothProfile profile,float t,int count,int index,out Quaternion q,out Vector3 p)
        {float frame=Mathf.Clamp01(t)*(profile.frames-1);int a=Mathf.FloorToInt(frame),b=Mathf.Min(a+1,profile.frames-1);float u=frame-a;q=Quaternion.Slerp(profile.rotations[a*count+index],profile.rotations[b*count+index],u);p=Vector3.Lerp(profile.positions[a*count+index],profile.positions[b*count+index],u);}
        void Solve(int side,Vector3 target,Quaternion targetRotation,float weight)
        {
            var a=upper[side];var b=lower[side];var c=foot[side];if(!a||!b||!c)return;
            Quaternion qa=a.rotation,qb=b.rotation,qc=c.rotation;Vector3 head=a.position,delta=target-head;
            float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,c.position),distance=Mathf.Clamp(delta.magnitude,.001f,l1+l2-.0001f);
            Vector3 axis=delta.normalized,pole=Vector3.ProjectOnPlane(b.position-head,axis).normalized;
            if(pole.sqrMagnitude<.1f)pole=Vector3.ProjectOnPlane(locomotion.transform.forward,axis).normalized;
            float along=(l1*l1+distance*distance-l2*l2)/(2*distance);
            Vector3 knee=head+axis*along+pole*Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
            a.rotation=Quaternion.FromToRotation(b.position-head,knee-head)*a.rotation;
            b.rotation=Quaternion.FromToRotation(c.position-b.position,target-b.position)*b.rotation;
            Quaternion solvedA=a.rotation,solvedB=b.rotation;
            a.rotation=Quaternion.Slerp(qa,solvedA,weight);b.rotation=Quaternion.Slerp(qb,solvedB,weight);c.rotation=Quaternion.Slerp(qc,targetRotation,weight);
        }
    }
}
