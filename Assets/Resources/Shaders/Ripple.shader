// A ring on the water for WhiteSpaceFlair: each particle is a thin soft circle (additive, no texture) that the
// particle system grows and fades; a second faint inner ring trails it like a real ripple.
Shader "Echo/Ripple"
{
    Properties { }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Ripple"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv * 2.0 - 1.0);
                float outer = saturate(1.0 - abs(r - 0.86) / 0.07);
                float inner = saturate(1.0 - abs(r - 0.62) / 0.06) * 0.45;
                float ring = outer * outer + inner * inner;
                half a = ring * i.color.a;
                return half4(i.color.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}
