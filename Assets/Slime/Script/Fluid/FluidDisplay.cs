using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class FluidDisplay : MonoBehaviour{
    public FluidMaster fluidMaster;

    public abstract void Init();
    public abstract void Display();

    protected void CreateTexture(ref RenderTexture texture, Vector3 resolution, RenderTextureFormat format = RenderTextureFormat.ARGB32) {
        if (texture == null || !texture.IsCreated() || texture.width != resolution.x || texture.height != resolution.y || texture.volumeDepth != resolution.z) {
            if (texture != null) {
                texture.Release();
            }
            texture = new RenderTexture((int)resolution.x, (int)resolution.y, 0, format);
            texture.volumeDepth = (int)resolution.z;
            texture.enableRandomWrite = true;
            texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            texture.wrapMode = TextureWrapMode.Clamp;

            texture.Create();
        }
    }
}
