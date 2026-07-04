Shader "ZZNC/PieceMotionStretch"
{
    Properties
    {
        _MainTex      ("Sprite Texture", 2D)      = "white" {}
        _Color        ("Tint",           Color)   = (1,1,1,1)
        // 以下由脚本每帧通过 MaterialPropertyBlock 写入，无需手动配置
        _StretchDir   ("Stretch Dir (local XY)",  Vector) = (0,1,0,0)
        _StretchAmount("Stretch Amount",  Range(0,1)) = 0
        _BlurSamples  ("Blur Samples",   Range(1,8))  = 5
        _BlurSpread   ("Blur UV Spread", Range(0,0.5))= 0.12
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
            "CanUseSpriteAtlas" = "True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed4    _Color;
            float4    _StretchDir;    // xy = 本地空间方向（normalized）
            float     _StretchAmount; // 0=无效果, 1=全拉伸
            int       _BlurSamples;
            float     _BlurSpread;   // 拖尾在 UV 上的总偏移幅度

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float2 blurStep : TEXCOORD1; // 每次采样的 UV 步长
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;

                // --- 顶点拉伸 ---
                // 把顶点位置在本地空间沿 stretchDir 方向放大
                // dot(v, dir) 取顶点在 dir 上的投影分量，乘以 stretchAmount 再叠加
                float2 dir = normalize(_StretchDir.xy + float2(0.0001, 0)); // 避免零向量
                float  proj = dot(IN.vertex.xy, dir);
                IN.vertex.xy += dir * proj * _StretchAmount * 0.6;

                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv     = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color  = IN.color * _Color;

                // --- 拖尾步长 ---
                // 将本地 dir 近似映射到 UV 空间（对齐 sprite quad 的 UV 布局）
                // sprite quad: local(-0.5~0.5) → uv(0~1), 所以 UV 方向与 local 方向一致
                // 拖尾方向与运动方向相反（身后）
                OUT.blurStep = -dir * _BlurSpread * _StretchAmount / max(_BlurSamples - 1, 1);

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 多次采样叠加，越靠后权重越低（线性渐隐）
                fixed4 col      = fixed4(0, 0, 0, 0);
                float  wTotal   = 0;
                int    samples  = max(1, _BlurSamples);

                for (int i = 0; i < samples; i++)
                {
                    float  t      = (float)i / max(samples - 1, 1); // 0=当前帧, 1=最远拖尾
                    float  weight = 1.0 - t * 0.85;                  // 越远越透明
                    float2 uv     = IN.uv + IN.blurStep * i;
                    col    += tex2D(_MainTex, uv) * weight;
                    wTotal += weight;
                }

                col    /= wTotal;
                col    *= IN.color;
                return col;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
