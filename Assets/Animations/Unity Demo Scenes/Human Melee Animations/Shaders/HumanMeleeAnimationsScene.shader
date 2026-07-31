Shader "Custom/Kevin Iglesias/Human Melee Animations Scene"
{
    Properties
    {
        _Color ("Base Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}

        _LightDir ("Light Direction", Vector) = (0.5,1,0,0)
        _LightColor ("Light Color", Color) = (1,1,1,1)
        _Ambient ("Ambient", Range(0,1)) = 0.2

        _Metallic ("Metalness", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _SpecStrength ("Specular Strength", Range(0,2)) = 1
        
        _UVRotation ("UV Rotation", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            float4 _LightDir;
            fixed4 _LightColor;
            float _Ambient;
            float _Metallic;
            float _Smoothness;
            float _SpecStrength;
            
            float _UVRotation;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float2 uv = v.uv;

                // Rotate around real UV center
                float angle = radians(_UVRotation);
                float s = sin(angle);
                float c = cos(angle);

                float2 pivot = float2(0.5, 0.5);

                uv -= pivot;
                uv = float2(
                    uv.x * c - uv.y * s,
                    uv.x * s + uv.y * c
                );
                uv += pivot;

                // Apply texture tiling + offset AFTER rotation
                o.uv = uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * _Color;

                float3 N = normalize(i.normalWS);
                float3 L = normalize(_LightDir.xyz);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 H = normalize(L + V);

                float NdotL = saturate(dot(N, L));
                float NdotH = saturate(dot(N, H));

                float specPower = lerp(8.0, 256.0, _Smoothness);
                float spec = pow(NdotH, specPower) * _SpecStrength;

                float3 dielectricSpec = float3(0.04, 0.04, 0.04);
                float3 metalSpec = tex.rgb;

                float3 specColor = lerp(dielectricSpec, metalSpec, _Metallic);

                float3 diffuseColor = tex.rgb * (1.0 - _Metallic);

                float3 diffuse =
                    diffuseColor *
                    _LightColor.rgb *
                    (_Ambient + NdotL);

                float3 specular =
                    specColor *
                    _LightColor.rgb *
                    spec *
                    NdotL;

                float3 finalColor = diffuse + specular;

                return fixed4(finalColor, tex.a);
            }
            ENDCG
        }
    }
}