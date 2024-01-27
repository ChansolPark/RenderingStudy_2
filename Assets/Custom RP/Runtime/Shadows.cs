using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");

    static Matrix4x4[]
        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount];

    const int maxShadowedDirectionalLightCount = 4;
    const string bufferName = "Shadows";

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
    }

    ShadowedDirectionalLight[] ShadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    CullingResults cullingResults;

    ShadowSettings settings;

    int ShadowedDirectionalLightCount;

    public Vector2 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (this.ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None && light.shadowStrength > 0f &&
            this.cullingResults.GetShadowCasterBounds(visibleLightIndex, out _))
        {
            this.ShadowedDirectionalLights[this.ShadowedDirectionalLightCount] =
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex
                };

            return new Vector2(light.shadowStrength, this.ShadowedDirectionalLightCount++);
        }
        return Vector2.zero;
    }

    public void Setup(
        ScriptableRenderContext context, CullingResults cullingResults,
        ShadowSettings settings
    )
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;
        this.ShadowedDirectionalLightCount = 0;
    }
    public void Render()
    {
        if (this.ShadowedDirectionalLightCount > 0)
        {
            this.RenderDirectionalShadows();
        }
        else
        {
            this.buffer.GetTemporaryRT(
                dirShadowAtlasId, 1, 1,
                32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
            );
        }
    }

    void RenderDirectionalShadows()
    {
        int atlasSize = (int)this.settings.directional.atlasSize;
        this.buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        this.buffer.SetRenderTarget(
            dirShadowAtlasId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        this.buffer.ClearRenderTarget(true, false, Color.clear);
        this.buffer.BeginSample(bufferName);
        this.ExecuteBuffer();

        int split = this.ShadowedDirectionalLightCount <= 1 ? 1 : 2;
        int tileSize = atlasSize / split;

        for (int i = 0; i < this.ShadowedDirectionalLightCount; i++)
        {
            this.RenderDirectionalShadows(i, split, tileSize);
        }

        this.buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        this.buffer.EndSample(bufferName);
        this.ExecuteBuffer();
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }

    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = this.ShadowedDirectionalLights[index];
        var shadowSettings =
            new ShadowDrawingSettings(this.cullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Orthographic);

        this.cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex, 0, 1, Vector3.zero, tileSize, 0f,
            out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
            out ShadowSplitData splitData);

        shadowSettings.splitData = splitData;

        dirShadowMatrices[index] = this.ConvertToAtlasMatrix(
            projectionMatrix * viewMatrix,
            this.SetTileViewport(index, split, tileSize), split
        );

        this.buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        this.ExecuteBuffer();
        this.context.DrawShadows(ref shadowSettings);
    }

    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        this.buffer.SetViewport(new Rect(
            offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
        ));
        return offset;
    }

    void ExecuteBuffer()
    {
        this.context.ExecuteCommandBuffer(this.buffer);
        this.buffer.Clear();
    }

    public void Cleanup()
    {
        this.buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        this.ExecuteBuffer();
    }
}