using System;
using System.Collections.Generic;
using UnityEngine;
namespace EchoCharacters
{
    public sealed partial class EchoSecondaryMotion
    {
        Vector3[] contactVertices;BoneWeight[] contactWeights;Matrix4x4[] contactBinds,contactMatrices;Transform[] contactBones;
        readonly Dictionary<Joint,int[]> skirtContactProbes=new Dictionary<Joint,int[]>();
        readonly Dictionary<Joint,int[]> skirtContactMatrixIndices=new Dictionary<Joint,int[]>();
        readonly Dictionary<Joint,float[]> skirtContactBaselines=new Dictionary<Joint,float[]>();
        Vector3 legLeftHip,legLeftKnee,legRightHip,legRightKnee;int skirtBaselineFrame=-1;
        public float LastSkirtContactCorrection {get;private set;}
        void SetupSkirtContacts()
        {
            skirtContactProbes.Clear();skirtContactMatrixIndices.Clear();skirtContactBaselines.Clear();if(!skirtRenderer||!skirtRenderer.sharedMesh.isReadable)return;
            var mesh=skirtRenderer.sharedMesh;contactVertices=mesh.vertices;contactWeights=mesh.boneWeights;contactBinds=mesh.bindposes;contactBones=skirtRenderer.bones;contactMatrices=new Matrix4x4[contactBones.Length];
            foreach(var joint in joints)
            {
                if(!joint.skirt||!joint.bone.name.EndsWith("_01",StringComparison.Ordinal))continue;
                var indices=new HashSet<int>();for(int k=0;k<contactBones.Length;k++)if(contactBones[k]==joint.bone||contactBones[k].IsChildOf(joint.bone))indices.Add(k);
                var affected=new int[indices.Count];indices.CopyTo(affected);skirtContactMatrixIndices[joint]=affected;
                var candidates=new List<int>();
                for(int i=0;i<contactWeights.Length;i++)
                {var b=contactWeights[i];float weight=(indices.Contains(b.boneIndex0)?b.weight0:0)+(indices.Contains(b.boneIndex1)?b.weight1:0)+(indices.Contains(b.boneIndex2)?b.weight2:0)+(indices.Contains(b.boneIndex3)?b.weight3:0);
                 if(weight>.10f&&contactVertices[i].z<.93f)candidates.Add(i);}
                var selected=new List<int>();
                for(int count=0;count<Mathf.Min(32,candidates.Count);count++)
                {int best=-1;float distance=-1;foreach(int i in candidates){float nearest=float.PositiveInfinity;foreach(int j in selected)nearest=Mathf.Min(nearest,(contactVertices[i]-contactVertices[j]).sqrMagnitude);if(nearest>distance){distance=nearest;best=i;}}if(best>=0)selected.Add(best);}
                skirtContactProbes[joint]=selected.ToArray();
                skirtContactBaselines[joint]=new float[selected.Count];
            }
        }
        Vector3 SkirtContactPoint(int index)
        {
            var w=contactWeights[index];var p=contactVertices[index];
            Vector3 result=Vector3.zero;
            if(w.weight0>0)result+=contactMatrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0;
            if(w.weight1>0)result+=contactMatrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1;
            if(w.weight2>0)result+=contactMatrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2;
            if(w.weight3>0)result+=contactMatrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
            return result;
        }
        void RepairSkirtContacts()
        {
            LastSkirtContactCorrection=0;if(!hips||!leftHip||!rightHip||contactBones==null)return;
            CacheLegContacts();
            float radius=.095f*Mathf.Abs(transform.lossyScale.y);
            for(int k=0;k<contactBones.Length;k++)contactMatrices[k]=contactBones[k].localToWorldMatrix*contactBinds[k];
            if(HasAuthoredPose)
            {
                if(skirtBaselineFrame!=Time.frameCount)return;
                float tolerance=.00015f*Mathf.Abs(transform.lossyScale.y);
                foreach(var pair in skirtContactProbes)
                {
                    var j=pair.Key;var baseline=skirtContactBaselines[j];bool safe=true;
                    for(int i=0;i<pair.Value.Length;i++)if(LegDistance(SkirtContactPoint(pair.Value[i]))<Mathf.Min(radius,baseline[i]-tolerance)){safe=false;break;}
                    if(!safe)
                    {foreach(var other in joints)if(other.skirt&&(other.bone==j.bone||other.bone.IsChildOf(j.bone))){other.bone.localRotation=other.animatedLocal;other.ready=false;}
                     foreach(int k in skirtContactMatrixIndices[j])contactMatrices[k]=contactBones[k].localToWorldMatrix*contactBinds[k];}
                }
                return;
            }
            foreach(var pair in skirtContactProbes)
            {
                var j=pair.Key;Vector3 outward=Vector3.ProjectOnPlane(j.bone.position-hips.position,Up).normalized;
                Vector3 axis=Vector3.Cross(-Up,outward).normalized;if(axis.sqrMagnitude<.1f)continue;
                float correction=0;
                for(int step=0;step<14;step++)
                {
                    foreach(int k in skirtContactMatrixIndices[j])contactMatrices[k]=contactBones[k].localToWorldMatrix*contactBinds[k];
                    float distance=float.PositiveInfinity;
                    foreach(int i in pair.Value)distance=Mathf.Min(distance,LegDistance(SkirtContactPoint(i)));
                    if(distance>=radius)break;
                    j.bone.rotation=Quaternion.AngleAxis(1.5f,axis)*j.bone.rotation;correction+=1.5f;
                }
                LastSkirtContactCorrection=Mathf.Max(LastSkirtContactCorrection,correction);
                foreach(int k in skirtContactMatrixIndices[j])contactMatrices[k]=contactBones[k].localToWorldMatrix*contactBinds[k];
            }
        }
        void CacheLegContacts(){legLeftHip=leftHip.position;legLeftKnee=leftKnee.position;legRightHip=rightHip.position;legRightKnee=rightKnee.position;}
        float LegDistance(Vector3 point)=>Mathf.Min(SegmentDistance(point,legLeftHip,legLeftKnee),SegmentDistance(point,legRightHip,legRightKnee));
        void CaptureSkirtContactBaselines()
        {
            if(contactBones==null||!leftHip||!rightHip)return;CacheLegContacts();
            for(int k=0;k<contactBones.Length;k++)contactMatrices[k]=contactBones[k].localToWorldMatrix*contactBinds[k];
            foreach(var pair in skirtContactProbes){var baseline=skirtContactBaselines[pair.Key];for(int i=0;i<pair.Value.Length;i++)baseline[i]=LegDistance(SkirtContactPoint(pair.Value[i]));}
            skirtBaselineFrame=Time.frameCount;
        }
    }
}
