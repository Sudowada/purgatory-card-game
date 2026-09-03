Shader "Custom/LiquidTierBar"
{
    Properties
    {
        _MainTex     ("Texture",    2D)    = "white" {}
        _Fill        ("Fill",       Range(0,1)) = 0.5
        _TierColor   ("Tier Color", Color) = (1,0.5,0.1,1)
        _WaveAmp     ("Wave Amp",   Range(0,0.05)) = 0.018
        _WaveSpeed   ("Wave Speed", Range(0,5))    = 1.2
        _WaveFreq    ("Wave Freq",  Range(1,40))   = 12.0
        _PixelSize   ("Pixel Size", Range(1,16))   = 4.0
        _ShimmerSpeed("Shimmer Speed", Range(0,3)) = 0.8
        _FlashColor  ("Flash Color",Color) = (0.4,0.7,1,0)
        _DebuffColor ("Debuff Color",Color)= (0.1,0.2,0.9,0)
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _Fill;
                float4 _TierColor;
                float  _WaveAmp;
                float  _WaveSpeed;
                float  _WaveFreq;
                float  _PixelSize;
                float  _ShimmerSpeed;
                float4 _FlashColor;
                float4 _DebuffColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color       = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // ── Pixelate UVs ─────────────────────────────────────────────
                float px = _PixelSize / 512.0;
                float2 pixUV = floor(uv / px) * px;

                // ── Animated liquid surface ───────────────────────────────────
                float t       = _Time.y;
                float wave    = sin(pixUV.x * _WaveFreq + t * _WaveSpeed) * _WaveAmp;
                float wave2   = sin(pixUV.x * _WaveFreq * 0.6 + t * _WaveSpeed * 1.4 + 1.3) * _WaveAmp * 0.5;
                float surface = _Fill + wave + wave2;

                // ── Fill mask ─────────────────────────────────────────────────
                float filled  = step(uv.y, surface);

                // ── Shimmer — scrolling bright band ──────────────────────────
                float shimPos = frac(t * _ShimmerSpeed);
                float shimX   = abs(pixUV.x - shimPos);
                float shim    = exp(-shimX * 18.0) * 0.22 * filled;

                // ── Depth gradient — darker at bottom, brighter near surface ─
                float depth   = uv.y / max(surface, 0.001);
                float grad    = lerp(0.55, 1.0, depth * depth);

                // ── Base liquid color ─────────────────────────────────────────
                half4 col     = _TierColor;
                col.rgb      *= grad;
                col.rgb      += shim;

                // ── Surface highlight line ────────────────────────────────────
                float surfDist  = abs(uv.y - surface);
                float highlight = exp(-surfDist * 180.0) * 0.7 * step(0.0, surface) * step(surface, 1.0);
                col.rgb        += highlight * 1.2;

                // ── Flash overlay (tier up) ───────────────────────────────────
                col.rgb = lerp(col.rgb, _FlashColor.rgb, _FlashColor.a);

                // ── Debuff overlay (tier down) ────────────────────────────────
                col.rgb = lerp(col.rgb, _DebuffColor.rgb, _DebuffColor.a);

                // ── Empty region — dark trough ────────────────────────────────
                half4 empty   = half4(0.06, 0.05, 0.03, 0.85);
                col           = lerp(empty, col, filled);

                // ── Pixel snap alpha edge ─────────────────────────────────────
                col.a *= IN.color.a;

                return col;
            }
            ENDHLSL
        }
    }
}
