// Unity 6.3 URP opaque surface shader. Designed for base-map-only handmade assets.
// Realtime Forward / Forward+ lights, shadows, SH ambient, depth, SSAO normals.
// Initial version: no baked lightmaps, normal maps, metallic maps or reflection probes.
Shader "Purgatory/Lighting Aware Surface"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        _PatternDensity ("Pattern Cells Per Object Unit", Range(8,128)) = 48
        _PatternStrength ("Pattern Strength", Range(0,1)) = 0.65
        _PatternVariation ("Pattern Variation", Range(0,1)) = 0.25
        _LightResponse ("Light Response Reference", Range(0.05,4)) = 0.5
        _ShadowContrast ("Dot Darkness", Range(0,1)) = 0.5
        _ShadowTint ("Shadow Palette Tint", Color) = (0.65,0.72,0.85,1)
        _TintStrength ("Shadow Tint Strength", Range(0,1)) = 0.2
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 1
        _Smoothness ("Smoothness", Range(0,1)) = 0.2
        _SpecularStrength ("Highlight Strength", Range(0,0.5)) = 0.04
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor, _ShadowTint, _EmissionColor;
            float _PatternDensity, _PatternStrength, _PatternVariation, _LightResponse;
            float _ShadowContrast, _TintStrength, _AmbientStrength, _Smoothness;
            float _SpecularStrength, _Cull;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            float3 positionOS : TEXCOORD3;
            float3 normalOS : TEXCOORD4;
            half fogFactor : TEXCOORD5;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings SurfaceVert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionOS = input.positionOS.xyz;
            output.normalOS = input.normalOS;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        float Hash21(float2 p)
        {
            float3 q = frac(float3(p.xyx) * 0.1031);
            q += dot(q, q.yzx + 33.33);
            return frac((q.x + q.y) * q.z);
        }
        float DotCoverage(float2 plane, float lightLevel)
        {
            float2 grid = plane * max(_PatternDensity, 1.0);
            float2 cell = floor(grid);
            float variation = Hash21(cell + float2(19,53));
            float2 center = 0.5 + _PatternVariation * 0.15 * (float2(
                Hash21(cell + float2(7,31)), Hash21(cell + float2(29,3))) * 2.0 - 1.0);
            float shadow = saturate(1.0 - lightLevel);
            // As lighting falls, dots grow, become more numerous, then merge.
            float radius = lerp(0.015, 0.73, pow(shadow, 0.85));
            radius *= lerp(1.0, lerp(0.8,1.2,variation), _PatternVariation);
            float probability = lerp(0.15,1.0,smoothstep(0.05,0.7,shadow));
            float presence = step(Hash21(cell + float2(83,11)), probability);
            float distanceToCenter = length(frac(grid) - center);
            float footprint = max(length(ddx(grid)), length(ddy(grid)));
            float aa = max(0.008, footprint * 0.55);
            float dots = (1.0 - smoothstep(radius-aa,radius+aa,distanceToCenter)) * presence;
            // Distant dots fade toward their approximate average coverage.
            float average = saturate(3.14159265 * radius * radius) * probability;
            return lerp(dots,average,smoothstep(0.5,1.5,footprint));
        }
        void AccumulateLight(Light light, float3 normalWS, float3 viewWS,
            inout float3 diffuse, inout float3 specular)
        {
            float ndotl = saturate(dot(normalWS, light.direction));
            float3 energy = light.color * light.distanceAttenuation * light.shadowAttenuation;
            diffuse += energy * ndotl;
            float3 h = SafeNormalize(light.direction + viewWS);
            float exponent = exp2(3.0 + _Smoothness * 8.0);
            float highlight = pow(saturate(dot(normalWS,h)),exponent) * ndotl;
            specular += energy * highlight * _SpecularStrength;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SurfaceVert
            #pragma fragment SurfaceFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            half4 SurfaceFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float4 shadowCoord;
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                float3 direct = 0;
                float3 specular = 0;
                Light mainLight = GetMainLight(shadowCoord);
                AccumulateLight(mainLight,inputData.normalWS,inputData.viewDirectionWS,direct,specular);
                #if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
                    #if USE_CLUSTER_LIGHT_LOOP
                        UNITY_LOOP for(uint lightIndex=0;
                            lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS);
                            lightIndex++)
                        {
                            Light light = GetAdditionalLight(lightIndex,input.positionWS,half4(1,1,1,1));
                            AccumulateLight(light,inputData.normalWS,inputData.viewDirectionWS,direct,specular);
                        }
                    #endif
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex,input.positionWS,half4(1,1,1,1));
                        AccumulateLight(light,inputData.normalWS,inputData.viewDirectionWS,direct,specular);
                    LIGHT_LOOP_END
                #endif
                float3 ambient = max(SampleSH(inputData.normalWS),0.0) * _AmbientStrength;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                    ambient *= ao.indirectAmbientOcclusion;
                    direct *= ao.directAmbientOcclusion;
                #endif
                float3 illumination = max(direct + ambient,0.0);
                // Use incoming light BEFORE the albedo. Dark paint and actual
                // shadows are distinct; lamp color remains in the shaded result.
                float energy = dot(illumination,float3(0.2126,0.7152,0.0722));
                float lightLevel = saturate(energy / max(_LightResponse,0.001));
                // Blend planar projections in object space: follows moving props
                // and does not rotate or change projection with the camera.
                float3 weights = pow(abs(normalize(input.normalOS)),8.0);
                weights /= max(weights.x + weights.y + weights.z,0.0001);
                float dots = DotCoverage(input.positionOS.zy,lightLevel) * weights.x
                    + DotCoverage(input.positionOS.xz,lightLevel) * weights.y
                    + DotCoverage(input.positionOS.xy,lightLevel) * weights.z;
                float shadowAmount = 1.0-lightLevel;
                float transition = 4.0*lightLevel*shadowAmount;
                float effect = saturate(_PatternStrength) * transition;
                float3 albedo = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb * _BaseColor.rgb;
                float3 diffuseColor = albedo * illumination;
                float3 palette = lerp(float3(1,1,1),_ShadowTint.rgb,
                    saturate(_TintStrength) * shadowAmount);
                float3 shade = diffuseColor * palette;
                shade *= 1.0 - dots * _ShadowContrast * effect;
                // Specular highlights and emission are outside the pattern.
                float3 finalColor = shade + specular + _EmissionColor.rgb;
                finalColor = MixFog(finalColor,input.fogFactor);
                return half4(finalColor,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            Varyings ShadowVert(Attributes input)
            {
                Varyings output = SurfaceVert(input);
                float3 direction = _LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    direction = normalize(_LightPosition-output.positionWS);
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(
                    output.positionWS,normalize(output.normalWS),direction));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #endif
                return output;
            }
            half4 DepthFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SurfaceVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            half4 DepthFrag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SurfaceVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalsFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normalWS);
                    return half4(PackFloat2To888(saturate(oct*0.5+0.5)),0);
                #else
                    return half4(normalWS,0);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
