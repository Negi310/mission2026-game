Shader "TankTown/Explosion Fire"
{
    Properties { _BaseColor("Color", Color) = (1, .6, .02, 1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS); o.normalWS = TransformObjectToWorldNormal(i.normalOS); return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half band = dot(normalize(i.normalWS), normalize(float3(-.3, .8, -.5))) > .25 ? 1 : .78;
                half nearFade = smoothstep(.8, 4.2, distance(i.positionWS, _WorldSpaceCameraPos));
                return half4(_BaseColor.rgb * band, .92 * nearFade);
            }
            ENDHLSL
        }
    }
}
