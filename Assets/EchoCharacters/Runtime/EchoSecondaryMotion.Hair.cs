using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoCharacters
{
    public sealed partial class EchoSecondaryMotion
    {
        [Header("Layered hair")]
        [Range(1,8)] public float hairFrequency=3.2f;
        [Range(.3f,1.2f)] public float hairDamping=.78f;
        [Range(0,1)] public float hairInertia=.18f;
        public bool hairCollision=true;
        public float HairMaxObservedBend {get;private set;}
        public float HairSimulationMilliseconds {get;private set;}
        public int HairCollisionClampCount {get;private set;}
        public int HairResetCount {get;private set;}
        public int HairProbeCount {get;private set;}
        public int HairJointCount {get {return hairSprings.Count;}}
        [Serializable] public class HairProfile {public HairJointSpec[] joints;public HairProbeSpec[] probes;public HairCapsuleSpec[] capsules;}
        [Serializable] public class HairJointSpec {public string name;public float length;public int segment;}
        [Serializable] public class HairProbeSpec {public string owner;public string[] bones;public Vector3[] local;public float[] weights;}
        [Serializable] public class HairCapsuleSpec {public string bone;public Vector3 a,b;public float radius;}
        sealed class HairSpring {public Transform bone;public Vector3 axis,tip,velocity,anchor;public Quaternion animated;public float length,limit;public bool ready;public readonly List<int> probes=new List<int>();}
        sealed class HairProbe {public Transform[] bones;public Vector3[] local;public float[] weights;}
        sealed class HairCapsule {public Transform bone;public Vector3 a,b,worldA,worldB;public float radius,worldRadius;}
        static HairProfile sharedHairProfile;
        readonly List<HairSpring> hairSprings=new List<HairSpring>();
        HairProbe[] hairProbes;HairCapsule[] hairCapsules;float[,] hairClearance;
        bool hairSetup;Transform hairHead;Vector3 hairLastPosition,hairLastVelocity,hairAcceleration;Quaternion hairLastRotation;
        void ResetHairBinding(){RestoreHairPose();hairSprings.Clear();hairSetup=false;hairHead=null;hairLastVelocity=hairAcceleration=Vector3.zero;}
        public void ResetHairMotion(){foreach(var j in hairSprings)j.ready=false;hairLastVelocity=hairAcceleration=Vector3.zero;HairResetCount++;}
        void RestoreHairPose(){foreach(var j in hairSprings)if(j.bone)j.bone.localRotation=j.animated;}
        void InitializeHairLazy()
        {
            hairSetup=true;
            if(sharedHairProfile==null){var asset=Resources.Load<TextAsset>("EchoHairMotionProfile");if(!asset)return;sharedHairProfile=JsonUtility.FromJson<HairProfile>(asset.text);}
            var transforms=new Dictionary<string,Transform>();foreach(var t in GetComponentsInChildren<Transform>(true))if(!transforms.ContainsKey(t.name))transforms.Add(t.name,t);
            if(!transforms.TryGetValue("Head",out hairHead))return;
            foreach(var spec in sharedHairProfile.joints)
            {
                if(!transforms.TryGetValue(spec.name,out var bone))continue;
                Vector3 axis=Vector3.up;foreach(Transform child in bone)if(child.name.StartsWith("HairFlow",StringComparison.Ordinal)){axis=child.localPosition.normalized;break;}
                hairSprings.Add(new HairSpring{bone=bone,axis=axis,length=spec.length,limit=spec.segment==0?.22f:spec.segment==1?.55f:1f,animated=bone.localRotation});
            }
            hairSprings.Sort((a,b)=>Depth(a.bone).CompareTo(Depth(b.bone)));
            var ps=new List<HairProbe>();var owners=new List<Transform>();
            foreach(var spec in sharedHairProfile.probes)
            {
                if(!transforms.TryGetValue(spec.owner,out var owner))continue;var bs=new Transform[spec.bones.Length];bool valid=true;
                for(int i=0;i<bs.Length;i++)if(!transforms.TryGetValue(spec.bones[i],out bs[i]))valid=false;
                if(!valid)continue;ps.Add(new HairProbe{bones=bs,local=spec.local,weights=spec.weights});owners.Add(owner);
            }
            hairProbes=ps.ToArray();HairProbeCount=hairProbes.Length;
            foreach(var j in hairSprings)for(int i=0;i<owners.Count;i++)if(owners[i]==j.bone||owners[i].IsChildOf(j.bone))j.probes.Add(i);
            var caps=new List<HairCapsule>();foreach(var spec in sharedHairProfile.capsules)if(transforms.TryGetValue(spec.bone,out var bone))caps.Add(new HairCapsule{bone=bone,a=spec.a,b=spec.b,radius=spec.radius});
            hairCapsules=caps.ToArray();hairClearance=new float[hairProbes.Length,hairCapsules.Length];hairLastPosition=hairHead.position;hairLastRotation=hairHead.rotation;
        }
        Vector3 HairProbePoint(int index){var p=hairProbes[index];var result=Vector3.zero;for(int i=0;i<p.bones.Length;i++)result+=p.bones[i].TransformPoint(p.local[i])*p.weights[i];return result;}
        static float HairDistance(Vector3 p,HairCapsule cap)
        {
            Vector3 a=cap.worldA,ab=cap.worldB-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,ab)/Mathf.Max(1e-9f,ab.sqrMagnitude));return (p-a-t*ab).magnitude-cap.worldRadius;
        }
        bool HairPoseSafe(HairSpring j,float scale)
        {
            foreach(int i in j.probes){Vector3 p=HairProbePoint(i);for(int c=0;c<hairCapsules.Length;c++){float baseline=hairClearance[i,c];float minimum=baseline<.03f*scale?baseline-.00015f*scale:.001f*scale;if(HairDistance(p,hairCapsules[c])<minimum)return false;}}return true;
        }
        void SimulateHair(float dt,bool teleport)
        {
            long start=System.Diagnostics.Stopwatch.GetTimestamp();
            if(!hairSetup)InitializeHairLazy();if(!hairHead||hairSprings.Count==0)return;
            float scale=Mathf.Abs(hairHead.lossyScale.y);
            bool reset=teleport||Time.deltaTime>.10f||Quaternion.Angle(hairLastRotation,hairHead.rotation)>75;
            Vector3 velocity=(hairHead.position-hairLastPosition)/Mathf.Max(dt,.0001f);Vector3 acceleration=(velocity-hairLastVelocity)/Mathf.Max(dt,.0001f);
            hairAcceleration=Vector3.Lerp(hairAcceleration,Vector3.ClampMagnitude(acceleration,30f*scale),1-Mathf.Exp(-dt*12));hairLastVelocity=velocity;hairLastPosition=hairHead.position;hairLastRotation=hairHead.rotation;
            foreach(var j in hairSprings)j.animated=j.bone.localRotation;
            if(hairCollision){foreach(var cap in hairCapsules){cap.worldA=cap.bone.TransformPoint(cap.a);cap.worldB=cap.bone.TransformPoint(cap.b);cap.worldRadius=cap.radius*Mathf.Abs(cap.bone.lossyScale.y);}for(int i=0;i<hairProbes.Length;i++){Vector3 p=HairProbePoint(i);for(int c=0;c<hairCapsules.Length;c++)hairClearance[i,c]=HairDistance(p,hairCapsules[c]);}}
            foreach(var j in hairSprings)
            {
                Vector3 head=j.bone.position,axis=j.bone.TransformDirection(j.axis).normalized;float length=j.length*scale;Vector3 rest=head+axis*length;
                if(!j.ready||reset){j.tip=rest;j.anchor=head;j.velocity=Vector3.zero;j.ready=true;continue;}
                Vector3 p=j.tip+(head-j.anchor),v=j.velocity;float frequency=hairFrequency*Mathf.Lerp(1.3f,.85f,j.limit),omega=2*Mathf.PI*frequency;
                int count=Mathf.Clamp(Mathf.CeilToInt(dt*240),1,16);float h=dt/count;
                for(int k=0;k<count;k++){v+=((rest-p)*(omega*omega)-2*hairDamping*omega*v-hairAcceleration*hairInertia+Vector3.down*(.08f*scale))*h;p+=v*h;}
                Vector3 direction=(p-head).normalized;if(direction.sqrMagnitude<.1f)direction=axis;
                float angle=Vector3.Angle(axis,direction),limit=hairMaxAngle*j.limit;if(angle>limit)direction=Vector3.Slerp(axis,direction,limit/angle).normalized;
                Quaternion original=j.bone.rotation,proposal=Quaternion.FromToRotation(axis,direction)*original;j.bone.rotation=proposal;
                if(hairCollision&&!HairPoseSafe(j,scale))
                {
                    HairCollisionClampCount++;bool safe=false;
                    foreach(float fraction in HairFractions){j.bone.rotation=Quaternion.Slerp(original,proposal,fraction);if(HairPoseSafe(j,scale)){safe=true;break;}}
                    if(!safe)j.bone.rotation=original;
                }
                Vector3 actual=j.bone.TransformDirection(j.axis).normalized;j.tip=head+actual*length;j.anchor=head;j.velocity=Vector3.ClampMagnitude(Vector3.ProjectOnPlane(v,actual),.45f*scale);
                HairMaxObservedBend=Mathf.Max(HairMaxObservedBend,Vector3.Angle(axis,actual));
            }
            HairSimulationMilliseconds=(float)((System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency);
        }
        static readonly float[] HairFractions={.75f,.5f,.25f,0f};
    }
}
