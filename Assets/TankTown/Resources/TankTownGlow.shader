Shader "TankTown/Arcade Glow"
{
    Properties { _MainTex("Texture", 2D) = "white" {} _Color("Tint", Color) = (1,1,1,1) _BurstOpacity("Burst fade", Range(0,1)) = 1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; half4 _Color; half _BurstOpacity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input) { Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); o.uv = TRANSFORM_TEX(input.uv, _MainTex); o.color = input.color; return o; }
            half4 Frag(Varyings input) : SV_Target { half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color * input.color; color.a *= _BurstOpacity; return color; }
            ENDHLSL
        }
    }
}
