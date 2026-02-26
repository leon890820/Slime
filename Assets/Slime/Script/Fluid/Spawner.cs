using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;


public class Spawner : MonoBehaviour{
    public int particleNumberPerAxis = 35;
    public Vector3 centre;
    public float size;
    float3[] positions;
    float3[] velocities;
    public float3 initialVel;
    public bool showSpawnBounds;

    public int debug_numParticles;

    public PropertyData GetPropertyData() {
        int numParticle = particleNumberPerAxis * particleNumberPerAxis * particleNumberPerAxis;
        positions = new float3[numParticle];
        velocities = new float3[numParticle];
        float inverseParticleCount = 1f / (float)(particleNumberPerAxis - 1);
        int count = 0;

        for (int z = 0; z < particleNumberPerAxis; z++) {
            for (int y = 0; y < particleNumberPerAxis; y++) {
                for (int x = 0; x < particleNumberPerAxis; x++) {
                    float3 pos = new float3(x,y,z) * inverseParticleCount;
                    pos = (pos * 2f - 1f) * size * .5f + (float3)centre;
                    positions[count] = pos;
                    velocities[count] = initialVel;
                    count++;
                }
            }
        }

        return new PropertyData() { positions = positions, velosities = velocities };
    }



    public struct PropertyData {
        public float3[] positions;
        public float3[] velosities;    
    }

    void OnValidate() {
        debug_numParticles = particleNumberPerAxis * particleNumberPerAxis * particleNumberPerAxis;
    }

    void OnDrawGizmos() {
        if (showSpawnBounds && !Application.isPlaying) {
            Gizmos.color = new Color(1, 1, 0, 0.5f);
            Gizmos.DrawWireCube(centre, Vector3.one * size);
        }
    }

}
