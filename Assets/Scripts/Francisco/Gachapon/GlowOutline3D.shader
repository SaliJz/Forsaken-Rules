Shader "Custom/GlowOutline3D"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (1,1,1,1)

        [Header(Outline)]
        [HDR]
        _OutlineColor ("Outline Color", Color) = (1,1,0,1)
        _OutlineIntensity ("Outline Intensity", Range(0,20)) = 4
        _OutlinePower ("Outline Power", Range(0.1, 8)) = 3

        [Header(Glow)]
        [HDR]
        _EmissionColor ("Emission Color", Color) = (1,1,0,1)
        _EmissionStrength ("Emission Strength", Range(0,20)) = 2

        [Header(Pulse)]
        _PulseSpeed ("Pulse Speed", Range(0,10)) = 2
        _PulseMin ("Pulse Min", Range(0,5)) = 0.2
        _PulseMax ("Pulse Max", Range(0,10)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Name "ForwardLit"

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _Color;

            float4 _OutlineColor;
            float _OutlineIntensity;
            float _OutlinePower;

            float4 _EmissionColor;
            float _EmissionStrength;

            float _PulseSpeed;
            float _PulseMin;
            float _PulseMax;

            v2f vert (appdata v)
            {
                v2f o;

                o.pos = UnityObjectToClipPos(v.vertex);

                float3 worldPos =
                    mul(unity_ObjectToWorld, v.vertex).xyz;

                o.worldNormal =
                    UnityObjectToWorldNormal(v.normal);

                o.viewDir =
                    normalize(_WorldSpaceCameraPos - worldPos);

                o.uv =
                    TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normal =
                    normalize(i.worldNormal);

                float3 viewDir =
                    normalize(i.viewDir);

                fixed4 tex =
                    tex2D(_MainTex, i.uv) * _Color;

                float fresnel =
                    1.0 - saturate(dot(normal, viewDir));

                fresnel =
                    pow(fresnel, _OutlinePower);

                float3 outline =
                    _OutlineColor.rgb *
                    fresnel *
                    _OutlineIntensity;

                float emissionPulse =
                    lerp(
                        _PulseMin,
                        _PulseMax,
                        (sin(_Time.y * _PulseSpeed) * 0.5) + 0.5
                    );

                float3 emission =
                    _EmissionColor.rgb *
                    emissionPulse *
                    (_EmissionStrength * 8);

                float3 finalColor =
                    lerp(
                        tex.rgb,
                        tex.rgb + emission,
                        emissionPulse
                    );

                finalColor += outline;

                return float4(finalColor, tex.a);
            }

            ENDHLSL
        }
    }
}