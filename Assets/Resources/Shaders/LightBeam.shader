// A soft shaft of light for the White Space (LightBeams): additive, no texture. Across the beam the light
// falls off softly to both edges, the beam widens towards its foot like a cone, both ends fade out, faint
// streaks of dust drift down through it and the whole beam slowly breathes. It fades out near the camera.
// uv.x runs across the beam, uv.y along it (1 = top, where the light comes from).
Shader "Echo/LightBeam"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.97, 0.9, 1)
        _Intensity ("Intensity", Float) = 0.22
        _Phase ("Phase", Float) = 0
        _NearFade ("Fade starts (m)", Float) = 2
        _FullAt ("Full at (m)", Float) = 7
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Beam"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity, _Phase, _NearFade, _FullAt;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float depth : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(w);
                o.depth = -TransformWorldToView(w).z;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float along = i.uv.y;
                // narrow at the top, wide at the foot
                float hw = lerp(0.5, 0.18, along);
                float x = abs(i.uv.x - 0.5) / hw;
                float across = exp(-x * x * 3.0) * step(x, 1.6);
                // both ends fade, brightest a third of the way down
                float ends = smoothstep(0.0, 0.35, along) * smoothstep(1.0, 0.8, along);
                // faint dust streaks drifting down, and a slow breath
                float streaks = 0.82 + 0.18 * sin(i.uv.x * 23.0 + _Phase * 3.1) * sin(along * 9.0 + t * 0.7 + _Phase);
                float breath = 0.78 + 0.22 * sin(t * 0.45 + _Phase);
                float near = saturate((i.depth - _NearFade) / max(_FullAt - _NearFade, 0.01));
                float a = across * ends * streaks * breath * near * _Intensity;
                return half4(_Color.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}
