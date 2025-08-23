Shader "LF/FishingTrip/TilesWaves"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _WavesHeight("Waves Height",Range(0,1)) = 1
        _WavesColor("Waves Color", Color) = (1,1,1,1)
        _WavesThickness("Waves Line Thickness",Range(1,6)) = 1
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
		LOD 100
	
		ZWrite On
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
                float2 uv0 : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv0 : TEXCOORD0;
                float3 worldPos : TEXCOORD3; 
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _WavesHeight;
            fixed4 _WavesColor;
            float _WavesThickness;

            float SimpleNoise( float2 pos , float t , float s )
			{
				pos *= s;
				float sin1 = sin( ( sin(pos.y*1.99) + pos.x + t*0.73 ) * 1.4523 );
				float sin2 = sin( ( sin(pos.y*0.72) + pos.x + t*0.57 ) * 2.2521 );
				float sin3 = sin( ( sin(pos.y*0.74) + pos.x + t*1.21 ) * 1.6242 );
				return ( sin1 + sin2 + sin3 + 3 ) * 0.15;
			}

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv0 = v.uv0;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
            	float mask = (i.worldPos.y-0.15 * _WavesHeight) *50 + SimpleNoise(i.worldPos.xz, _Time.y*4, 15);
            	float foamMask = (i.worldPos.y-0.1* _WavesHeight) *50 + SimpleNoise(i.worldPos.xz, _Time.y*4, 15);
            	mask = saturate(mask);

            	fixed foam = saturate(min(foamMask, 1-foamMask/_WavesThickness)*2);

            	fixed4 diff = tex2D(_MainTex, i.uv0);
            	
                fixed4 col = diff+foam*_WavesColor;

                col.a = mask * diff.a;
                return col;
            }
            ENDCG
        }
    }
}
