using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

public class DebugDisplay : FluidDisplay
{
    public Shader shader;
    public float scale;
    public int meshResolution;
    public int debug_MeshTriCount;
    
    Mesh mesh;
    Material mat;    
    ComputeBuffer argsBuffer;
    Bounds bounds;


    public MarchingCubeDisplay marchingCubeDisplay;

    public override void Init() {
        InitBuffer();
        InitMesh();        
    }

    void InitMesh() {
        mat = new Material(shader);
        mat.SetBuffer("Positions", fluidMaster.positionBuffer);
        mat.SetBuffer("Covariance", marchingCubeDisplay.GetCovarianceBuffer());
        mat.SetFloat("_Scale", scale);

        mesh = SebStuff.SphereGenerator.GenerateSphereMesh(meshResolution);
        debug_MeshTriCount = mesh.triangles.Length / 3;
        argsBuffer = ComputeHelper.CreateArgsBuffer(mesh, fluidMaster.number);
        bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
    }

    void InitBuffer() {

    }

    void UpdateParameter() {
        marchingCubeDisplay.calcDensityCompute.SetInt("number", fluidMaster.number);
        marchingCubeDisplay.calcDensityCompute.SetFloat("smoothingRadius", fluidMaster.smoothingRadius);
    }

    public override void Display() {
        UpdateParameter();
        marchingCubeDisplay.DebugDispatch();
        Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, bounds, argsBuffer);        
    }
}
