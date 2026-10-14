// Unity 6.3 URP: use with a Full Screen Pass Renderer Feature.
// Injection: After Rendering Post Processing. Fetch Color Buffer: enabled.
// Requirements: Depth. Screen-space overlay; depth only controls near-surface fading.
// Color/brightness-adaptive fullscreen effect; dark albedo is not separable from shadows.
Shader "Purgatory/Fullscreen Dither"
{
    Properties
    {
        _Strength ("Effect Strength", Range(0, 1)) = 0.85
        _PixelStyle ("Pixel Dither Blend (0 = Round Dots)", Range(0, 1)) = 1
        _PixelSize ("Dither Pixel Size", Range(1, 6)) = 1
        _Grime ("Grime Variation", Range(0, 1)) = 0.2
        _Coverage ("Pattern Coverage", Range(0,1)) = 0.65
        _TransitionFocus ("Shadow Transition Focus", Range(0.5,4)) = 1.5
        _PatternSpacing ("Dot Spacing (Screen Pixels)", Range(2, 16)) = 4
        _NearClear ("No Dots Within (World Units)", Range(0, 3)) = 0.5
        _NearFull ("Full Dots Beyond (World Units)", Range(0.1, 6)) = 1.5
        _ShadowStart ("Highlight Fade Starts", Range(0, 1)) = 0.55
        _ShadowEnd ("No Dots Above Brightness", Range(0, 1)) = 0.85
        _DotSize ("Dot Radius", Range(0.05, 0.48)) = 0.32
        _DotDensity ("Dot Density", Range(0, 1)) = 0.8
        _SizeVariation ("Dot Size Variation", Range(0, 1)) = 0.45
        _DensityVariation ("Density Variation", Range(0, 1)) = 0.35
        _Jitter ("Dot Position Variation", Range(0, 0.4)) = 0.18
        _PatchScale ("Variation Patch Size (Screen Pixels)", Range(16, 512)) = 128
        _DotBrightness ("Dot Background Brightness", Range(0, 1)) = 0.35
        _ColorVariation ("Dot Color Variation", Range(0, 1)) = 0.2
        _InkTint ("Optional Ink Tint", Color) = (0.10, 0.08, 0.06, 1)
        _InkTintStrength ("Ink Tint Strength", Range(0, 1)) = 0
        _DeepFadeStart ("Deep Shadow Fade Starts", Range(0, 0.6)) = 0.18
        _DeepFadeEnd ("Deep Shadow Fade Ends", Range(0, 0.8)) = 0.42
        _ColorResponse ("Surface Color Response", Range(0, 1)) = 0.45
        _PaletteInfluence ("Adaptive Ink Color Strength", Range(0, 1)) = 0.3
        _Posterize ("Posterization Strength", Range(0, 1)) = 0
        _Levels ("Brightness Levels", Range(2, 12)) = 4
        _Contrast ("Contrast", Range(0.5, 2)) = 1
        _Exposure ("Exposure (Stops)", Range(-3, 3)) = 0
        _BlackPoint ("Shadow Black Point", Range(0, 0.3)) = 0
        _Saturation ("Saturation", Range(0, 1.5)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "PurgatoryDither"
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Strength, _PatternSpacing, _Levels, _Contrast;
                float _NearClear, _NearFull;
                float _PixelStyle, _PixelSize, _Grime;
                float _Coverage, _TransitionFocus;
                float _ShadowStart, _ShadowEnd;
                float _Exposure, _BlackPoint, _Saturation;
                float _DotSize, _DotDensity, _SizeVariation, _DensityVariation;
                float _Jitter, _PatchScale, _DotBrightness, _ColorVariation;
                float _InkTintStrength, _Posterize;
                float _DeepFadeStart, _DeepFadeEnd, _ColorResponse, _PaletteInfluence;
                float4 _InkTint;
            CBUFFER_END

            float Bayer4(uint2 p)
            {
                uint x = p.x & 3u;
                uint y = p.y & 3u;
                uint low = ((x & 1u) ^ (y & 1u)) * 2u + (y & 1u);
                uint high = (((x >> 1u) & 1u) ^ ((y >> 1u) & 1u)) * 2u + ((y >> 1u) & 1u);
                return (float(low * 4u + high) + 0.5) / 16.0;
            }

            float Hash21(float2 p)
            {
                float3 q = frac(float3(p.xyx) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            float PatchNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(cell), Hash21(cell + float2(1, 0)), f.x),
                    lerp(Hash21(cell + float2(0, 1)),
                    Hash21(cell + float2(1, 1)), f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 original = SAMPLE_TEXTURE2D_X(
                    _BlitTexture, sampler_LinearClamp, input.texcoord);

                // Work in perceptual space; preserve local surface colors.
                float3 color = max(original.rgb, 0.0);
                #ifndef UNITY_COLORSPACE_GAMMA
                    color = LinearToSRGB(color);
                #endif
                // This response is sampled from the scene, before optional grading.
                float sourceLuma = dot(color, float3(0.2126, 0.7152, 0.0722));
                float maxChannel = max(color.r, max(color.g, color.b));
                float minChannel = min(color.r, min(color.g, color.b));
                float chroma = saturate((maxChannel-minChannel) / max(maxChannel,0.001));
                float warmth = clamp((color.r-color.b) / max(maxChannel,0.001),-1.0,1.0);
                float colorProtection = 1.0 - chroma * saturate(_ColorResponse) * 0.45;
                float tone = saturate(sourceLuma);
                color *= exp2(_Exposure);
                float luma = dot(color, float3(0.2126, 0.7152, 0.0722));
                color = lerp(luma.xxx, color, _Saturation);
                float brightness = saturate((luma - _BlackPoint)
                    / max(1.0 - _BlackPoint, 0.001));
                brightness = saturate((brightness - 0.5) * _Contrast + 0.5);

                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                float rawDepth = SampleSceneDepth(screenUV);
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.000001) return original;
                    float depth = rawDepth;
                #else
                    if (rawDepth >= 0.999999) return original;
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                float3 worldPos = ComputeWorldSpacePosition(
                    screenUV, depth, UNITY_MATRIX_I_VP);

                // Depth controls visibility, but the pattern stays on the screen.
                float distanceToCamera = length(worldPos - GetCameraPositionWS());
                float nearMask = smoothstep(_NearClear,
                    max(_NearFull, _NearClear + 0.001), distanceToCamera);
                float2 plane = input.positionCS.xy;
                float spacing = max(_PatternSpacing, 2.0);
                float2 grid = plane / spacing;
                float2 cell = floor(grid);
                float randomSize = Hash21(cell + float2(17, 59));
                float randomPresence = Hash21(cell + float2(83, 11));
                float randomColor = Hash21(cell + float2(41, 97));
                float2 center = 0.5 + _Jitter * (float2(
                    Hash21(cell + float2(7, 31)),
                    Hash21(cell + float2(29, 3))) * 2.0 - 1.0);
                float patch = PatchNoise(plane / max(_PatchScale, 16.0));
                float darkness = 1.0 - tone;
                // Larger, more numerous dots in darker areas. Broad patches
                // and individual cells vary, without time-based randomness.
                float radius = _DotSize * lerp(0.4, 1.0, darkness)
                    * lerp(1.0, lerp(0.55, 1.35, randomSize), _SizeVariation);
                radius = min(radius, 0.48);
                float probability = _DotDensity * colorProtection * lerp(0.2, 1.0, darkness)
                    * lerp(1.0, lerp(0.45, 1.35, patch), _DensityVariation);
                float presence = step(randomPresence, saturate(probability));
                float dotDistance = length(frac(grid) - center);
                // Smooth only the dot edges, with screen derivative AA.
                float edgeAA = max(0.005, 0.6 / spacing);
                float dotMask = (1.0 - smoothstep(radius - edgeAA,
                    radius + edgeAA, dotDistance)) * presence;

                // Protect bright saturated colors as well as bright neutral colors.
                float highlightBrightness = max(tone, saturate(maxChannel));
                float shadowMask = 1.0 - smoothstep(_ShadowStart,
                    max(_ShadowEnd, _ShadowStart + 0.001), highlightBrightness);
                // Very dark areas stay quiet, without sparse bright speckles.
                shadowMask *= smoothstep(_DeepFadeStart,
                    max(_DeepFadeEnd, _DeepFadeStart + 0.001), tone);
                shadowMask *= colorProtection;
                shadowMask = pow(saturate(shadowMask),max(_TransitionFocus,0.5));
                // Broad softly bounded gaps interrupt continuous screen coverage.
                // Coverage 1 removes the patch mask; coverage 0 removes all patterns.
                float patchMask = smoothstep(1.0-_Coverage-0.18,
                    1.0-_Coverage+0.18,patch);
                patchMask *= smoothstep(0.0,0.08,_Coverage);
                patchMask = lerp(patchMask,1.0,smoothstep(0.92,1.0,_Coverage));
                float patternMask = shadowMask * nearMask * patchMask;

                float steps = max(round(_Levels) - 1.0, 1.0);
                float quantized = floor(brightness * steps + 0.5) / steps;
                float baseBrightness = lerp(brightness, quantized,
                    _Posterize * patternMask);
                float3 baseColor = saturate(color
                    * (baseBrightness / max(luma, 0.0001)));
                // Derive ink from the local background rather than universal black.
                float inkBrightness = saturate(_DotBrightness
                    + tone * 0.18 + chroma * _ColorResponse * 0.1) * lerp(1.0,
                    lerp(0.65, 1.25, randomColor), _ColorVariation);
                // A surface-relative palette: warmth shifts ink toward warm tones;
                // cool surfaces shift toward cool tones. Neutral hues remain neutral.
                float3 adaptivePalette = lerp(float3(0.68,0.78,0.95),
                    float3(0.95,0.78,0.68), warmth * 0.5 + 0.5);
                float3 localInk = baseColor * saturate(inkBrightness)
                    * lerp(float3(1,1,1),adaptivePalette,saturate(_PaletteInfluence));
                float3 tint = _InkTint.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    tint = LinearToSRGB(tint);
                #endif
                float3 ink = lerp(localInk, tint, _InkTintStrength);
                // Bound the optional tint so dots remain darker than their surface.
                ink = min(ink, baseColor);
                float amount = dotMask * patternMask;
                float3 roundResult = lerp(baseColor, ink, saturate(amount));
                // Sharp ordered pixels, rather than anti-aliased circular dots.
                float pixelSize = max(round(_PixelSize), 1.0);
                uint2 pixel = (uint2)floor(input.positionCS.xy / pixelSize);
                float threshold = Bayer4(pixel);
                // Local texture color drives brightness; a subtle fixed patch
                // changes the thresholds so the grid is not perfectly uniform.
                threshold = saturate(threshold + (patch - 0.5) * _Grime * 0.35);
                float scaledBrightness = brightness * steps;
                float ditherBrightness = (floor(scaledBrightness)
                    + step(threshold, frac(scaledBrightness))) / steps;
                float3 pixelColor = saturate(color
                    * (ditherBrightness / max(luma,0.0001)));
                float3 pixelResult = lerp(baseColor,pixelColor,
                    saturate(patternMask));
                float3 result = lerp(roundResult,pixelResult,saturate(_PixelStyle));
                #ifndef UNITY_COLORSPACE_GAMMA
                    result = SRGBToLinear(result);
                #endif
                return float4(lerp(original.rgb, result, _Strength), original.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
