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
                half3 normal = normalize(i.normalWS);
                half3 key = normalize(float3(-.3, .8, -.5));
                half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half band = .64 + .4 * saturate(dot(normal, key));
                half edge = pow(1 - saturate(dot(normal, view)), 2);
                half highlight = pow(saturate(dot(normal, normalize(key + view))), 22) * .12;
                half3 fire = _BaseColor.rgb * band * (1 - edge * .14) + half3(1, .9, .58) * highlight;
                half nearFade = smoothstep(.8, 4.2, distance(i.positionWS, _WorldSpaceCameraPos));
                return half4(fire, _BaseColor.a * nearFade);
            }
            ENDHLSL
        }
    }
}
