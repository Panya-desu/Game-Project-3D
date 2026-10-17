using System;
using UnityEngine;
namespace EchoCharacters
{
    /// <summary>Preserve elbow bend after Humanoid retargeting. Source weights distribute wrist roll.</summary>
    [DefaultExecutionOrder(10000),DisallowMultipleComponent]
    public sealed class EchoArmPose : MonoBehaviour
    {
        Animator animator;EchoAuthoredPose authored;readonly Transform[] lower=new Transform[2],hand=new Transform[2];
        readonly Quaternion[] restRotation=new Quaternion[2];readonly Vector3[] restAxis=new Vector3[2];float blend;
        void Awake()
        {
            animator=GetComponentInChildren<Animator>();if(!animator||!animator.isHuman)return;authored=animator.GetComponent<EchoAuthoredPose>();
            SkinnedMeshRenderer body=null;foreach(var r in GetComponentsInChildren<SkinnedMeshRenderer>())if(r.name=="Body")body=r;
            if(!body||!body.sharedMesh)return;
            for(int side=0;side<2;side++)
            {
                lower[side]=animator.GetBoneTransform(side==0?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
                hand[side]=animator.GetBoneTransform(side==0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                int index=Array.IndexOf(body.bones,lower[side]);if(index<0){lower[side]=null;continue;}
                var rest=body.transform.localToWorldMatrix*body.sharedMesh.bindposes[index].inverse;
                restRotation[side]=Quaternion.Inverse(transform.rotation)*rest.rotation;
                restAxis[side]=transform.InverseTransformDirection(rest.MultiplyVector(Vector3.up).normalized);
            }
        }
        void LateUpdate()
        {
            if(!animator||!animator.isActiveAndEnabled)return;
            if(authored&&authored.AppliedFrame==Time.frameCount)return;
            blend=Mathf.MoveTowards(blend,1,Time.deltaTime/.12f);if(blend<=0)return;
            for(int side=0;side<2;side++)
            {
                if(!lower[side]||!hand[side])continue;
                Vector3 position=hand[side].position,direction=(position-lower[side].position).normalized;
                Quaternion handRotation=hand[side].rotation;
                Quaternion minimal=Quaternion.FromToRotation(transform.TransformDirection(restAxis[side]),direction)*transform.rotation*restRotation[side];
                lower[side].rotation=Quaternion.Slerp(lower[side].rotation,minimal,blend);
                hand[side].SetPositionAndRotation(position,handRotation);
            }
        }
    }
}
