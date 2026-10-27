// 九九乘法戰機的地形：用頂點顏色上色 (草地、沙灘、山、河…)，有光影和霧
Shader "PhonicShooter/VertexColorLit"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow
        #pragma target 3.0

        fixed4 _Color;

        struct Input
        {
            float4 vcolor;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vcolor = v.color;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.vcolor.rgb * _Color.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
