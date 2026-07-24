Shader "Custom/PixelCutout"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "White" {}
        _Cutoff ("Cut Off", Range(0, 1)) = 0.5
        _PixelDensity ("Pixel Density", float) = 32
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest"
        "RenderType"="TransparentCutout"}

        Pass 
        {
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Cutoff;
            float _PixelDensity;
            float _LightColor0;

            struct appdata
            {
                float4 vertex : POSITION;
                float normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v) 
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            { 
                //Pixel Grid
                float2 grid = frac(i.uv * _PixelDensity);

                //Patron
                float mask = step(grid.x, _Cutoff) * step (grid.y, _Cutoff);
                clip(mask - 0.5);

                //Textura
                fixed4 col = tex2D (_MainTex, i.uv);

                // clip (col.a - _Cutoff);
                
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = max(0, dot(i.worldNormal, lightDir));
                // col.rgb *= NdotL * _LightColor0.rgb;
                
                return col;
            }
            ENDCG
        }
    }
}
