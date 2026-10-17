using System;
using UnityEngine;
namespace EchoCharacters
{
    /// <summary>Optional sound hooks. Events are on the Animator object, with no scene dependency.</summary>
    public sealed class EchoAnimationEvents:MonoBehaviour
    {
        public event Action LeftFoot,RightFoot,Takeoff,Landing;
        readonly System.Collections.Generic.List<AnimatorClipInfo> current=new System.Collections.Generic.List<AnimatorClipInfo>(32),next=new System.Collections.Generic.List<AnimatorClipInfo>(32);
        Animator animator;AnimationClip dominant;int checkedFrame=-1;
        public int AcceptedFootEvents {get;private set;}
        public int SuppressedFootEvents {get;private set;}
        void Awake(){animator=GetComponent<Animator>();}
        bool Accept(AnimationEvent data)
        {
            // Procedural turn steps call these hooks without an AnimationEvent.
            if(data==null)return true;
            if(!animator||!data.animatorClipInfo.clip||data.animatorClipInfo.weight<=.0001f)return false;
            if(checkedFrame!=Time.frameCount)
            {
                checkedFrame=Time.frameCount;dominant=null;float best=0,transition=0;
                current.Clear();next.Clear();animator.GetCurrentAnimatorClipInfo(0,current);
                if(animator.IsInTransition(0)){transition=Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime);animator.GetNextAnimatorClipInfo(0,next);}
                Choose(current,1-transition,ref best);Choose(next,transition,ref best);
            }
            return data.animatorClipInfo.clip==dominant;
        }
        void Choose(System.Collections.Generic.List<AnimatorClipInfo> clips,float stateWeight,ref float best)
        {foreach(var item in clips){float weight=item.weight*stateWeight;if(weight>.0001f&&(weight>best+.00001f||(Mathf.Abs(weight-best)<=.00001f&&dominant&&item.clip.GetInstanceID()<dominant.GetInstanceID()))){best=weight;dominant=item.clip;}}}
        public void FootL(AnimationEvent data=null){if(Accept(data)){AcceptedFootEvents++;LeftFoot?.Invoke();}else SuppressedFootEvents++;}
        public void FootR(AnimationEvent data=null){if(Accept(data)){AcceptedFootEvents++;RightFoot?.Invoke();}else SuppressedFootEvents++;}
        // These are single-state lifecycle events, not blend-tree footfalls.
        public void JumpTakeoff(AnimationEvent data=null){Takeoff?.Invoke();}
        public void Land(AnimationEvent data=null){Landing?.Invoke();}
    }
}
