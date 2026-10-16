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
        public int AppliedFrame {get;private set;}=-1;
        public void Configure(Transform[] destination,Transform[] source,AnimationClip[] clips)
        {targets=destination;references=source;supportedClips=clips;}
        void Awake(){animator=GetComponent<Animator>();supported=new HashSet<AnimationClip>(supportedClips??new AnimationClip[0]);}
        void LateUpdate()
        {
            if(!animator||!animator.isActiveAndEnabled||targets==null||references==null)return;
            current.Clear();animator.GetCurrentAnimatorClipInfo(0,current);bool managed=false;
            foreach(var clip in current)if(supported.Contains(clip.clip)){managed=true;break;}
            if(!managed&&animator.IsInTransition(0))
            {next.Clear();animator.GetNextAnimatorClipInfo(0,next);foreach(var clip in next)if(supported.Contains(clip.clip)){managed=true;break;}}
            if(!managed)return;
            for(int i=0;i<targets.Length;i++)
            {if(!targets[i]||!references[i])continue;targets[i].localPosition=references[i].localPosition;targets[i].localRotation=references[i].localRotation;targets[i].localScale=references[i].localScale;}
            AppliedFrame=Time.frameCount;
        }
    }
}
