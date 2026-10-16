// Soft drifting mist for AtmosphereLook: each particle is a round, very soft additive glow (no texture),
// coloured by the particle colour, that fades out as it comes near the camera so the player never walks
// through a visible card.
Shader "Echo/Mist"
{
    Properties
    {
        _NearFade ("Fade starts (m)", Float) = 1.5
        _FullAt ("Full at (m)", Float) = 5
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Mist"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _NearFade, _FullAt;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float depth : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(w);
                o.depth = -TransformWorldToView(w).z;
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv * 2.0 - 1.0);
                half soft = saturate(1.0 - r);
                soft = soft * soft * (3.0 - 2.0 * soft); // smooth falloff to the edge
                half near = saturate((i.depth - _NearFade) / max(_FullAt - _NearFade, 0.01));
                half a = soft * near * i.color.a;
                return half4(i.color.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}
