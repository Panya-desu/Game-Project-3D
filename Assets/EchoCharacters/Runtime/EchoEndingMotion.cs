using UnityEngine;
namespace EchoCharacters
{
    /// <summary>Opt-in, scaled retreat for the in-place HitReact clip. The recovered position is retained.</summary>
    [DefaultExecutionOrder(9985),DisallowMultipleComponent]
    public sealed class EchoEndingMotion:MonoBehaviour
    {
        Animator animator;bool reacting,vanishing;Vector3 backward;float applied;string reactState="HitReact";
        public bool IsReacting=>reacting||vanishing;
        public event System.Action<Vector3> MotorDisplacement;
        public Vector3 LastMotorDelta {get;private set;}
        public const float RetreatDistance=.16f;
        void Awake(){animator=GetComponentInChildren<Animator>();}
        public static float Retreat(float seconds)
        {return RetreatDistance*(.56f*EchoLocomotion.Ease((seconds-.10f)/.45f)+.44f*EchoLocomotion.Ease((seconds-.55f)/.50f));}
        public void HitReact()
        {
            reactState="HitReact";vanishing=false;
            if(!animator)return;var motion=GetComponent<EchoLocomotion>();if(motion)motion.ResetMotion();
            animator.SetBool("Talking",false);animator.SetBool("Rushing",false);applied=0;backward=-transform.forward*Mathf.Abs(transform.lossyScale.y);reacting=true;
            animator.CrossFadeInFixedTime("HitReact",.14f,0,0);
        }
        public void Startle(){HitReact();reactState="Startle";animator.CrossFadeInFixedTime("Startle",.08f,0,0);}
        public void ResolveIdle()
        {if(!animator)return;if(reacting){LastMotorDelta=backward*(RetreatDistance-applied);MotorDisplacement?.Invoke(LastMotorDelta);applied=RetreatDistance;}reacting=vanishing=false;animator.SetBool("Talking",false);animator.CrossFadeInFixedTime("ResolveIdle",.18f,0,0);}
        public void DoubleVanish()
        {if(!animator)return;GetComponent<EchoLocomotion>()?.ResetMotion();reacting=false;vanishing=true;animator.SetBool("Rushing",false);animator.SetBool("Talking",false);animator.CrossFadeInFixedTime("DoubleVanish",.18f,0,0);}
        void LateUpdate()
        {
            if(!reacting||!animator)return;
            var state=animator.GetCurrentAnimatorStateInfo(0);var next=animator.GetNextAnimatorStateInfo(0);
            if(state.IsName(reactState)||animator.IsInTransition(0)&&next.IsName(reactState))
            {
                var hit=state.IsName(reactState)?state:next;float distance=Retreat(Mathf.Clamp01(hit.normalizedTime)*1.2f);
                LastMotorDelta=backward*(distance-applied);MotorDisplacement?.Invoke(LastMotorDelta);applied=distance;
            }
            else if(state.IsName("ResolveIdle")||state.IsName("Locomotion"))
            {LastMotorDelta=backward*(RetreatDistance-applied);MotorDisplacement?.Invoke(LastMotorDelta);applied=RetreatDistance;reacting=false;}
        }
        void OnDisable(){reacting=vanishing=false;applied=0;}
    }
}
