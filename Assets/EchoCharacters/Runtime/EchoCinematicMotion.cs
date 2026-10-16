using UnityEngine;
namespace EchoCharacters
{
    /// <summary>Optional adapter for normalized cutscene paths. Does not read input.</summary>
    [DisallowMultipleComponent]
    public sealed class EchoCinematicMotion : MonoBehaviour
    {
        EchoLocomotion motion;float lastProgress=-1;Vector3 previous;
        void Awake(){motion=GetComponent<EchoLocomotion>();}
        public void Walk(Vector3 start,Vector3 end,float progress)
        {
            float t=Mathf.Clamp01(progress),position=DistanceFraction(t);
            Vector3 next=Vector3.Lerp(start,end,position);
            bool reset=lastProgress<0||t<lastProgress;
            Vector3 velocity=!reset&&Time.deltaTime>0?(next-previous)/Time.deltaTime:Vector3.zero;
            transform.position=next;
            if(motion){motion.SetVelocity(velocity);motion.SetFacing(end-start);if(t>=1)motion.SetSpeed(0);}
            previous=next;lastProgress=t;
        }
        public void ResetPath(){lastProgress=-1;}
        static float DistanceFraction(float t)
        {
            // Integral of a smooth acceleration/constant speed/deceleration profile.
            const float a=.15f,b=.18f,area=1-(a+b)*.5f;
            if(t<a){float u=t/a;return a*(u*u*u-.5f*u*u*u*u)/area;}
            if(t<=1-b)return (t-a*.5f)/area;
            float v=(1-t)/b;return 1-b*(v*v*v-.5f*v*v*v*v)/area;
        }
    }
}
