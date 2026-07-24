Shader "Custom/PlayerSilhouette"
{
    Properties
    {
        _Color ("Silhouette Color", Color) = (1, 1, 1, 0.5)
        _EmissionColor ("Emission Color", Color) = (1, 1, 1, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 2)) = 1
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2
        _PulseAmplitude ("Pulse Amplitude", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags         
        {             
            "Queue" = "Transparent+100"             
            "RenderType" = "Transparent"             
            "IgnoreProjector" = "True"
        }
        
        Pass
        {
            Name "SilhouetteThroughWalls"
            
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };
            
            float4 _Color;
            float4 _EmissionColor;
            float _EmissionIntensity;
            float _PulseSpeed;
            float _PulseAmplitude;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                float fresnel = 1.0 - saturate(dot(i.worldNormal, i.viewDir));
                fresnel = pow(fresnel, 2);
                
                float pulse = sin(_Time.y * _PulseSpeed) * _PulseAmplitude;
                float alpha = _Color.a + pulse;
                alpha = saturate(alpha);
                
                float4 finalColor = _Color;
                finalColor.rgb += _EmissionColor.rgb * _EmissionIntensity * fresnel;
                finalColor.a = alpha * (0.5 + fresnel * 0.5); 
                
                return finalColor;
            }
            ENDCG
        }
    }
    Fallback "Transparent/Diffuse"
}