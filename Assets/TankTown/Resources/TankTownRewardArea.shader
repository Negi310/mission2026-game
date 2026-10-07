Shader "TankTown/Reward Area"
{
    Properties { _Color("Item color", Color) = (1,1,1,.8) }
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
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); o.uv = input.uv; return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float height = saturate(input.uv.y);
                float columns = pow(saturate(cos(input.uv.x * 48 * 6.2831853)), 8);
                float rising = pow(saturate(1 - abs(frac(height * 1.8 - _Time.y * .85 + input.uv.x * 7) - .5) * 2), 4);
                float fade = pow(1 - height, 1.7);
                float alpha = fade * (.04 + columns * (.14 + rising * .75));
                float rim = 1 - smoothstep(0, .08, height);
                alpha += rim * .22;
                half3 light = lerp(_Color.rgb, half3(1,1,1), rising * columns * .45);
                return half4(light * 1.6, _Color.a * alpha);
            }
            ENDHLSL
        }
    }
}
