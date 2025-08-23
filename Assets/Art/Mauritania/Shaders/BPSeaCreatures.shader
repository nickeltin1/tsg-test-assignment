Shader "LF/BattlePass/SeaCreatures"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent"}
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            fixed4 _Color;


            v2f vert (appdata v)
            {
                v2f o;
                

                
                o.uv.xy = v.uv0 - fmod(_Time.y,144) * float2(0.25,0)+ float2(0,sin(v.uv0.x*10)*0.02);
                o.uv.zw = v.uv1;


                float4 vertexZero = float4(v.vertex-v.normal*0.17,0);
                float Lerp = saturate( abs( ( o.uv.x - 0.5 ) * -5 ) -5 );
                v.vertex = v.vertex * (1-Lerp) + vertexZero * Lerp;

                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = _Color;
                col.a = tex2D(_MainTex, i.uv.xy).r;
                col.a *= saturate( 2-5*abs(i.uv.w*2 - 1));
                col.a *= _Color.a;

                return col;
            }
            ENDCG
        }
    }
}
