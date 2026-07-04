Shader "ZZNC/RadialBlur"
{
    Properties
    {
        _MainTex   ("Source",        2D)    = "white" {}
        _Intensity ("Intensity",   Float)   = 0.05
        _Samples   ("Samples",     Float)   = 10
        _CenterX   ("Center X",    Float)   = 0.5
        _CenterY   ("Center Y",    Float)   = 0.5
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Intensity;
            float _Samples;
            float _CenterX;
            float _CenterY;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 center = float2(_CenterX, _CenterY);
                float2 dir    = (i.uv - center) * _Intensity;
                int    n      = max((int)_Samples, 1);
                float2 step_  = dir / n;
                float4 col    = 0;
                float2 uv     = i.uv;

                for (int s = 0; s < n; s++)
                {
                    col += tex2D(_MainTex, uv);
                    uv  -= step_;
                }
                return col / n;
            }
            ENDCG
        }
    }
}
