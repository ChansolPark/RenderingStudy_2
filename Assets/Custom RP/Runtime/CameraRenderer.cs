using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    ScriptableRenderContext context;

    Camera camera;

    const string bufferName = "Render Camera";

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    CullingResults cullingResults;

    public void Render(ScriptableRenderContext context, Camera camera)
    {
        this.context = context;
        this.camera = camera;

        this.PrepareBuffer();
        this.PrepareForSceneWindow();

        if (!this.Cull())
        {
            return;
        }

        this.Setup();
        this.DrawVisibleGeometry();
        this.DrawUnsupportedShaders();
        this.DrawGizmos();
        this.Submit();
    }

    bool Cull()
    {
        if (this.camera.TryGetCullingParameters(out var parameters))
        {
            this.cullingResults = this.context.Cull(ref parameters);
            return true;
        }
        return false;
    }

    void Setup()
    {
        this.context.SetupCameraProperties(this.camera);
        CameraClearFlags flags = this.camera.clearFlags;
        this.buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ?
                this.camera.backgroundColor.linear : Color.clear
        );
        this.buffer.BeginSample(this.SampleName);
        this.ExecuteBuffer();
    }

    void DrawVisibleGeometry()
    {
        var sortingSettings = new SortingSettings(this.camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(
            unlitShaderTagId, sortingSettings
        );
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        this.context.DrawRenderers(
            this.cullingResults, ref drawingSettings, ref filteringSettings
        );

        this.context.DrawSkybox(this.camera);

        // ������ ������Ʈ�� ���� ���ۿ� �������� �����Ƿ�, ��ī�� �ڽ��� ���� ��������� �ʱ� ���� ���Ŀ� �׸���.
        // ? ���� ������Ʈ�� ���� ���ۿ� ���� ���� �ʳ� ? 
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;

        this.context.DrawRenderers(
            this.cullingResults, ref drawingSettings, ref filteringSettings
        );
    }

    void Submit()
    {
        this.buffer.EndSample(this.SampleName);
        this.ExecuteBuffer();
        this.context.Submit();
    }

    void ExecuteBuffer()
    {
        this.context.ExecuteCommandBuffer(this.buffer);
        this.buffer.Clear();
    }
}