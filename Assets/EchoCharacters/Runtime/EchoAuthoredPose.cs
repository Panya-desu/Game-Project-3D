using System.Collections.Generic;
using UnityEngine;
namespace EchoCharacters
{
    /// <summary>Retain authored body FK for this character's own clips while keeping a valid Humanoid Avatar.
    /// Head/eye IK remains controlled by the game. Other animation assets use normal Humanoid retargeting.</summary>
    [DefaultExecutionOrder(9990),RequireComponent(typeof(Animator)),DisallowMultipleComponent]
    public sealed class EchoAuthoredPose : MonoBehaviour
    {
        [SerializeField] Transform[] targets,references;
        [SerializeField] AnimationClip[] supportedClips;
        Animator animator;HashSet<AnimationClip> supported;
        readonly List<AnimatorClipInfo> current=new List<AnimatorClipInfo>(),next=new List<AnimatorClipInfo>();
        Quaternion neckBeforeIK,headBeforeIK;Transform neck,head;int ikFrame=-1;EchoJumpMotion jump;
        public int AppliedFrame {get;private set;}=-1;
        public int TransitionHoldFrames {get;private set;}
        public float LastHeadIKDelta {get;private set;}
        public float LastNeckIKDelta {get;private set;}
        public void Configure(Transform[] destination,Transform[] source,AnimationClip[] clips)
        {targets=destination;references=source;supportedClips=clips;}
        void Awake(){animator=GetComponent<Animator>();jump=GetComponentInParent<EchoJumpMotion>();if(jump)jump.CacheHemBones(targets);supported=new HashSet<AnimationClip>(supportedClips??new AnimationClip[0]);if(animator.isHuman){neck=animator.GetBoneTransform(HumanBodyBones.Neck);head=animator.GetBoneTransform(HumanBodyBones.Head);}}
        void OnAnimatorIK(int layer)
        {if(layer!=0||!neck||!head)return;neckBeforeIK=neck.localRotation;headBeforeIK=head.localRotation;ikFrame=Time.frameCount;}
        void LateUpdate()
        {
            if(!animator||!animator.isActiveAndEnabled||targets==null||references==null)return;
            current.Clear();next.Clear();animator.GetCurrentAnimatorClipInfo(0,current);bool managed=false;
            foreach(var clip in current)if(supported.Contains(clip.clip)){managed=true;break;}
            if(!managed&&animator.IsInTransition(0))
            {next.Clear();animator.GetNextAnimatorClipInfo(0,next);foreach(var clip in next)if(supported.Contains(clip.clip)){managed=true;break;}}
            if(!managed&&current.Count==0&&next.Count==0&&AppliedFrame==Time.frameCount-1){managed=true;TransitionHoldFrames++;}
            if(!managed)return;
            Quaternion neckDelta=Quaternion.identity,headDelta=Quaternion.identity;
            if(ikFrame==Time.frameCount){neckDelta=Quaternion.Inverse(neckBeforeIK)*neck.localRotation;headDelta=Quaternion.Inverse(headBeforeIK)*head.localRotation;}
            LastHeadIKDelta=Quaternion.Angle(Quaternion.identity,headDelta);LastNeckIKDelta=Quaternion.Angle(Quaternion.identity,neckDelta);
            for(int i=0;i<targets.Length;i++)
            {if(!targets[i]||!references[i])continue;targets[i].localPosition=references[i].localPosition;targets[i].localRotation=references[i].localRotation*(targets[i]==neck?neckDelta:targets[i]==head?headDelta:Quaternion.identity);targets[i].localScale=references[i].localScale;}
            AppliedFrame=Time.frameCount;
            if(jump)jump.ApplyMovingAnticipation();
        }
    }
}
