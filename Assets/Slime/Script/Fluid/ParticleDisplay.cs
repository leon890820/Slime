using UnityEngine;


public class ParticleDisplay : FluidDisplay{
    public Shader shader;
    public float scale;
    
    Mesh mesh;
    Material mat;
    public int meshResolution;
    public int debug_MeshTriCount;
    ComputeBuffer argsBuffer;
    Bounds bounds;

    public override void Init() {
        mat = new Material(shader);
        mat.SetBuffer("Positions", fluidMaster.positionBuffer);
        mat.SetFloat("_Scale", scale);

        mesh = SebStuff.SphereGenerator.GenerateSphereMesh(meshResolution);
        debug_MeshTriCount = mesh.triangles.Length / 3;
        argsBuffer = ComputeHelper.CreateArgsBuffer(mesh, fluidMaster.positionBuffer.count);
        bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
    }

    public override void Display() {
        Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, bounds, argsBuffer);
    }

}


