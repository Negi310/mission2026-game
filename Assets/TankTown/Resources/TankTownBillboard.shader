Shader "TankTown/Billboard"
{
    Properties { _MainTex("Icon", 2D) = "white" {} _Color("Tint", Color) = (1,1,1,1) }
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
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; half4 _Color;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input) { Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); o.uv = TRANSFORM_TEX(input.uv, _MainTex); return o; }
            half4 Frag(Varyings input) : SV_Target { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color; }
            ENDHLSL
        }
    }
}
