Shader "TankTown/Saturated Toon"
{
    Properties
    {
        _BaseColor("Color", Color) = (1,1,1,1)
        _BaseMap("Candy albedo", 2D) = "white" {}
        _TextureRelief("Candy texture relief", Range(0,.3)) = .04
        _Saturation("Saturation", Range(0,2)) = 1.05
        _Gloss("Toy highlight", Range(0,1)) = .26
        _HighlightPower("Highlight sharpness", Range(8,128)) = 48
        _SurfaceDetail("Subtle surface grain", Range(0,.2)) = .035
        _DetailScale("Grain per metre", Range(1,60)) = 24
        _Brushed("Directional grain", Range(0,1)) = 0
        _GroundDetail("Road grain", Range(0,1)) = 0
        _CameraVisibility("Camera visibility", Range(0,1)) = 1
        _DebrisVisibility("Debris visibility", Range(0,1)) = 1
        _StructuralDamage("Structural damage", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        float4 _BaseMap_ST;
        half _TextureRelief;
        half _Saturation;
        half _Gloss;
        half _HighlightPower;
        half _SurfaceDetail;
        half _DetailScale;
        half _Brushed;
        half _GroundDetail;
        half _CameraVisibility;
        half _DebrisVisibility;
        half _StructuralDamage;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float3 surfacePosition : TEXCOORD2; float2 uv : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
            // Grain follows a rotating fragment, with density measured in metres.
            float3x3 objectToWorld = (float3x3)GetObjectToWorldMatrix();
            float3 scale = float3(length(mul(objectToWorld, float3(1,0,0))), length(mul(objectToWorld, float3(0,1,0))), length(mul(objectToWorld, float3(0,0,1))));
            output.surfacePosition = input.positionOS.xyz * scale;
            return output;
        }
        float GrainHash(float2 p)
        {
            float3 q = frac(float3(p.xyx) * .1031);
            q += dot(q, q.yzx + 33.33);
            return frac((q.x + q.y) * q.z);
        }
        float SurfaceGrain(float2 p)
        {
            float2 cell = floor(p), f = frac(p);
            f = f * f * (3 - 2 * f);
            return lerp(lerp(GrainHash(cell), GrainHash(cell + float2(1,0)), f.x),
                lerp(GrainHash(cell + float2(0,1)), GrainHash(cell + 1), f.x), f.y);
        }
        float FractureEdge(float2 p)
        {
            float2 cell = floor(p), f = frac(p);
            float nearest = 10, second = 10;
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
            {
                float2 offset = float2(x, y);
                float2 seed = cell + offset;
                float2 site = .2 + .6 * float2(GrainHash(seed), GrainHash(seed + float2(13, 71)));
                float2 d = offset + site - f;
                float distanceSquared = dot(d, d);
                if (distanceSquared < nearest) { second = nearest; nearest = distanceSquared; }
                else second = min(second, distanceSquared);
            }
            return second - nearest;
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
            half3 albedo = _BaseColor.rgb;
            #if defined(_CANDY_TEXTURE)
                half3 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                albedo *= texel;
                // Small texture relief changes the lighting, never the mesh or physics.
                float height = dot(texel, float3(.2126, .7152, .0722));
                float3 dx = ddx(input.positionWS), dy = ddy(input.positionWS);
                float3 rx = cross(dy, normal), ry = cross(normal, dx);
                float determinant = dot(dx, rx);
                float3 gradient = (ddx(height) * rx + ddy(height) * ry) * sign(determinant) / max(abs(determinant), .00001);
                normal = normalize(normal - clamp(gradient * _TextureRelief, -0.3, 0.3));
            #endif
            half ndl = saturate(dot(normal, light.direction));
            half gray = dot(albedo, half3(.2126,.7152,.0722));
            half3 color = saturate(lerp(gray.xxx, albedo, _Saturation));
            half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
            AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
            // Cool fill keeps shadow detail legible; warm sunlight carries the contrast.
            half hemisphere = lerp(.72, 1, saturate(normal.y * .5 + .5));
            half3 ambient = color * half3(.24, .31, .41) * hemisphere * lerp(.55, 1, ao.indirectAmbientOcclusion);
            half3 direct = color * light.color * .51 * pow(ndl, .85) * light.shadowAttenuation * ao.directAmbientOcclusion;
            half3 lit = ambient + direct;
            half highlight = pow(saturate(dot(normal, normalize(light.direction + view))), _HighlightPower) * _Gloss * light.shadowAttenuation * ao.directAmbientOcclusion;
            half rim = pow(1 - saturate(dot(normal, view)), 4) * .025;
            lit += half3(1, .95, .8) * highlight + color * rim;
            float3 p = input.surfacePosition;
            float2 grainPoint = float2(p.x + p.z * .37, p.y + p.z * .73) * _DetailScale;
            // Smooth noise fades before it becomes subpixel; distant surfaces stay calm.
            float footprint = max(length(ddx(grainPoint)), length(ddy(grainPoint)));
            half detailFade = (1 - smoothstep(.45, 1.4, footprint)) * (1 - smoothstep(12, 40, distance(input.positionWS, _WorldSpaceCameraPos)));
            // Distant grain already fades out; skip the noise work once it contributes nothing.
            if (detailFade > 0 && _SurfaceDetail + _GroundDetail > 0)
            {
                float grain = SurfaceGrain(grainPoint);
                if (_Brushed > 0)
                    grain = lerp(grain, SurfaceGrain(grainPoint * float2(1.8, .055)), _Brushed);
                lit *= 1 + (grain - .5) * (_SurfaceDetail + _GroundDetail * .025) * detailFade;
            }
            if (_StructuralDamage > .01)
            {
                float fade = 1 - smoothstep(20, 55, distance(input.positionWS, _WorldSpaceCameraPos));
                float crack = 0;
                if (fade > 0)
                {
                    float2 crackPoint = float2(p.x + p.z * .37, p.y + p.z * .73) * 1.15;
                    float ridge = FractureEdge(crackPoint);
                    float edge = max(.008, min(.04, fwidth(ridge)));
                    crack = 1 - smoothstep(.006, .006 + edge, ridge);
                }
                lit *= 1 - _StructuralDamage * (.08 + crack * .55 * fade);
            }
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
            #pragma multi_compile_local_fragment _ _CANDY_TEXTURE
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
