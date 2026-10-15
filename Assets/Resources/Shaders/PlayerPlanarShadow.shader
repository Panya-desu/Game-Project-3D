// The player's shadow indoors: her body flattened onto the floor along one smoothed light direction
// (a planar projected shadow). Set by ShadowFocus: _PlaneY (floor height), _LightDir (direction the
// light travels, pointing down), _Origin (her feet), _MaxLength, _ShadowColor.
// The stencil makes overlapping parts (arm over body) darken the floor only once.
Shader "Echo/PlayerPlanarShadow"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.02, 0.02, 0.05, 0.4)
        _PlaneY ("Plane Y", Float) = 0
        _LightDir ("Light Direction", Vector) = (0.3, -1, 0.2, 0)
        _Origin ("Feet", Vector) = (0, 0, 0, 0)
        _MaxLength ("Max Length", Float) = 3
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-100" "IgnoreProjector" = "True" }
        Pass
        {
            Name "PlanarShadow"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1
            Stencil { Ref 64 ReadMask 64 WriteMask 64 Comp NotEqual Pass Replace }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                float _PlaneY;
                float4 _LightDir;
                float4 _Origin;
                float _MaxLength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float fade : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(v.positionOS.xyz);
                float3 dir = normalize(_LightDir.xyz);
                float t = (w.y - _PlaneY) / max(-dir.y, 0.2);
                float3 p = w + dir * max(t, 0.0);
                p.y = _PlaneY + 0.003;
                o.positionCS = TransformWorldToHClip(p);
                // softly fades towards the far end, like a real shadow losing contact with her
                float along = length(p.xz - _Origin.xz);
                o.fade = saturate(1.0 - along / max(_MaxLength, 0.01)) * 0.7 + 0.3;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(_ShadowColor.rgb, _ShadowColor.a * i.fade);
            }
            ENDHLSL
        }
    }
}
