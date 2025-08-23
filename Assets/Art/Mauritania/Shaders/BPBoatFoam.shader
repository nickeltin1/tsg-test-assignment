Shader "LF/BattlePass/Boat Foam"
{
    Properties
    {
        _Phase ("Speed", Range(0,1))=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent"}
        LOD 100
        ZWrite Off 
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float color : COLOR;
            };

            float _Phase;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color.r + v.color.r * ( sin( (v.vertex.x *8 + sin(v.vertex.x * 2 + _Time.y*2) ) - _Time.y * 7)*0.4 + 1 );
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = 1;
                col.a = i.color * i.uv.y;
                col.a = saturate( pow(col.a,0.8) * 10 -4 );
                col.a *=  saturate(i.uv.y-0.8+_Phase);
                return col;
            }
            ENDCG
        }
    }
}
