using UnityEngine;

namespace EchoCharacters
{
    /// <summary>Visual API only. Does not read input, move the actor, or modify game systems.</summary>
    [DisallowMultipleComponent]
    public sealed class EchoCharacterView : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] GameObject glasses;
        [SerializeField] GameObject lenses;
        [SerializeField] bool isDouble;
        public Animator Animator => animator;
        public bool IsDouble => isDouble;
        public EchoFoot JumpLeadFoot => GetComponent<EchoJumpMotion>()?.JumpLeadFoot ?? EchoFoot.None;

        public void Configure(Animator target, GameObject frame, GameObject lens, bool dark)
        { animator = target; glasses = frame; lenses = lens; isDouble = dark; }

        public void SetGlassesVisible(bool visible)
        { if (glasses) glasses.SetActive(visible); if (lenses) lenses.SetActive(visible); }

        /// <param name="normalizedSpeed">0 = idle, 0.5 = walk, 1 = run.</param>
        public void SetLocomotion(float normalizedSpeed)
        { var locomotion=GetComponent<EchoLocomotion>();if(locomotion&&locomotion.enabled)locomotion.SetSpeed(normalizedSpeed);else if(animator)animator.SetFloat("Speed",Mathf.Clamp01(normalizedSpeed),.18f,Time.deltaTime); }

        public void SetTalking(bool talking)
        { if (animator) animator.SetBool("Talking", talking); }
        public void BeginJump(){GetComponent<EchoJumpMotion>()?.BeginJump();}
        public void BeginJump(Vector2 localVelocity){GetComponent<EchoJumpMotion>()?.BeginJump(localVelocity);}
        public void SetAirborne(bool value,float verticalVelocity=0){GetComponent<EchoJumpMotion>()?.SetAirborne(value,verticalVelocity);}
        public void Landed(float fallHeight){GetComponent<EchoJumpMotion>()?.Landed(fallHeight);}
        public void Landed(float fallHeight,Vector2 localVelocity){GetComponent<EchoJumpMotion>()?.Landed(fallHeight,localVelocity);}
        public void PlayGesture(string clip,float fade=.16f){var a=GetComponentInChildren<Animator>();if(a&&a.HasState(0,Animator.StringToHash("Base Layer."+clip)))a.CrossFadeInFixedTime(clip,fade,0,0);}
        public void HitReact(){GetComponent<EchoEndingMotion>()?.HitReact();}
        public void Startle(){GetComponent<EchoEndingMotion>()?.Startle();}
        public void WakeIdle(){PlayGesture("WakeIdle");}
        public void GazeUp(){PlayGesture("GazeUp");}
        public void ReachDoor(){PlayGesture("ReachDoor");}
        public void TalkGesture(int variant=1){SetTalking(true);PlayGesture("TalkGesture"+Mathf.Clamp(variant,1,3));}
        public void Nod(){PlayGesture("Nod");}
        public void ShakeHead(){PlayGesture("ShakeHead");}
        public void Sigh(){PlayGesture("Sigh");}
        public void ResolveIdle(){GetComponent<EchoEndingMotion>()?.ResolveIdle();}
        public void DoubleVanish(){GetComponent<EchoEndingMotion>()?.DoubleVanish();}
        public void SetRunning(bool enabled){GetComponent<EchoLocomotion>()?.SetRunning(enabled);}
        public void ReachOut()
        {if(animator)animator.CrossFadeInFixedTime("ReachOut",.15f,0,0);}
        public void Interact() { if (animator) animator.SetTrigger("Interact"); }
        public void LookAround() { if (animator) animator.SetTrigger("LookAround"); }
        public void Appear() { if (animator) animator.SetTrigger("Appear"); }
        public void Rush() { if (animator) animator.SetBool("Rushing", true); }
        public void StopRush() { if (animator) animator.SetBool("Rushing", false); }

        /// <summary>Hide head accessories/body for a first-person camera without modifying the rig.</summary>
        public void SetBodyVisible(bool visible)
        { foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.enabled = visible; }
    }
}
