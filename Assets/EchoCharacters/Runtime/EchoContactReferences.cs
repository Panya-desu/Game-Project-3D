using UnityEngine;
namespace EchoCharacters
{
    public sealed partial class EchoSecondaryMotion
    {
        EchoAuthoredPose authoredPose;Transform contactLeftHand,contactRightHand;
        Vector3 contactLeftElbow,contactRightElbow,contactLeftPalm,contactRightPalm,contactLeftTip,contactRightTip;float contactScale=1;
        Vector3 contactTorsoStart,contactTorsoEnd,contactLeftShoulder,contactRightShoulder;
        readonly System.Collections.Generic.List<Probe> hemRimProbes=new System.Collections.Generic.List<Probe>();
        bool HasAuthoredPose=>authoredPose&&authoredPose.AppliedFrame==Time.frameCount;
        void SetupContactReferences(Animator animator)
        {
            if(!animator||!animator.isHuman)return;authoredPose=animator.GetComponent<EchoAuthoredPose>();
            contactLeftHand=animator.GetBoneTransform(HumanBodyBones.LeftHand);contactRightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            hemRimProbes.Clear();if(shirtHemRimVertices!=null)foreach(int index in shirtHemRimVertices)hemRimProbes.Add(new Probe{vertex=index});
        }
        void CacheHandContacts()
        {
            contactScale=Mathf.Abs(transform.lossyScale.y);if(!contactLeftHand||!contactRightHand)return;
            contactLeftElbow=leftLowerArm.position;contactRightElbow=rightLowerArm.position;
            contactLeftShoulder=leftUpperArm.position;contactRightShoulder=rightUpperArm.position;
            contactTorsoStart=hips.position-transform.up*.025f;contactTorsoEnd=chest.position;
            contactLeftPalm=contactLeftHand.position;contactRightPalm=contactRightHand.position;
            contactLeftTip=contactLeftPalm+contactLeftHand.up*(.16f*contactScale);contactRightTip=contactRightPalm+contactRightHand.up*(.16f*contactScale);
        }
        float HandGap(Vector3 point)
        {
            if(!contactLeftHand||!contactRightHand)return float.PositiveInfinity;
            float forearm=Mathf.Min(SegmentDistance(point,contactLeftElbow,contactLeftPalm),SegmentDistance(point,contactRightElbow,contactRightPalm))-.035f*contactScale;
            float hand=Mathf.Min(SegmentDistance(point,contactLeftPalm,contactLeftTip),SegmentDistance(point,contactRightPalm,contactRightTip))-.05f*contactScale;
            return Mathf.Min(forearm,hand);
        }
        void CaptureHemRim(bool baseline)
        {
            foreach(var p in hemRimProbes)
            {p.reference=SkinPoint(p.vertex);p.skirt=SkirtClearance(p.reference);if(baseline||!HasAuthoredPose)p.authoredSkirt=p.skirt;
             p.capsule=SegmentDistance(p.reference,contactTorsoStart,contactTorsoEnd);p.handGap=HandGap(p.reference);p.legGap=LegDistance(p.reference);}
        }
        bool HemRimSafe()
        {
            float tolerance=.00025f*contactScale;
            foreach(var p in hemRimProbes)
            {
                var position=SkinPoint(p.vertex);
                if(SkirtClearance(position)<Mathf.Min(-tolerance,p.authoredSkirt-tolerance))return false;
                if(HandGap(position)<Mathf.Min(.0015f*contactScale,p.handGap-.0001f*contactScale))return false;
                if(LegDistance(position)<Mathf.Min(.084f*contactScale,p.legGap-.0001f*contactScale))return false;
                if(SegmentDistance(position,contactTorsoStart,contactTorsoEnd)<Mathf.Min(torsoRadius*contactScale,p.capsule-.00015f*contactScale))return false;
            }
            return true;
        }
    }
}
