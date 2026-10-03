Shader "TankTown/Sky Gradient"
{
    Properties
    {
        _Horizon("Horizon", Color) = (.5,.86,1,1)
        _Zenith("Zenith", Color) = (.12,.56,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Horizon; half4 _Zenith;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz; return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                half3 sky = lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(direction.y), .65));
                float sun = pow(saturate(dot(direction, normalize(float3(-.4,.6,.7)))), 240);
                return half4(lerp(sky, half3(1,.97,.73), sun), 1);
            }
            ENDHLSL
        }
    }
}
