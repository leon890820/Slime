Shader "Hidden/FluidRayMarchingShader"
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


            float3 boundsMin;
            float3 boundsMax;
            float densityMultiplier;

            Texture3D<float> DensityTex;
            SamplerState linearClampSampler;;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewVector : TEXCOORD1;
            };

            float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir) {
                // Adapted from: http://jcgt.org/published/0007/03/04/
                float3 t0 = (boundsMin - rayOrigin) * invRaydir;
                float3 t1 = (boundsMax - rayOrigin) * invRaydir;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                
                float dstA = max(max(tmin.x, tmin.y), tmin.z);
                float dstB = min(tmax.x, min(tmax.y, tmax.z));

                // CASE 1: ray intersects box from outside (0 <= dstA <= dstB)
                // dstA is dst to nearest intersection, dstB dst to far intersection

                // CASE 2: ray intersects box from inside (dstA < 0 < dstB)
                // dstA is the dst to intersection behind the ray, dstB is dst to forward intersection

                // CASE 3: ray misses box (dstA > dstB)

                float dstToBox = max(0, dstA);
                float dstInsideBox = max(0, dstB - dstToBox);
                return float2(dstToBox, dstInsideBox);
            }


            void rayMarchingFluidDensity(float3 rayDir){
                
            
            }
            
            v2f vert (appdata v) {
                v2f output;
                output.pos = UnityObjectToClipPos(v.vertex);
                output.uv = v.uv;
                // Camera space matches OpenGL convention where cam forward is -z. In unity forward is positive z.
                // (https://docs.unity3d.com/ScriptReference/Camera-cameraToWorldMatrix.html)
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                output.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                return output;
            }

            sampler2D _MainTex;

            float4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);

                float3 rayPos = _WorldSpaceCameraPos;
                float viewLength = length(i.viewVector);
                float3 rayDir = i.viewVector / viewLength;

                float2 boxInfo = rayBoxDst(boundsMin, boundsMax, rayPos , 1/rayDir);
                
                float stepSize = 0.01;
                float3 startPos = rayPos + rayDir * boxInfo.x;

                float totalDensity = 0.0;
                float3 boundSize = boundsMax - boundsMin;

                for(float dst = 0; dst < boxInfo.y - 0.02; dst += stepSize){
                    float3 samplepos = startPos + rayDir * dst;
                    float d = (DensityTex.SampleLevel(linearClampSampler, (samplepos + boundSize * 0.5) / boundSize , 0) - 150.0) * densityMultiplier * stepSize;
                    totalDensity += d;
                }


                if(boxInfo.y > 0) return float4(totalDensity,totalDensity,totalDensity,1.0);
                
                return float4(0.0,0.0,0.0,1.0);
            }
            ENDCG
        }
    }
}
