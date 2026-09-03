Shader "Custom/ScoringCellSwirl"
{
    Properties
    {
        _MainTex     ("Texture",      2D)    = "white" {}
        _Color       ("Base Color",   Color) = (0.1, 0.18, 0.35, 1)
        _Color2      ("Swirl Color",  Color) = (0.15, 0.28, 0.55, 1)
        _Speed       ("Swirl Speed",  Range(0, 2))   = 0.4
        _Scale       ("Swirl Scale",  Range(1, 10))  = 4.0
        _Intensity   ("Intensity",    Range(0, 0.5)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float4    _Color;
            float4    _Color2;
            float     _Speed;
            float     _Scale;
            float     _Intensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = TRANSFORM_TEX(v.uv, _MainTex);
                o.color  = v.color;
                return o;
            }

            // Simple 2D hash
            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Smooth noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = dot(hash2(i + float2(0,0)), f - float2(0,0));
                float b = dot(hash2(i + float2(1,0)), f - float2(1,0));
                float c = dot(hash2(i + float2(0,1)), f - float2(0,1));
                float d = dot(hash2(i + float2(1,1)), f - float2(1,1));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 0.5 + 0.5;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float  t  = _Time.y * _Speed;

                // Two layers of noise drifting in different directions
                float2 uv1 = uv * _Scale + float2(t * 0.3,  t * 0.2);
                float2 uv2 = uv * _Scale + float2(-t * 0.2, t * 0.35);

                float n1 = noise(uv1);
                float n2 = noise(uv2 + float2(3.7, 1.5));

                // Combine — creates a swirling pattern
                float swirl = (n1 * 0.6 + n2 * 0.4);

                // Lerp between base and swirl color
                float4 col = lerp(_Color, _Color2, swirl * _Intensity * 2.0);

                // Respect vertex alpha (for UI fading)
                col.a *= i.color.a * _Color.a;

                return col;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
