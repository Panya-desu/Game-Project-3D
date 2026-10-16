using UnityEngine;
namespace EchoCharacters
{
    public sealed partial class EchoSecondaryMotion
    {
        [HideInInspector] public Vector3[] skirtLayerVertices;
        [HideInInspector] public BoneWeight[] skirtLayerWeights;
        [HideInInspector] public int[] skirtLayerTriangles;
        [HideInInspector] public float skirtLayerMargin=.002f;
        const int AngularBins=64;
        readonly System.Collections.Generic.List<int>[] polarBins=new System.Collections.Generic.List<int>[48*AngularBins];
        Vector3 layerUp,layerRight,layerForward,layerAxis;
        int AngularBin(float angle)=>((Mathf.FloorToInt((angle+Mathf.PI)/(2*Mathf.PI)*AngularBins)%AngularBins)+AngularBins)%AngularBins;
        float LayerAngle(Vector3 p)=>Mathf.Atan2(Vector3.Dot(p-layerAxis,layerForward),Vector3.Dot(p-layerAxis,layerRight));
        void UpdateSkirtProxySurface()
        {
            if(!layerGuard||contactBones==null||skirtLayerVertices==null||skirtLayerWeights==null||skirtLayerVertices.Length==0)return;
            for(int i=0;i<contactBones.Length;i++)contactMatrices[i]=contactBones[i].localToWorldMatrix*contactBinds[i];
            if(skirtWorld==null||skirtWorld.Length!=skirtLayerVertices.Length)skirtWorld=new Vector3[skirtLayerVertices.Length];
            for(int i=0;i<skirtWorld.Length;i++)
            {
                Vector3 p=skirtLayerVertices[i],q=Vector3.zero;var w=skirtLayerWeights[i];
                if(w.weight0>0)q+=contactMatrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0;
                if(w.weight1>0)q+=contactMatrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1;
                if(w.weight2>0)q+=contactMatrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2;
                if(w.weight3>0)q+=contactMatrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
                skirtWorld[i]=q;
            }
            skirtTriangles=skirtLayerTriangles;int count=skirtTriangles.Length/3;
            if(triangleMin==null||triangleMin.Length!=count){triangleMin=new float[count];triangleMax=new float[count];}
            var up=Up;layerUp=up;layerRight=transform.right;layerForward=transform.forward;layerAxis=hips.position-layerForward*.005f;
            minHeight=float.PositiveInfinity;maxHeight=float.NegativeInfinity;
            for(int i=0;i<count;i++)
            {
                float a=Vector3.Dot(skirtWorld[skirtTriangles[i*3]],up),b=Vector3.Dot(skirtWorld[skirtTriangles[i*3+1]],up),c=Vector3.Dot(skirtWorld[skirtTriangles[i*3+2]],up);
                triangleMin[i]=Mathf.Min(a,Mathf.Min(b,c));triangleMax[i]=Mathf.Max(a,Mathf.Max(b,c));minHeight=Mathf.Min(minHeight,triangleMin[i]);maxHeight=Mathf.Max(maxHeight,triangleMax[i]);
            }
            for(int i=0;i<heightBins.Length;i++){if(heightBins[i]==null)heightBins[i]=new System.Collections.Generic.List<int>();else heightBins[i].Clear();}
            for(int i=0;i<count;i++)for(int bin=HeightBin(triangleMin[i]-.0002f);bin<=HeightBin(triangleMax[i]+.0002f);bin++)heightBins[bin].Add(i);
            for(int i=0;i<polarBins.Length;i++){if(polarBins[i]==null)polarBins[i]=new System.Collections.Generic.List<int>();else polarBins[i].Clear();}
            for(int i=0;i<count;i++)
            {
                float a=LayerAngle(skirtWorld[skirtTriangles[i*3]]),b=LayerAngle(skirtWorld[skirtTriangles[i*3+1]]),c=LayerAngle(skirtWorld[skirtTriangles[i*3+2]]);
                b=a+Mathf.DeltaAngle(a*Mathf.Rad2Deg,b*Mathf.Rad2Deg)*Mathf.Deg2Rad;c=a+Mathf.DeltaAngle(a*Mathf.Rad2Deg,c*Mathf.Rad2Deg)*Mathf.Deg2Rad;
                float lo=Mathf.Min(a,Mathf.Min(b,c))-.0001f,hi=Mathf.Max(a,Mathf.Max(b,c))+.0001f;
                int first=Mathf.FloorToInt((lo+Mathf.PI)/(2*Mathf.PI)*AngularBins),last=Mathf.FloorToInt((hi+Mathf.PI)/(2*Mathf.PI)*AngularBins);
                for(int h=HeightBin(triangleMin[i]-.0002f);h<=HeightBin(triangleMax[i]+.0002f);h++)
                    for(int k=first;k<=last;k++)polarBins[h*AngularBins+((k%AngularBins+AngularBins)%AngularBins)].Add(i);
            }
        }
        float SkirtClearance(Vector3 point)
        {return ExactSkirtClearance(point)-skirtLayerMargin*Mathf.Abs(transform.lossyScale.y);}
    }
}
