using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoCharacters
{
    /// <summary>Spring chains for the v5 rig, with shirt-over-skirt layering guards. Disable legacy SkirtSpring,
    /// HairSway and shader sway before enabling this component.</summary>
    [DefaultExecutionOrder(10010)]
    public sealed partial class EchoSecondaryMotion : MonoBehaviour
    {
        [Range(0, 1)] public float drag = .28f;
        public bool layerGuard = true; // Kept for CharacterLook compatibility.
        // Game: the cloth contact data (shirt probes, skirt/hand/leg contacts) is built on the first frame that
        // needs it, and only when layerGuard is on. Building it in OnEnable cost ~350 ms per spawned character
        // (a stall at the start of every cutscene), and the per-frame contact checks ~4 ms per character.
        bool guardReady; Animator guardAnimator;
        public float stiffness = 65f;
        [HideInInspector] public int[] shirtOuterVertices,shirtHemRimVertices;
        [Range(0, 20)] public float skirtMaxAngle = 4f;
        [Range(0, 10)] public float hairMaxAngle = 4f;
        public float thighRadius = .085f;
        [Range(0, 6)] public float shirtMaxAngle = 3.5f;
        [Range(0, 2)] public float sleeveMaxAngle = 1.1f;
        [Range(0, 1)] public float collarMaxAngle = .25f;
        public float torsoRadius = .095f, upperArmRadius = .044f;
        public float LastShirtBend { get; private set; }
        public float MaxShirtProbeDisplacement { get; private set; }
        public int LayerContactFrames { get; private set; }
        public int UnresolvedLayerContacts { get; private set; }
        public float LastBend { get; private set; }
        sealed class Joint
        {
            public Transform bone;
            public Vector3 axis, tip, previous, velocity;
            public Quaternion animatedLocal;
            public float length, limit;
            public bool skirt, ready;
            public int cloth;
            public int[] affectedShirtMatrices;
            public readonly List<Probe> probes = new List<Probe>();
        }
        readonly List<Joint> joints = new List<Joint>();
        Transform leftHip, leftKnee, rightHip, rightKnee;
        Transform hips, chest, leftUpperArm, leftLowerArm, rightUpperArm, rightLowerArm;
        SkinnedMeshRenderer shirtRenderer, skirtRenderer;
        Vector3[] shirtVertices, skirtWorld;
        BoneWeight[] shirtWeights;
        Matrix4x4[] shirtBinds,skinMatrices;
        Transform[] shirtBones;
        int[] skirtTriangles;
        float[] triangleMin, triangleMax;
        Mesh skirtBake;
        sealed class Probe { public int vertex; public Vector3 reference; public float capsule, skirt, authoredSkirt, handGap, legGap; }
        static readonly float[] GuardFractions={1f,.75f,.5f,.25f,.125f,0f};
        readonly List<Vector3> skirtLocal=new List<Vector3>();
        readonly List<int>[] heightBins=new List<int>[48];
        float minHeight,maxHeight;
        public double LastSimulationMilliseconds {get;private set;}
        Vector3 Predict(Joint j,Vector3 target,Vector3 gravity,float dt)
        {
            int count=Mathf.Clamp(Mathf.CeilToInt(dt*120),1,8);float h=dt/count;
            float damping=-Mathf.Log(Mathf.Max(.001f,1-drag))*30;
            Vector3 p=j.tip,v=j.velocity;
            for(int i=0;i<count;i++)
            {v+=(target-p)*stiffness*h+gravity*h;v*=Mathf.Exp(-damping*h);p+=v*h;}
            j.velocity=v;return p;
        }
        Vector3 Up => transform.up.normalized;
        Vector3 previousRoot;
        // ENDPOINT_SPEC is generated from the Blender bone lengths, in metres.
        static readonly Dictionary<string, float> Lengths = new Dictionary<string, float>
        {
                        {"SkirtChain00_01", 0.106212010f},
            {"SkirtChain00_02", 0.112587755f},
            {"SkirtChain00_03", 0.105929241f},
            {"SkirtChain01_01", 0.106852660f},
            {"SkirtChain01_02", 0.112483345f},
            {"SkirtChain01_03", 0.105865500f},
            {"SkirtChain02_01", 0.107489493f},
            {"SkirtChain02_02", 0.112378836f},
            {"SkirtChain02_03", 0.105801720f},
            {"SkirtChain03_01", 0.106852661f},
            {"SkirtChain03_02", 0.112483344f},
            {"SkirtChain03_03", 0.105865501f},
            {"SkirtChain04_01", 0.106212010f},
            {"SkirtChain04_02", 0.112587757f},
            {"SkirtChain04_03", 0.105929241f},
            {"SkirtChain05_01", 0.106852661f},
            {"SkirtChain05_02", 0.112483344f},
            {"SkirtChain05_03", 0.105865501f},
            {"SkirtChain06_01", 0.107489493f},
            {"SkirtChain06_02", 0.112378836f},
            {"SkirtChain06_03", 0.105801720f},
            {"SkirtChain07_01", 0.106852660f},
            {"SkirtChain07_02", 0.112483345f},
            {"SkirtChain07_03", 0.105865500f},
            {"HairBangLeft_01", 0.052735157f},
            {"HairBangLeft_02", 0.049132436f},
            {"HairBangLeft_03", 0.046076098f},
            {"HairSideLeft_01", 0.088147640f},
            {"HairSideLeft_02", 0.086353844f},
            {"HairSideLeft_03", 0.086052346f},
            {"HairBackLeft_01", 0.088515570f},
            {"HairBackLeft_02", 0.085732062f},
            {"HairBackLeft_03", 0.086180081f},
            {"RibbonWingLeft_01", 0.023000000f},
            {"RibbonWingLeft_02", 0.028035688f},
            {"RibbonTailLeft_01", 0.030331491f},
            {"RibbonTailLeft_02", 0.033030305f},
            {"EarringLeft_01", 0.010198030f},
            {"EarringLeft_02", 0.012041678f},
            {"HairBangRight_01", 0.052735157f},
            {"HairBangRight_02", 0.049132436f},
            {"HairBangRight_03", 0.046076098f},
            {"HairSideRight_01", 0.088147640f},
            {"HairSideRight_02", 0.086353844f},
            {"HairSideRight_03", 0.086052346f},
            {"HairBackRight_01", 0.088515570f},
            {"HairBackRight_02", 0.085732062f},
            {"HairBackRight_03", 0.086180081f},
            {"RibbonWingRight_01", 0.023000000f},
            {"RibbonWingRight_02", 0.028035688f},
            {"RibbonTailRight_01", 0.030331491f},
            {"RibbonTailRight_02", 0.033030305f},
            {"EarringRight_01", 0.010198030f},
            {"EarringRight_02", 0.012041678f},

            {"ShirtHemFrontLeft_01", 0.078143477f},
            {"ShirtHemFrontLeft_02", 0.083152507f},
            {"ShirtHemSideLeft_01", 0.078526960f},
            {"ShirtHemSideLeft_02", 0.092267792f},
            {"ShirtHemBackLeft_01", 0.079165681f},
            {"ShirtHemBackLeft_02", 0.089921807f},
            {"ShirtHemBackRight_01", 0.079165681f},
            {"ShirtHemBackRight_02", 0.089921809f},
            {"ShirtHemSideRight_01", 0.078526967f},
            {"ShirtHemSideRight_02", 0.092267821f},
            {"ShirtHemFrontRight_01", 0.078143505f},
            {"ShirtHemFrontRight_02", 0.083152506f},
            {"SleeveLeft_01", 0.012000009f},
            {"SleeveLeft_02", 0.012999996f},
            {"ShirtCollarLeft_01", 0.028372508f},
            {"SleeveRight_01", 0.012000009f},
            {"SleeveRight_02", 0.012999996f},
            {"ShirtCollarRight_01", 0.028372508f},
        };
        void OnEnable()
        {
            joints.Clear();
            var animator = GetComponentInChildren<Animator>();
            if (!animator) animator = GetComponentInParent<Animator>();
            if (animator && animator.isHuman)
            {
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                leftHip = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                rightHip = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            }
            foreach (var bone in GetComponentsInChildren<Transform>(true))
            {
                if (!Lengths.TryGetValue(bone.name, out float length)) continue;
                Vector3 axis = Vector3.up;
                foreach (Transform child in bone)
                    if (Lengths.ContainsKey(child.name)) { axis = child.localPosition.normalized; break; }
                bool skirt = bone.name.StartsWith("SkirtChain", StringComparison.Ordinal);
                int cloth = bone.name.StartsWith("ShirtHem",StringComparison.Ordinal) ? 1 : bone.name.StartsWith("Sleeve",StringComparison.Ordinal) ? 2 : bone.name.StartsWith("ShirtCollar",StringComparison.Ordinal) ? 3 : 0;
                joints.Add(new Joint { bone = bone, axis = axis, length = length, cloth = cloth,
                    animatedLocal = bone.localRotation, skirt = skirt,
                    limit = cloth==1 ? shirtMaxAngle : cloth==2 ? sleeveMaxAngle : cloth==3 ? collarMaxAngle : skirt ? skirtMaxAngle : bone.name.StartsWith("Earring", StringComparison.Ordinal) ? 10 : hairMaxAngle });
            }
            joints.Sort((a, b) => Depth(a.bone).CompareTo(Depth(b.bone)));
            previousRoot = transform.position;
            guardReady=false;guardAnimator=animator;
        }
        static int Depth(Transform t) { int n = 0; while (t.parent) { n++; t = t.parent; } return n; }
        void Update()
        {
            // Animator writes its pose after Update. This also prevents accumulating
            // spring rotation on clips that have no curves for a particular chain.
            foreach (var j in joints) j.bone.localRotation = j.animatedLocal;
        }
        void LateUpdate()
        {
            long began=System.Diagnostics.Stopwatch.GetTimestamp();
            float dt = Mathf.Min(Time.deltaTime, 1f / 15f);
            if(dt<=0)return;
            bool teleport = (transform.position - previousRoot).sqrMagnitude > 1;
            previousRoot = transform.position; LastBend = 0;
            foreach (var j in joints) j.animatedLocal = j.bone.localRotation;
            if(layerGuard)
            {
                if(!guardReady){SetupShirtGuard();SetupSkirtContacts();SetupContactReferences(guardAnimator);guardReady=true;}
                CacheHandContacts();CacheLegContacts();if(HasAuthoredPose){UpdateSkirtSurface();CaptureShirtReferences(true);CaptureSkirtContactBaselines();}
            }
            foreach (var j in joints)
            {
                if(j.cloth!=0) continue;
                float scale = Mathf.Abs(j.bone.lossyScale.y), length = j.length * scale;
                Vector3 head = j.bone.position;
                Vector3 animated = j.bone.TransformDirection(j.axis).normalized;
                Vector3 rest = head + animated * length;
                if (!j.ready || teleport) { j.tip = j.previous = rest; j.velocity=Vector3.zero; j.ready = true; continue; }
                if (dt <= 0) continue;
                Vector3 next = Predict(j,rest,Vector3.down*(j.skirt?.25f:.03f)*scale,dt);
                Vector3 direction = (next - head).normalized;
                if (direction.sqrMagnitude < .1f) direction = animated;
                next = head + direction * length;
                if (j.skirt)
                    for (int k = 0; k < 3; k++)
                    {
                        next = OutsideCapsule(next, leftHip, leftKnee, thighRadius * scale);
                        next = OutsideCapsule(next, rightHip, rightKnee, thighRadius * scale);
                        next = head + (next - head).normalized * length;
                    }
                direction = (next - head).normalized;
                float angle = Vector3.Angle(animated, direction);
                if (angle > j.limit) direction = Vector3.Slerp(animated, direction, j.limit / angle).normalized;
                j.previous = j.tip; j.tip = head + direction * length;
                j.velocity=Vector3.ClampMagnitude((j.tip-j.previous)/dt,.8f);
                j.bone.rotation = Quaternion.FromToRotation(animated, direction) * j.bone.rotation;
                LastBend = Mathf.Max(LastBend, Vector3.Angle(animated, direction));
            }
            if(!layerGuard)
            {
                // springs only: no contact repair or layer checks
                UpdateShirt(dt,teleport);
                LastSimulationMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-began)*1000.0/System.Diagnostics.Stopwatch.Frequency;
                return;
            }
            RepairSkirtContacts();UpdateSkirtSurface(); RepairAuthoredHem(); CaptureShirtReferences();
            UpdateShirt(dt,teleport);
            if(!LayersSafe())
            {
                // The blouse is the outer layer. At contact, restrict only the
                // additional skirt spring; authored skirt animation is retained.
                LayerContactFrames++;
                foreach(var j in joints) if(j.skirt) {j.bone.localRotation=j.animatedLocal;j.ready=false;}
                foreach(var j in joints) if(j.cloth!=0) {j.bone.localRotation=j.animatedLocal;j.ready=false;}
                RepairSkirtContacts();UpdateSkirtSurface(); RepairAuthoredHem(); CaptureShirtReferences(); UpdateShirt(dt,true);
                if(!LayersSafe())
                {
                    foreach(var j in joints) if(j.cloth!=0){j.bone.localRotation=j.animatedLocal;j.ready=false;}
                    RepairAuthoredHem();if(!LayersSafe())UnresolvedLayerContacts++;
                }
            }
            LastSimulationMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-began)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        static Vector3 OutsideCapsule(Vector3 p, Transform a, Transform b, float radius)
        {
            if (!a || !b) return p;
            Vector3 ab = b.position - a.position;
            float t = Mathf.Clamp01(Vector3.Dot(p - a.position, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
            Vector3 center = a.position + t * ab, d = p - center;
            if (d.sqrMagnitude >= radius * radius) return p;
            return center + (d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.forward) * radius;
        }

        void SetupShirtGuard()
        {
            foreach(var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {if(renderer.name=="Shirt")shirtRenderer=renderer;else if(renderer.name=="Skirt")skirtRenderer=renderer;}
            if(!shirtRenderer||!shirtRenderer.sharedMesh||!shirtRenderer.sharedMesh.isReadable)return;
            var mesh=shirtRenderer.sharedMesh;shirtVertices=mesh.vertices;shirtWeights=mesh.boneWeights;shirtBinds=mesh.bindposes;shirtBones=shirtRenderer.bones;
            if(skirtRenderer){skirtTriangles=skirtRenderer.sharedMesh.triangles;skirtBake=new Mesh{name="V5 shirt/skirt guard"};}
            foreach(var j in joints)
            {
                if(j.cloth==0)continue;
                var indices=new HashSet<int>();
                for(int i=0;i<shirtBones.Length;i++)if(shirtBones[i]==j.bone||shirtBones[i].IsChildOf(j.bone))indices.Add(i);
                var outer=shirtOuterVertices==null?null:new HashSet<int>(shirtOuterVertices);
                var rim=shirtHemRimVertices==null?new HashSet<int>():new HashSet<int>(shirtHemRimVertices);
                j.affectedShirtMatrices=new int[indices.Count];indices.CopyTo(j.affectedShirtMatrices);
                var candidates=new List<int>();
                for(int i=0;i<shirtWeights.Length;i++)
                {
                    var w=shirtWeights[i];float influence=(indices.Contains(w.boneIndex0)?w.weight0:0)+(indices.Contains(w.boneIndex1)?w.weight1:0)+(indices.Contains(w.boneIndex2)?w.weight2:0)+(indices.Contains(w.boneIndex3)?w.weight3:0);
                    if(influence>(j.cloth==1?.01f:.08f)&&(outer==null||outer.Contains(i)))candidates.Add(i);
                }
                // Farthest-point samples cover the rim and both sides of the cloth,
                // rather than taking a contiguous run of mesh vertex indices.
                var selected=new List<int>();
                int count=Mathf.Min(j.cloth==1?24:8,candidates.Count);
                for(int k=0;k<count;k++)
                {
                    int best=-1;float far=-1;
                    foreach(int index in candidates)
                    {
                        if(selected.Contains(index))continue;float nearest=float.MaxValue;
                        foreach(int other in selected)nearest=Mathf.Min(nearest,(shirtVertices[index]-shirtVertices[other]).sqrMagnitude);
                        if(nearest>far){far=nearest;best=index;}
                    }
                    if(best<0)break;selected.Add(best);j.probes.Add(new Probe{vertex=best});
                }
            }
        }
        void RefreshSkinMatrices(Joint changed=null)
        {
            if(shirtBones==null)return;
            if(skinMatrices==null||skinMatrices.Length!=shirtBones.Length)skinMatrices=new Matrix4x4[shirtBones.Length];
            if(changed!=null&&changed.affectedShirtMatrices!=null)
            {foreach(int i in changed.affectedShirtMatrices)skinMatrices[i]=shirtBones[i].localToWorldMatrix*shirtBinds[i];}
            else for(int i=0;i<shirtBones.Length;i++)skinMatrices[i]=shirtBones[i].localToWorldMatrix*shirtBinds[i];
        }
        Vector3 SkinPoint(int i)
        {
            var w=shirtWeights[i];var p=shirtVertices[i];Vector3 q=Vector3.zero;
            if(w.weight0>0)q+=skinMatrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0;
            if(w.weight1>0)q+=skinMatrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1;
            if(w.weight2>0)q+=skinMatrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2;
            if(w.weight3>0)q+=skinMatrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
            return q;
        }
        static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b)
        {var ab=b-a;return Vector3.Distance(p,a+ab*Mathf.Clamp01(Vector3.Dot(p-a,ab)/Mathf.Max(1e-9f,ab.sqrMagnitude)));}
        float CapsuleDistance(Joint j,Vector3 p)
        {
            if(j.cloth==2)
            {bool left=j.bone.name.StartsWith("SleeveLeft",StringComparison.Ordinal);var a=left?leftUpperArm:rightUpperArm;var b=left?leftLowerArm:rightLowerArm;return a&&b?SegmentDistance(p,left?contactLeftShoulder:contactRightShoulder,left?contactLeftElbow:contactRightElbow):float.MaxValue;}
            return hips&&chest?SegmentDistance(p,contactTorsoStart,contactTorsoEnd):float.MaxValue;
        }
        void UpdateSkirtSurface(){UpdateSkirtProxySurface();}
        int HeightBin(float h)=>Mathf.Clamp((int)((h-minHeight)/Mathf.Max(.0001f,maxHeight-minHeight)*heightBins.Length),0,heightBins.Length-1);
        float ExactSkirtClearance(Vector3 p)
        {if(!layerGuard)return float.PositiveInfinity;
            if(skirtWorld==null||!hips)return float.PositiveInfinity;
            var up=layerUp;Vector3 axis=layerAxis;Vector3 center=axis+up*Vector3.Dot(p-axis,up),ray=p-center;float radius=ray.magnitude;
            if(radius<.02f)return float.PositiveInfinity;Vector3 dir=ray/radius;float h=Vector3.Dot(p,up),hit=float.PositiveInfinity;
            foreach(int i in polarBins[HeightBin(h)*AngularBins+AngularBin(LayerAngle(p))])
            {
                if(h<triangleMin[i]-.0001f||h>triangleMax[i]+.0001f)continue;
                var a=skirtWorld[skirtTriangles[i*3]];var e1=skirtWorld[skirtTriangles[i*3+1]]-a;var e2=skirtWorld[skirtTriangles[i*3+2]]-a;
                var cross=Vector3.Cross(dir,e2);float determinant=Vector3.Dot(e1,cross);if(Mathf.Abs(determinant)<1e-9f)continue;
                float inverse=1/determinant;var t=center-a;float u=Vector3.Dot(t,cross)*inverse;if(u<0||u>1)continue;
                var q=Vector3.Cross(t,e1);float v=Vector3.Dot(dir,q)*inverse;if(v<0||u+v>1)continue;
                float distance=Vector3.Dot(e2,q)*inverse;if(distance>0)hit=Mathf.Min(hit,distance);
            }
            return float.IsPositiveInfinity(hit)?float.PositiveInfinity:radius-hit;
        }
        void CaptureShirtReferences(bool baseline=false)
        {RefreshSkinMatrices();CaptureHemRim(baseline);
            foreach(var j in joints)if(j.cloth!=0)foreach(var p in j.probes)
            {p.reference=SkinPoint(p.vertex);p.capsule=CapsuleDistance(j,p.reference);p.skirt=j.cloth==1?SkirtClearance(p.reference):float.PositiveInfinity;if(baseline||!HasAuthoredPose)p.authoredSkirt=p.skirt;p.handGap=HandGap(p.reference);p.legGap=LegDistance(p.reference);}
        }
        bool SafeShirt(Joint j)
        {RefreshSkinMatrices(j);
            float scale=Mathf.Abs(transform.lossyScale.y),limit=(j.cloth==1?.006f:j.cloth==2?.0012f:.0003f)*scale;
            foreach(var p in j.probes)
            {
                Vector3 position=SkinPoint(p.vertex);if((position-p.reference).sqrMagnitude>limit*limit)return false;
                float radius=(j.cloth==2?upperArmRadius:torsoRadius)*scale;
                if(CapsuleDistance(j,position)<Mathf.Min(radius+.0003f*scale,p.capsule-.00015f*scale))return false;
                if(j.cloth==1&&LegDistance(position)<Mathf.Min(.084f*scale,p.legGap-.0001f*scale))return false;
                if(j.cloth==1&&HandGap(position)<Mathf.Min(.0015f*scale,p.handGap-.0001f*scale))return false;
                if(j.cloth==1&&SkirtClearance(position)<Mathf.Min(.0015f*scale,p.skirt-.00015f*scale))return false;
            }
            return true;
        }
        void UpdateShirt(float dt,bool reset)
        {
            LastShirtBend=0;MaxShirtProbeDisplacement=0;
            foreach(var j in joints)
            {
                if(j.cloth==0)continue;
                float length=j.length*Mathf.Abs(j.bone.lossyScale.y);Vector3 head=j.bone.position,animated=j.bone.TransformDirection(j.axis).normalized;
                Quaternion rotation=j.bone.rotation;Vector3 rest=head+animated*length;
                if(!j.ready||reset){j.tip=j.previous=rest;j.velocity=Vector3.zero;j.ready=true;}
                Vector3 next=Predict(j,rest,Vector3.down*.02f,dt);
                Vector3 direction=(next-head).normalized;if(direction.sqrMagnitude<.1f)direction=animated;
                float angle=Vector3.Angle(animated,direction),limit=j.cloth==1?shirtMaxAngle:j.cloth==2?sleeveMaxAngle:collarMaxAngle;
                if(angle>limit)direction=Vector3.Slerp(animated,direction,limit/angle).normalized;
                Quaternion proposed=Quaternion.FromToRotation(animated,direction)*rotation;
                bool accepted=false;
                if(!layerGuard){j.bone.rotation=proposed;accepted=true;}
                else foreach(float fraction in GuardFractions)
                {j.bone.rotation=Quaternion.Slerp(rotation,proposed,fraction);if(SafeShirt(j)){accepted=true;break;}}
                if(!accepted)j.bone.rotation=rotation;
                j.previous=j.tip;j.tip=head+j.bone.TransformDirection(j.axis).normalized*length;
                j.velocity=Vector3.ClampMagnitude((j.tip-j.previous)/dt,.5f);
                LastShirtBend=Mathf.Max(LastShirtBend,Quaternion.Angle(rotation,j.bone.rotation));
                if(layerGuard&&j.probes!=null)foreach(var p in j.probes)MaxShirtProbeDisplacement=Mathf.Max(MaxShirtProbeDisplacement,Vector3.Distance(SkinPoint(p.vertex),p.reference));
            }
        }
        // Repair hem overlap introduced by Humanoid retargeting or clip blending.
        void RepairAuthoredHem()
        {
            if(HasAuthoredPose||!hips||shirtBones==null)return;
            float margin=.002f*Mathf.Abs(transform.lossyScale.y);
            foreach(var j in joints)
            {
                if(j.cloth!=1||!j.bone.name.EndsWith("_01"))continue;
                Vector3 outward=Vector3.ProjectOnPlane(j.bone.position-hips.position,Up).normalized;
                Vector3 axis=Vector3.Cross(-Up,outward).normalized;
                if(axis.sqrMagnitude<.1f)continue;
                for(int step=0;step<32;step++)
                {
                    RefreshSkinMatrices(j);bool safe=true;
                    foreach(var p in j.probes)if(SkirtClearance(SkinPoint(p.vertex))<margin){safe=false;break;}
                    if(safe)break;
                    j.bone.rotation=Quaternion.AngleAxis(1.5f,axis)*j.bone.rotation;
                }
            }
        }
        bool LayersSafe()
        {RefreshSkinMatrices();if(!HemRimSafe())return false;float tolerance=.00025f*Mathf.Abs(transform.lossyScale.y);foreach(var j in joints)if(j.cloth==1)foreach(var p in j.probes)if(SkirtClearance(SkinPoint(p.vertex))<Mathf.Min(-tolerance,p.authoredSkirt-tolerance))return false;return true;}

        void OnDisable()
        {
            foreach (var j in joints) if (j.bone) j.bone.localRotation = j.animatedLocal;
            joints.Clear();
            if(skirtBake){Destroy(skirtBake);skirtBake=null;}
        }
    }
}
