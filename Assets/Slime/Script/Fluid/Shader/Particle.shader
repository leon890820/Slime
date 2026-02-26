Shader "URP/Instanced/Particle3DLitIndirect"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        _ColourMap ("Colour Map", 2D) = "white" {}
        _Scale ("Scale", Float) = 1.0
        _VelocityMax ("Velocity Max", Float) = 10.0

        _Smoothness ("Smoothness", Range(0,1)) = 0.2
        _Specular ("Specular", Range(0,1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "DisableBatching"="True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex vert
            #pragma fragment frag

            // GPU instancing + procedural
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:Setup

            // URP lighting keywords (最常用這幾個就夠)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_ColourMap); SAMPLER(sampler_ColourMap);

            float4 _BaseColor;
            float _Scale;
            float _VelocityMax;
            float _Smoothness;
            float _Specular;

            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            StructuredBuffer<float3> Positions;
            StructuredBuffer<float3> Velocities;
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half4  color       : COLOR0;

                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                float4 shadowCoord : TEXCOORD3;
                #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            void Setup() {} // 只為了啟用 procedural instancing 宏，不依賴矩陣覆寫

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.uv = IN.uv;

                // 顏色：速度對應 colour map
                half4 instCol = half4(1,1,1,1);
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                float speed = length(Velocities[unity_InstanceID]);
                float t = saturate(speed / max(_VelocityMax, 1e-6));
                instCol = SAMPLE_TEXTURE2D_LOD(_ColourMap, sampler_ColourMap, float2(t, 0.5), 0);
                #endif
                OUT.color = instCol;

                // 位置：用 Positions 當世界位移
                float3 posWS;
                float s = _Scale;

                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                float3 p = Positions[unity_InstanceID];
                posWS = p + (IN.positionOS.xyz * s);
                #else
                posWS = TransformObjectToWorld(IN.positionOS.xyz);
                #endif

                OUT.positionWS = posWS;
                OUT.positionHCS = TransformWorldToHClip(posWS);

                // 法線：因為我們只做 uniform scale + translation，所以 normal 直接把 OS normal 轉世界即可
                // 但我們沒有用到 objectToWorld 矩陣，這裡用「假設 mesh 本身在世界是 identity」的方式：
                // -> 如果你畫的 mesh 本身就沒旋轉縮放（一般粒子 mesh 都是），這是 OK 的。
                // -> 若你想支援 mesh 本體 transform 旋轉縮放，跟我說我再給你完整版本（會多一點矩陣處理）
                float3 nWS = normalize(IN.normalOS);
                OUT.normalWS = nWS;

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                OUT.shadowCoord = TransformWorldToShadowCoord(posWS);
                #endif

                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                half3 albedo = baseTex.rgb * IN.color.rgb;

                // 主光
                Light mainLight = GetMainLight(
                    #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                        IN.shadowCoord
                    #else
                        0
                    #endif
                );

                half3 N = normalize(IN.normalWS);
                half3 L = -normalize(mainLight.direction); // URP: direction 是從點指向光源的反方向(依版本)，這裡用 normalize 後取 dot 即可
                half ndotl = saturate(dot(N, -L));        // 用 -L 比較保險：讓正向面變亮

                // Lambert diffuse
                half3 diffuse = albedo * (mainLight.color * ndotl);

                // 簡單 specular（Blinn-Phong）
                half3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half3 H = normalize((-L) + V);
                half ndoth = saturate(dot(N, H));
                half specPow = lerp(8.0h, 128.0h, (half)_Smoothness);
                half spec = pow(ndoth, specPow) * (half)_Specular;

                half3 color = (diffuse + spec) * mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                return half4(color, 1);
            }

            ENDHLSL
        }
    }
}