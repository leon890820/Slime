
Shader "Custom/3DTextureShader"
{
    
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Assets/script/Clouds/Shaders/CloudDebug.cginc"

            // vertex input: position, UV
            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            v2f vert (appdata v) {
                v2f output;
                output.pos = UnityObjectToClipPos(v.vertex);
                output.uv = v.uv;
                
                return output;
            }

            // Textures
            Texture3D<float> DensityTex;

            
            SamplerState samplerDensityTex;
            float depth;
            float maxNum;
           

          
            float4 frag (v2f i) : SV_Target{
                float2 uv = i.uv;
                float3 samplePos = float3(uv.x,uv.y, depth);

                float col = DensityTex.SampleLevel(samplerDensityTex, samplePos, 0);
                col /= maxNum;
                
                return float4(col,col,col,0);

            }

            ENDCG
        }
    }
}