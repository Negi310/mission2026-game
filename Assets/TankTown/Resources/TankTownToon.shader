Shader "TankTown/Saturated Toon"
{
    Properties { _BaseColor("Color", Color) = (1,1,1,1) _Saturation("Saturation", Range(0,2)) = 1.15 _CameraVisibility("Camera visibility", Range(0,1)) = 1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half _Saturation;
        half _CameraVisibility;
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
        void CameraClip(float2 pixel)
        {
            // Stable ordered coverage; no temporal noise or transparent sorting changes.
            float2 low = fmod(floor(pixel), 2);
            float2 high = fmod(floor(pixel / 2), 2);
            float index = low.x * 8 + low.y * 4 + high.x * 2 + high.y;
            clip(_CameraVisibility - (index + .5) / 16.0);
        }
        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            CameraClip(input.positionCS.xy);
            Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
            half3 normal = normalize(input.normalWS);
            half ndl = saturate(dot(normal, light.direction));
            half band = ndl > .65 ? 1.06 : ndl > .2 ? .91 : .74;
            half lighting = lerp(.68, band, light.shadowAttenuation);
            half gray = dot(_BaseColor.rgb, half3(.2126,.7152,.0722));
            half3 color = saturate(lerp(gray.xxx, _BaseColor.rgb, _Saturation));
            half rim = pow(1 - saturate(dot(normal, GetWorldSpaceNormalizeViewDir(input.positionWS))), 3) * .12;
            return half4(color * lighting + rim * color, 1);
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
        half4 ShadowFrag() : SV_Target { return 0; }
        half4 DepthFrag(Varyings input) : SV_Target { CameraClip(input.positionCS.xy); return 0; }
        ENDHLSL
        Pass
        {
            Name "Forward" Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
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
