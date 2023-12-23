using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    partial void PrepareBuffer();
    partial void PrepareForSceneWindow();

    partial void DrawGizmos();

    partial void DrawUnsupportedShaders();

#if UNITY_EDITOR
    string SampleName { get; set; }

#else

	const string SampleName = bufferName;

#endif

#if UNITY_EDITOR

    static ShaderTagId[] legacyShaderTagIds = {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };

    static Material errorMaterial;

    partial void PrepareBuffer()
    {
        Profiler.BeginSample("Editor Only");
        this.buffer.name = this.SampleName = this.camera.name;
        Profiler.EndSample();
    }

    partial void PrepareForSceneWindow()
    {
        if (this.camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(this.camera);
        }
    }

    partial void DrawGizmos()
    {
        if (Handles.ShouldRenderGizmos())
        {
            this.context.DrawGizmos(this.camera, GizmoSubset.PreImageEffects);
            this.context.DrawGizmos(this.camera, GizmoSubset.PostImageEffects);
        }
    }

    partial void DrawUnsupportedShaders()
    {
        if (errorMaterial == null)
        {
            errorMaterial =
                new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        var drawingSettings = new DrawingSettings(
            legacyShaderTagIds[0], new SortingSettings(this.camera)
        )
        {
            overrideMaterial = errorMaterial
        };
        var filteringSettings = FilteringSettings.defaultValue;
        this.context.DrawRenderers(
            this.cullingResults, ref drawingSettings, ref filteringSettings
        );
    }
#endif
}