Shader "TankTown/Volume Particles"
{
    Properties
    {
        _MainTex("Density and volume lighting", 2D) = "white" {}
        _Fire("Emissive flame", Float) = 0
        _Emission("Intensity", Float) = 1
        _BurstOpacity("Burst fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; half _Fire; half _Emission; half _BurstOpacity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings o; o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS); o.uv = input.uv; o.color = input.color; return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv + sin(input.uv.yx * 12 + _Time.y * 7) * (.014 * _Fire);
                half4 density = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS)), _ZBufferParams);
                float eyeDepth = -TransformWorldToView(input.positionWS).z;
                half soft = saturate((sceneDepth - eyeDepth) * .8) * smoothstep(.6, 2.5, eyeDepth);
                half3 fill = half3(.50,.60,.76) + GetMainLight().color * .40;
                half3 smoke = input.color.rgb * fill * (.48 + density.r * .7);
                half core = density.b * density.b * saturate(input.color.g * 1.6);
                half3 fire = lerp(input.color.rgb, half3(1,.94,.56), core * .85) * (.75 + density.g * .9) * _Emission;
                return half4(lerp(smoke, fire, _Fire), input.color.a * density.a * soft * _BurstOpacity);
            }
            ENDHLSL
        }
    }
}
