Shader "TankTown/Item Energy"
{
    Properties { _Color("Color", Color) = (0.1, 0.9, 1, 0.7) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float4 color : COLOR; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv; o.color = v.color;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float stripe = .72 + .28 * sin(i.uv.x * 70 - _Time.y * 22);
                float fade = smoothstep(.5, 3.5, distance(i.positionWS, _WorldSpaceCameraPos));
                float4 c = _Color * i.color;
                return half4(c.rgb * 1.8, c.a * stripe * fade);
            }
            ENDHLSL
        }
    }
}
