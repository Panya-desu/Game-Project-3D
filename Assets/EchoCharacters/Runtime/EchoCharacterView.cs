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

        public void Configure(Animator target, GameObject frame, GameObject lens, bool dark)
        { animator = target; glasses = frame; lenses = lens; isDouble = dark; }

        public void SetGlassesVisible(bool visible)
        { if (glasses) glasses.SetActive(visible); if (lenses) lenses.SetActive(visible); }

        /// <param name="normalizedSpeed">0 = idle, 0.5 = walk, 1 = run.</param>
        public void SetLocomotion(float normalizedSpeed)
        { var locomotion=GetComponent<EchoLocomotion>();if(locomotion&&locomotion.enabled)locomotion.SetSpeed(normalizedSpeed);else if(animator)animator.SetFloat("Speed",Mathf.Clamp01(normalizedSpeed),.18f,Time.deltaTime); }

        public void SetTalking(bool talking)
        { if (animator) animator.SetBool("Talking", talking); }
        bool airborne;
        public void SetAirborne(bool value,float verticalVelocity=0)
        {
            if(!animator||value==airborne)return;
            airborne=value;string state=value?(verticalVelocity>0?"JumpStart":"FallLoop"):"Land";
            int hash=Animator.StringToHash("Base Layer."+state);
            if(animator.HasState(0,hash))animator.CrossFadeInFixedTime(hash,.10f,0,0);
        }
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
