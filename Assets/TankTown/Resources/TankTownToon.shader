Shader "TankTown/Saturated Toon"
{
    Properties
    {
        _BaseColor("Color", Color) = (1,1,1,1)
        _Saturation("Saturation", Range(0,2)) = 1.05
        _Gloss("Toy highlight", Range(0,1)) = .26
        _GroundDetail("Road grain", Range(0,1)) = 0
        _CameraVisibility("Camera visibility", Range(0,1)) = 1
        _DebrisVisibility("Debris visibility", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half _Saturation;
        half _Gloss;
        half _GroundDetail;
        half _CameraVisibility;
        half _DebrisVisibility;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return output;
        }
        void CoverageClip(float visibility, float2 pixel)
        {
            // Stable ordered coverage; no temporal noise or transparent sorting changes.
            float2 low = fmod(floor(pixel), 2);
            float2 high = fmod(floor(pixel / 2), 2);
            float index = low.x * 8 + low.y * 4 + high.x * 2 + high.y;
            clip(visibility - (index + .5) / 16.0);
        }
        void CameraClip(float2 pixel) { CoverageClip(_CameraVisibility * _DebrisVisibility, pixel); }
        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            CameraClip(input.positionCS.xy);
            Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
            half3 normal = normalize(input.normalWS);
            half ndl = saturate(dot(normal, light.direction));
            half gray = dot(_BaseColor.rgb, half3(.2126,.7152,.0722));
            half3 color = saturate(lerp(gray.xxx, _BaseColor.rgb, _Saturation));
            half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
            AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
            // Cool fill keeps shadow detail legible; warm sunlight carries the contrast.
            half hemisphere = lerp(.72, 1, saturate(normal.y * .5 + .5));
            half3 ambient = color * half3(.24, .31, .41) * hemisphere * lerp(.55, 1, ao.indirectAmbientOcclusion);
            half3 direct = color * light.color * .51 * pow(ndl, .85) * light.shadowAttenuation * ao.directAmbientOcclusion;
            half3 lit = ambient + direct;
            half highlight = pow(saturate(dot(normal, normalize(light.direction + view))), 42) * _Gloss * light.shadowAttenuation * ao.directAmbientOcclusion;
            half rim = pow(1 - saturate(dot(normal, view)), 4) * .025;
            lit += half3(1, .95, .8) * highlight + color * rim;
            float grain = frac(sin(dot(floor(input.positionWS.xz * 12), float2(12.9898, 78.233))) * 43758.5453) - .5;
            half detailFade = 1 - smoothstep(12, 45, distance(input.positionWS, _WorldSpaceCameraPos));
            lit *= 1 + grain * .06 * _GroundDetail * detailFade;
            // Compute fog per fragment: the ocean is one large quad, whose corner
            // fog values would otherwise wash out even water beside the player.
            half fog = ComputeFogFactor(TransformWorldToHClip(input.positionWS).z);
            return half4(MixFog(lit, fog), 1);
        }
        float3 _LightDirection;
        float4 ShadowVert(Attributes input) : SV_POSITION
        {
            UNITY_SETUP_INSTANCE_ID(input);
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            float4 clip = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
            #if UNITY_REVERSED_Z
            clip.z = min(clip.z, UNITY_NEAR_CLIP_VALUE * clip.w);
            #else
            clip.z = max(clip.z, UNITY_NEAR_CLIP_VALUE * clip.w);
            #endif
            return clip;
        }
        half4 ShadowFrag(float4 positionCS : SV_POSITION) : SV_Target { CoverageClip(_DebrisVisibility, positionCS.xy); return 0; }
        half4 DepthFrag(Varyings input) : SV_Target { CameraClip(input.positionCS.xy); return 0; }
        half4 NormalsFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            CameraClip(input.positionCS.xy);
            half3 normal = normalize(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(normal);
                return half4(PackFloat2To888(saturate(oct * .5 + .5)), 0);
            #else
                return half4(normal, 0);
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "Forward" Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
