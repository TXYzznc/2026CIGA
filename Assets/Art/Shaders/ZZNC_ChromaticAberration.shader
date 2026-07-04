Shader "ZZNC/ChromaticAberration"
{
    Properties
    {
        _MainTex   ("Texture",   2D)            = "white" {}
        _Intensity ("Intensity", Range(0, 0.1)) = 0
    }
    SubShader
    {
        Pass
        {
            ZTest Always
            Cull  Off
            ZWrite Off

            CGPROGRAM
            #pragma vertex   vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float     _Intensity;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv  = i.uv;
                float2 dir = (uv - 0.5) * _Intensity;

                fixed4 col;
                col.r = tex2D(_MainTex, uv - dir).r;
                col.g = tex2D(_MainTex, uv       ).g;
                col.b = tex2D(_MainTex, uv + dir).b;
                col.a = 1.0;
                return col;
            }
            ENDCG
        }
    }
}
