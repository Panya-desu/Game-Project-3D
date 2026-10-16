// A tiny flake of a memory shard for WindDrift (the White Space has no trees, so no petals): a thin
// diamond of pale, faintly iridescent crystal (alpha blended, slightly cool so it shows against the white
// world) that now and then catches the sun in a small gold glint as it turns in the wind (additive).
// Premultiplied blending carries both. Fades out near the camera.
Shader "Echo/Flake"
{
    Properties { }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Flake"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float depth : TEXCOORD1; float phase : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(w);
                o.depth = -TransformWorldToView(w).z;
                o.color = v.color;
                o.uv = v.uv;
                o.phase = frac(sin(dot(floor(w * 3.0), float3(12.9898, 78.233, 37.719))) * 43758.5453) * 6.2831;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float d = abs(p.x) * 1.6 + abs(p.y);              // a slim diamond
                float shape = saturate((1.0 - d) * 4.0);
                float edge = saturate(1.0 - abs(d - 0.75) * 6.0);  // brighter facet edge
                // faint rainbow sheen across the facet
                half3 sheen = 0.5 + 0.5 * cos(6.2831 * (p.x * 0.6 + p.y * 0.3 + float3(0.0, 0.33, 0.67)));
                half3 body = lerp(i.color.rgb, sheen, 0.18) * (0.85 + 0.25 * edge);
                // a gold glint now and then as it turns towards the sun
                float glint = pow(saturate(sin(_Time.y * 1.7 + i.phase)), 24.0) * shape;
                half near = saturate((i.depth - 0.4) / 0.8);
                half a = shape * i.color.a * near;
                half3 col = body * a + half3(1.0, 0.82, 0.5) * glint * 1.6 * near;
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
