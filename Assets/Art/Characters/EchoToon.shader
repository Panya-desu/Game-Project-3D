// Anime-style toon shading for the Echo characters (URP 17, Forward and Forward+):
// two-tone cel shading with a soft edge and a coloured (not grey) shadow tone, warm ambient fill,
// rim light, an optional hair highlight band, and an inverted-hull outline.
// It does not sample screen-space AO, so the characters stay clean instead of speckled.
// Faces are drawn from both sides (the collar fold and the eyes are single-sided shells), cast shadows on
// the character are softened (low-resolution lamp shadows made jagged marks on the face), eyes can be
// partly self-lit, and hair can sway: the script HairSway feeds a lagging offset per renderer that bends
// vertices more the further they hang below an anchor (the crown of the head).
Shader "Echo/Toon"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ShadeColor ("Shade Tint", Color) = (0.78, 0.68, 0.76, 1)
        _ShadeThreshold ("Shade Threshold", Range(-1, 1)) = 0.05
        _ShadeSoftness ("Shade Softness", Range(0.001, 1)) = 0.12
        _ShadeAmount ("Shade Amount", Range(0, 1)) = 1
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.6
        _RimColor ("Rim Color", Color) = (1, 0.95, 0.9, 1)
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.35
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5
        _SpecColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _SpecStrength ("Highlight Strength", Range(0, 1)) = 0
        _SpecThreshold ("Highlight Threshold", Range(0.5, 1)) = 0.93
        _Emission ("Emission", Color) = (0, 0, 0, 0)
        _OutlineColor ("Outline Color", Color) = (0.22, 0.17, 0.2, 1)
        _OutlineWidth ("Outline Width", Range(0, 4)) = 1
        _OutlineDepthOffset ("Outline Depth Offset (m)", Range(0, 0.05)) = 0.012
        _SelfLit ("Self Lit", Range(0, 1)) = 0
        _ShadowStrength ("Received Shadow Strength", Range(0, 1)) = 0.85
        _SwayAmount ("Sway Amount", Range(0, 1)) = 0
        // the skirt marks its pixels; the thigh fill under it is only drawn where the skirt is not
        [IntRange] _StencilRef ("Stencil Ref", Range(0, 255)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Op", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _ShadeColor, _RimColor, _SpecColor, _Emission, _OutlineColor;
            half _ShadeThreshold, _ShadeSoftness, _ShadeAmount, _AmbientStrength;
            half _RimStrength, _RimPower, _SpecStrength, _SpecThreshold, _OutlineWidth;
            half _OutlineDepthOffset, _SelfLit, _ShadowStrength, _SwayAmount;
        CBUFFER_END

        // set per renderer by HairSway (MaterialPropertyBlock): xyz anchor + w length, and the lagging offset
        float4 _SwayAnchor;
        float4 _SwayOffset;

        float3 Sway(float3 positionWS)
        {
            if (_SwayAmount <= 0 || _SwayAnchor.w <= 0) return positionWS;
            float d = saturate((_SwayAnchor.y - positionWS.y) / _SwayAnchor.w);
            return positionWS + _SwayOffset.xyz * (d * d * _SwayAmount);
        }
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            Stencil { Ref [_StencilRef] Comp [_StencilComp] Pass [_StencilOp] }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 positionWS = Sway(TransformObjectToWorld(v.positionOS.xyz));
                o.positionCS = TransformWorldToHClip(positionWS);
                o.positionWS = positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half Ramp(half ndl, half shadow)
            {
                half lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndl);
                return min(lit, lerp(1.0h, shadow, _ShadowStrength));
            }

            half3 Toon(half3 albedo, half3 n, half3 v, Light light, half shadow)
            {
                half ndl = dot(n, light.direction);
                half ramp = Ramp(ndl, shadow);
                half3 shade = lerp(albedo, albedo * _ShadeColor.rgb, _ShadeAmount);
                half3 c = lerp(shade, albedo, ramp) * light.color * light.distanceAttenuation;
                if (_SpecStrength > 0)
                {
                    half3 h = normalize(light.direction + v);
                    half band = smoothstep(_SpecThreshold, _SpecThreshold + 0.02h, saturate(dot(n, h)));
                    c += _SpecColor.rgb * band * _SpecStrength * ramp * light.color * light.distanceAttenuation;
                }
                return c;
            }

            half4 frag(Varyings i, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half3 albedo = tex.rgb;
                half3 n = normalize(i.normalWS) * (frontFace ? 1.0h : -1.0h);
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 color = Toon(albedo, n, v, mainLight, mainLight.shadowAttenuation);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    color += Toon(albedo, n, v, l, l.shadowAttenuation);
                }
                #endif
                LIGHT_LOOP_BEGIN(lightCount)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    color += Toon(albedo, n, v, l, l.shadowAttenuation);
                LIGHT_LOOP_END
                #endif

                // soft ambient so the shadow side keeps its colour, plus a rim of light along the silhouette
                half3 ambient = SampleSH(n) * _AmbientStrength;
                color += albedo * ambient;
                half rim = pow(saturate(1.0h - saturate(dot(n, v))), _RimPower) * _RimStrength;
                color += _RimColor.rgb * rim * (0.35h + 0.65h * saturate(dot(n, mainLight.direction) + 0.5h)) * mainLight.color;
                color += _Emission.rgb;
                color = lerp(color, albedo + _Emission.rgb, _SelfLit); // eyes keep their colour in dim rooms

                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Inverted-hull outline: back faces pushed out along the normal by a near-constant screen width
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            Stencil { Ref [_StencilRef] Comp [_StencilComp] Pass [_StencilOp] }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                if (_OutlineWidth <= 0)
                {
                    o.positionCS = float4(2, 2, 2, 1); // outside the view: no outline for this material
                    return o;
                }
                // pushed a little away from the camera so the shell does not cover the chin, the eye
                // sockets or other surfaces just in front of it
                float3 positionVS = TransformWorldToView(Sway(TransformObjectToWorld(v.positionOS.xyz)));
                positionVS.z -= _OutlineDepthOffset;
                float4 positionCS = TransformWViewToHClip(positionVS);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                float2 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy;
                float2 offset = normalize(normalCS + 1e-5) * _OutlineWidth * 0.0018;
                offset.x *= _ScreenParams.y / _ScreenParams.x;
                positionCS.xy += offset * min(positionCS.w, 4.0);
                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half3 c = lerp(_OutlineColor.rgb, tex * _OutlineColor.rgb * 1.6h, 0.4h);
                return half4(MixFog(c, i.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = Sway(TransformObjectToWorld(v.positionOS.xyz));
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                o.positionCS = ApplyShadowClamping(positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v) { Varyings o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformWorldToHClip(Sway(TransformObjectToWorld(v.positionOS.xyz))); return o; }
            half frag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(Sway(TransformObjectToWorld(v.positionOS.xyz)));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
