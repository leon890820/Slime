using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using static Spawner;


[StructLayout(LayoutKind.Sequential)]
public struct ColliderData {
    public Matrix4x4 localToWorld;
    public Matrix4x4 worldToLocal;
}

public class FluidMaster : MonoBehaviour{

    public event System.Action SimulationStepCompleted;

    [Header("Simulation Parameter")]
    public float timeScale = 1;
    public bool fixedTimeStep;
    public int iterationsPerFrame;

    public Spawner spawner;
    public ComputeShader compute;

    [Header("Fluid Parameter")]
    public float gravity = -9.8f;
    public float collisionDamping = 0.99f;
    public float smoothingRadius = 0.2f;
    public float viscosityStrength = 0.001f;

    public float targetDensity = 530;
    public float pressureMultiplier = 288;
    public float nearPressureMultiplier = 2.25f;

    [Header("Slime Parameter")]
    public Transform container;
    public float concentration = 5f;

    public ComputeBuffer positionBuffer;
    public ComputeBuffer velocityBuffer;
    public ComputeBuffer predictedPositionsBuffer;
    public ComputeBuffer densityBuffer;
    public ComputeBuffer spatialIndices;
    public ComputeBuffer spatialOffsets;
    public ComputeBuffer colliderBuffer;

    private GPUSort gpuSort;

    // Kernel IDs
    const int externalForcesKernel = 0;
    const int spatialHashKernel = 1;
    const int densityKernel = 2;
    const int pressureKernel = 3;
    const int viscosityKernel = 4;
    const int updatePositionsKernel = 5;

    public int number;

    public void Initialized() {
        float deltaTime = 1 / 60f;
        Time.fixedDeltaTime = deltaTime;
        InitBuffer();
        SetBuffer();
        SetGPUBuffer();
    }

    void InitBuffer() {
        var propertyData = spawner.GetPropertyData();
        number = propertyData.positions.Length;
        CreateBuffer(number);
        SetInitialBufferData(propertyData);
        SetColliderBuffer();
    }

    void CreateBuffer(int number) {
        positionBuffer = ComputeHelper.CreateStructuredBuffer<float3>(number);
        predictedPositionsBuffer = ComputeHelper.CreateStructuredBuffer<float3>(number);
        velocityBuffer = ComputeHelper.CreateStructuredBuffer<float3>(number);
        densityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(number);
        spatialIndices = ComputeHelper.CreateStructuredBuffer<uint3>(number);
        spatialOffsets = ComputeHelper.CreateStructuredBuffer<uint>(number);
        colliderBuffer = ComputeHelper.CreateStructuredBuffer<ColliderData>(1);
    }

    void SetInitialBufferData(PropertyData spawnData) {
        float3[] allPoints = new float3[spawnData.positions.Length];
        System.Array.Copy(spawnData.positions, allPoints, spawnData.positions.Length);
        positionBuffer.SetData(allPoints);
        predictedPositionsBuffer.SetData(allPoints);
        velocityBuffer.SetData(spawnData.velosities);
    }

    void SetColliderBuffer() {
        ColliderData colliderData = new ColliderData() {
            localToWorld = container.localToWorldMatrix,
            worldToLocal = container.worldToLocalMatrix
        };
        colliderBuffer.SetData(new ColliderData[] { colliderData });
    }

    void SetBuffer() {
        ComputeHelper.SetBuffer(compute, positionBuffer, "Positions", externalForcesKernel, densityKernel, pressureKernel, updatePositionsKernel);
        ComputeHelper.SetBuffer(compute, predictedPositionsBuffer, "PredictedPositions", externalForcesKernel, densityKernel, pressureKernel, viscosityKernel, spatialHashKernel);
        ComputeHelper.SetBuffer(compute, velocityBuffer, "Velocities", externalForcesKernel, pressureKernel, viscosityKernel, updatePositionsKernel);
        ComputeHelper.SetBuffer(compute, densityBuffer, "Densities", densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compute, spatialIndices, "SpatialIndices", spatialHashKernel, densityKernel, viscosityKernel, pressureKernel);
        ComputeHelper.SetBuffer(compute, spatialOffsets, "SpatialOffsets", spatialHashKernel, densityKernel, viscosityKernel, pressureKernel);
        ComputeHelper.SetBuffer(compute, colliderBuffer, "Colliders", updatePositionsKernel);
        ComputeHelper.SetParameter(compute, "numParticles", positionBuffer.count);
    }

    public void UpdateParticleSimulated() {
        if (!fixedTimeStep && Time.frameCount > 10) {
            float timeStep = Mathf.Min(Time.deltaTime, 1f / 30f) / iterationsPerFrame * timeScale;
            UpdateSettings(timeStep);
            for (int i = 0; i < iterationsPerFrame; i++) {
                RunSimulationStep();
                SimulationStepCompleted?.Invoke();
            }
        }
    }

    void RunSimulationStep() {
        ComputeHelper.Dispatch(compute, positionBuffer.count,kernelIndex : externalForcesKernel);
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: spatialHashKernel);
        gpuSort.SortAndCalculateOffsets();
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: densityKernel);
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: pressureKernel);
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: viscosityKernel);
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: updatePositionsKernel);
    }

    void SetGPUBuffer() {
        gpuSort = new();
        gpuSort.SetBuffers(spatialIndices, spatialOffsets);
    }


    void UpdateSettings(float deltaTime) {
        ComputeHelper.SetParameter(compute, "deltaTime", deltaTime);
        ComputeHelper.SetParameter(compute, "gravity", gravity);
        ComputeHelper.SetParameter(compute, "collisionDamping", collisionDamping);
        ComputeHelper.SetParameter(compute, "smoothingRadius", smoothingRadius);
        ComputeHelper.SetParameter(compute, "targetDensity", targetDensity);
        ComputeHelper.SetParameter(compute, "nearPressureMultiplier", nearPressureMultiplier);
        ComputeHelper.SetParameter(compute, "pressureMultiplier", pressureMultiplier);
        ComputeHelper.SetParameter(compute, "viscosityStrength", viscosityStrength);

        ComputeHelper.SetParameter(compute, "localToWorld", container.localToWorldMatrix);
        ComputeHelper.SetParameter(compute, "worldToLocal", container.worldToLocalMatrix);

        ComputeHelper.SetParameter(compute, "center", container.position);
        ComputeHelper.SetParameter(compute, "concentration", concentration);

        SetColliderBuffer();
    }

    void OnDestroy() {
        ComputeHelper.Release(positionBuffer, predictedPositionsBuffer, velocityBuffer, densityBuffer, spatialIndices, spatialOffsets);
    }

}

