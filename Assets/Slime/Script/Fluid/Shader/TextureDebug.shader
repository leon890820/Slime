Shader "Unlit/3DTextureSlice_URP"
{
    Properties
    {
        _Tex3D ("3D Texture", 3D) = "" {}
        _Slice ("Slice (0..1)", Range(0,1)) = 0
        _Channel ("Channel (0=RGBA, 1=R,2=G,3=B,4=A)", Float) = 0
        _FlipY ("Flip Y (0/1)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Unlit"
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE3D(_Tex3D);
            SAMPLER(sampler_Tex3D);

            CBUFFER_START(UnityPerMaterial)
                float _Slice;
                float _Channel;
                float _FlipY;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float4 ApplyChannel(float4 c, float mode)
            {
                // mode:
                // 0: RGBA
                // 1: R, 2: G, 3: B, 4: A (Εγ¥ά¦Η¶¥)
                if (mode < 0.5) return c;
                if (mode < 1.5) return float4(c.rrr, 1);
                if (mode < 2.5) return float4(c.ggg, 1);
                if (mode < 3.5) return float4(c.bbb, 1);
                return float4(c.aaa, 1);
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                if (_FlipY > 0.5) uv.y = 1 - uv.y;

                float3 uvw = float3(uv, saturate(_Slice));
                float4 c = SAMPLE_TEXTURE3D(_Tex3D, sampler_Tex3D, uvw);

                return ApplyChannel(-c/200, _Channel);
            }
            ENDHLSL
        }
    }
}