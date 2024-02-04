using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        cascadeCountId = Shader.PropertyToID("_CascadeCount"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades],
        cascadeData = new Vector4[maxCascades];

    static Matrix4x4[]
        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];

    static string[] cascadeBlendKeywords = {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    static string[] directionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };

    const int maxShadowedDirectionalLightCount = 4, maxCascades = 4;
    const string bufferName = "Shadows";

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
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

    public Vector3 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (this.ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None && light.shadowStrength > 0f &&
            this.cullingResults.GetShadowCasterBounds(visibleLightIndex, out _))
        {
            this.ShadowedDirectionalLights[this.ShadowedDirectionalLightCount] =
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex,
                    slopeScaleBias = light.shadowBias,
                    nearPlaneOffset = light.shadowNearPlane
                };

            return new Vector3(light.shadowStrength, this.settings.directional.cascadeCount * this.ShadowedDirectionalLightCount++, light.shadowNormalBias);
        }
        return Vector3.zero;
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

        float f = 1f - this.settings.directional.cascadeFade;
        this.buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1f / this.settings.maxDistance, 1f / this.settings.distanceFade, 1f / (1f - f * f)));
        this.buffer.BeginSample(bufferName);
        this.ExecuteBuffer();

        int tiles = this.ShadowedDirectionalLightCount * this.settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < this.ShadowedDirectionalLightCount; i++)
        {
            this.RenderDirectionalShadows(i, split, tileSize);
        }

        this.buffer.SetGlobalInt(cascadeCountId, this.settings.directional.cascadeCount);
        this.buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
        this.buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);

        this.buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);

        this.SetKeywords(directionalFilterKeywords, (int)this.settings.directional.filter - 1);
        this.SetKeywords(cascadeBlendKeywords, (int)this.settings.directional.cascadeBlend - 1);

        this.buffer.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
        this.buffer.EndSample(bufferName);
        this.ExecuteBuffer();
    }

    void SetKeywords(string[] keywords, int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                this.buffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                this.buffer.DisableShaderKeyword(keywords[i]);
            }
        }
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
        int cascadeCount = this.settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = this.settings.directional.CascadeRatios;

        float cullingFactor = Mathf.Max(0f, 0.8f - this.settings.directional.cascadeFade);

        for (int i = 0; i < cascadeCount; i++)
        {
            this.cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowSettings.splitData = splitData;

            if (index == 0)
            {
                this.SetCascadeData(i, splitData.cullingSphere, tileSize);
            }

            int tileIndex = tileOffset + i;
            dirShadowMatrices[tileIndex] = this.ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix,
                this.SetTileViewport(tileIndex, split, tileSize), split
            );
            this.buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            this.buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            this.ExecuteBuffer();
            this.context.DrawShadows(ref shadowSettings);
            this.buffer.SetGlobalDepthBias(0f, 0f);
        }
    }
    void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)this.settings.directional.filter + 1f);

        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;

        cascadeData[index] = new Vector4(
            1f / cullingSphere.w,
            filterSize * 1.4142136f
        );
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