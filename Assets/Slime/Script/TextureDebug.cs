using System.Linq.Expressions;
using Unity.Mathematics;
using UnityEngine;

[ExecuteAlways]
public class TextureDebug : MonoBehaviour {
    private const float EPSILON = 1e-6f;

    private void Start() {
        float3x3 A = new float3x3(new float3(2, 1, 1), new float3(1, 3, 1), new float3(1, 1, 1));
        EVD_Jacobi(A, out float3 lambda, out float3x3 V);
        float3x3 L = float3x3.zero;
        L.c0.x = lambda.x;
        L.c1.y = lambda.y;
        L.c2.z = lambda.z;

        float3x3 A1 = V * L * math.transpose(V);           
        float3x3 A2 = math.transpose(V) * L * V;
        Debug.Log(A1);
        Debug.Log(A2);
    }

    public static void EVD_Jacobi(float3x3 A, out float3 lambda, out float3x3 V) {
        float3x3 D = A;
        V = float3x3.identity;
        for (int sweep = 0; sweep < 10; ++sweep) {
            float maxOff = 0.0f;
            int p = 0, q = 1;

            for (int i = 0; i < 3; ++i)
                for (int k = i + 1; k < 3; ++k) {
                    float aik = math.abs(D[i][k]);
                    if (aik < maxOff)
                        continue;
                    maxOff = aik;
                    p = i;
                    q = k;
                }
            if (maxOff < EPSILON) break;


            float App = D[p][p];
            float Aqq = D[q][q];
            float Apq = D[p][q];
            float tau = (Aqq - App) / (2.0f * Apq);
            float t = math.sign(tau) / (math.abs(tau) + math.sqrt(1.0f + tau * tau));
            float c = 1.0f / math.sqrt(1.0f + t * t);
            float s = t * c;

            float3x3 J = float3x3.identity;
            J[p][p] = c;
            J[q][q] = c;
            J[p][q] = -s;
            J[q][p] = s;

            D = math.mul(math.transpose(J), math.mul(D, J));
            V = math.mul(V, J);
        }

        lambda = new float3(D[0][0], D[1][1], D[2][2]);
    }
}