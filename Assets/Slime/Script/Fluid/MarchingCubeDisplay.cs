using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class MarchingCubeDisplay : FluidDisplay{

    public ComputeShader calcDensityCompute;
    public ComputeShader marchingCompute;
    public Material mat;
    public float isoLevel = 0.0f;
    public Vector3 size = new Vector3(0.02f,0.01f,0.02f);
    public int blurRadius = 3;

    ComputeBuffer MeanPositionBuffer;
    ComputeBuffer CovarianceBuffer;
    ComputeBuffer triangleBuffer;
    ComputeBuffer triCountBuffer;

    RenderTexture densityTexture;
    RenderTexture blurTexture;

    Vector3Int resolution = new Vector3Int(2,2,2);
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;
    int triangleCapacity = 0;
    const float TRI_GROWTH = 1.2f;

    // Kernel IDs
    const int computeMeanPos = 0;
    const int computeCovariance = 1;
    const int getDensity = 2;
    const int blurDensity = 3;

    public override void Init() {
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        mesh = new Mesh();
        SetBuffer();
    }

    public override void Display() {
        DisPlayMarchingCube();
    }

    void SetBuffer() {
        MeanPositionBuffer = ComputeHelper.CreateStructuredBuffer<Vector3>(fluidMaster.number);
        CovarianceBuffer = ComputeHelper.CreateStructuredBuffer<float3x3>(fluidMaster.number);
        SetDensityBuffer();
        SetMarchingCubeBuffer();
    }

    void SetDensityBuffer() {
        ComputeHelper.SetBuffer(calcDensityCompute, MeanPositionBuffer, "MeanPositions", computeMeanPos, computeCovariance, getDensity);
        ComputeHelper.SetBuffer(calcDensityCompute, CovarianceBuffer, "Covariance", computeCovariance, getDensity);
        ComputeHelper.SetBuffer(calcDensityCompute, fluidMaster.predictedPositionsBuffer, "PredictedPositions", computeMeanPos, computeCovariance, getDensity);
        ComputeHelper.SetBuffer(calcDensityCompute, fluidMaster.spatialIndices, "SpatialIndices", computeMeanPos, computeCovariance, getDensity);
        ComputeHelper.SetBuffer(calcDensityCompute, fluidMaster.spatialOffsets, "SpatialOffsets", computeMeanPos, computeCovariance, getDensity);
        ComputeHelper.SetBuffer(calcDensityCompute, fluidMaster.densityBuffer, "Densities", getDensity);
        

        ComputeHelper.SetParameter(calcDensityCompute, "numParticles", fluidMaster.positionBuffer.count);
        CreateRenderTexture();

    }

    void CreateRenderTexture() {
        CreateTexture(ref densityTexture, resolution, RenderTextureFormat.RFloat);
        CreateTexture(ref blurTexture, resolution, RenderTextureFormat.RFloat);
        ComputeHelper.AssignTexture(calcDensityCompute, densityTexture, "DensityMap", getDensity, blurDensity);
        ComputeHelper.AssignTexture(calcDensityCompute, blurTexture, "BlurDensityMap", blurDensity);
    }

    void SetMarchingCubeBuffer() {
        int numVoxel = (int)(resolution.x - 1) * (int)(resolution.y - 1) * (int)(resolution.z - 1);
        triangleBuffer = new ComputeBuffer(numVoxel * 5, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        triangleBuffer.SetCounterValue(0);
        ComputeHelper.AssignTexture(marchingCompute, blurTexture, "density", 0);
        ComputeHelper.SetBuffer(marchingCompute, triangleBuffer, "triangles", 0);        
    }
    void DisPlayMarchingCube() {       
        CalcBoundary();
        UpdateParameter();
        DispatchDensity();
        DispatchMarchingCube();
        CalcMesh();
    }


    void UpdateParameter() {
        UpdateDensityPatameter();
        UpdateMarchingCubeParameter();
    }

    void UpdateDensityPatameter() {
        ComputeHelper.SetParameter(calcDensityCompute, "number", fluidMaster.number);
        ComputeHelper.SetParameter(calcDensityCompute, "smoothingRadius", fluidMaster.smoothingRadius);
        ComputeHelper.SetParameter(calcDensityCompute, "size", size);
        ComputeHelper.SetParameter(calcDensityCompute, "resolution", (Vector3)resolution);
        ComputeHelper.SetParameter(calcDensityCompute, "minPos", minPos);
        ComputeHelper.SetParameter(calcDensityCompute, "radius", blurRadius);
    }

    void UpdateMarchingCubeParameter() {
        ComputeHelper.SetParameter(marchingCompute, "isoLevel", isoLevel);
        ComputeHelper.SetParameter(marchingCompute, "size", size);
        ComputeHelper.SetParameter(marchingCompute, "minPos", minPos);
        ComputeHelper.SetParameter(marchingCompute, "numVoxelPerAxis", new Vector3(resolution.x - 1, resolution.y - 1, resolution.z - 1));

        ComputeHelper.AssignTexture(marchingCompute, blurTexture, "density", 0);
        ComputeHelper.SetBuffer(marchingCompute, triangleBuffer, "triangles", 0);
    }


    void CalcBoundary() {
        Vector3[] pos = new Vector3[fluidMaster.number];
        fluidMaster.positionBuffer.GetData(pos);
        Vector3 minRaw = pos[0];
        Vector3 maxRaw = pos[0];
        for (int i = 1; i < pos.Length; i++) {
            minRaw = Vector3.Min(minRaw, pos[i]);
            maxRaw = Vector3.Max(maxRaw, pos[i]);
        }

        minPos = FloorToGrid(minRaw, size) - size * 10;
        maxPos = CeilToGrid(maxRaw, size) + size * 10;

        Vector3 extent = maxPos - minPos;

        resolution = new Vector3Int(
            Mathf.CeilToInt(extent.x / size.x) + 1,
            Mathf.CeilToInt(extent.y / size.y) + 1,
            Mathf.CeilToInt(extent.z / size.z) + 1
        );
        EnsureResourcesForResolution(resolution);
    }

    void EnsureResourcesForResolution(Vector3Int res) {
        CreateRenderTexture();
        int numVoxel = (res.x - 1) * (res.y - 1) * (res.z - 1);
        int requiredTriangles = Mathf.CeilToInt(numVoxel * 5 * TRI_GROWTH);

        bool needRecreate =
            triangleBuffer == null || !triangleBuffer.IsValid() || triangleCapacity < requiredTriangles;

        if (needRecreate) {
            triangleBuffer?.Release();
            triCountBuffer?.Release();

            triangleCapacity = requiredTriangles;
            triangleBuffer = new ComputeBuffer(triangleCapacity, sizeof(float) * 3 * 3, ComputeBufferType.Append);
            triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        }
    }

    void DispatchDensity() {
        ComputeHelper.Dispatch(calcDensityCompute, fluidMaster.number , kernelIndex: computeMeanPos);
        ComputeHelper.Dispatch(calcDensityCompute, fluidMaster.number, kernelIndex: computeCovariance);        
        ComputeHelper.Dispatch(calcDensityCompute, resolution.x, resolution.y, resolution.z, getDensity);
        ComputeHelper.Dispatch(calcDensityCompute, resolution.x, resolution.y, resolution.z, blurDensity);
    }

    public void DebugDispatch() {
        ComputeHelper.Dispatch(calcDensityCompute, fluidMaster.number, kernelIndex: computeMeanPos);
        ComputeHelper.Dispatch(calcDensityCompute, fluidMaster.number, kernelIndex: computeCovariance);
    }

    void DispatchMarchingCube() {
        triangleBuffer.SetCounterValue(0);
        ComputeHelper.Dispatch(marchingCompute, resolution.x - 1, resolution.y - 1, resolution.z - 1);
    }

    void CalcMesh() {
        ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int numTris = triCountArray[0];
        Triangle[] tris = new Triangle[numTris];
        triangleBuffer.GetData(tris, 0, 0, numTris);
        UpdateMesh(tris, numTris);
    }

    public void UpdateMesh(Triangle[] tris, int numTris) {
        mesh.Clear(false);
        mesh.indexFormat = IndexFormat.UInt32;
        var vertices = new Vector3[numTris * 3];
        var meshTriangles = new int[numTris * 3];
        for (int i = 0; i < numTris; i++) {
            vertices[i * 3 + 0] = tris[i].pointA;
            vertices[i * 3 + 1] = tris[i].pointB;
            vertices[i * 3 + 2] = tris[i].pointC;
            for (int j = 0; j < 3; j++) {
                meshTriangles[i * 3 + j] = i * 3 + j;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = meshTriangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
        meshRenderer.material = mat;
    }


    public ComputeBuffer GetCovarianceBuffer() { 
        return CovarianceBuffer;
    }

    private void OnDestroy() {
        densityTexture.Release();
        blurTexture.Release();
        triangleBuffer.Release();
        triCountBuffer.Release();
    }



    public struct Triangle {
        public Vector3 pointA;
        public Vector3 pointB;
        public Vector3 pointC;
    }


    public Vector3 minPos;
    public Vector3 maxPos;

    // 可選：畫在世界座標 or local 座標
    public bool useLocalSpace = false;

    private void OnDrawGizmos() {

        Vector3 center = (minPos + maxPos) * 0.5f;
        Vector3 size = (maxPos - minPos);

        if (useLocalSpace) {
            // 若 min/max 是 local space：套用物件 transform
            Gizmos.matrix = transform.localToWorldMatrix;
        } else {
            Gizmos.matrix = Matrix4x4.identity;
        }

        Gizmos.DrawWireCube(center, size);

        // 想看 min/max 點也可以
        Gizmos.DrawSphere(minPos, 0.02f);
        Gizmos.DrawSphere(maxPos, 0.02f);

        Gizmos.matrix = Matrix4x4.identity;
    }


    static Vector3 FloorToGrid(Vector3 p, Vector3 cell) {
        return new Vector3(
            Mathf.Floor(p.x / cell.x) * cell.x,
            Mathf.Floor(p.y / cell.y) * cell.y,
            Mathf.Floor(p.z / cell.z) * cell.z
        );
    }

    static Vector3 CeilToGrid(Vector3 p, Vector3 cell) {
        return new Vector3(
            Mathf.Ceil(p.x / cell.x) * cell.x,
            Mathf.Ceil(p.y / cell.y) * cell.y,
            Mathf.Ceil(p.z / cell.z) * cell.z
        );
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Float3x3 {
        public Vector4 r0;
        public Vector4 r1;
        public Vector4 r2;
    }

}
