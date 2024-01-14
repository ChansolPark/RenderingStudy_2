using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomShaderGUI : ShaderGUI
{
    MaterialEditor editor;
    Object[] materials;
    MaterialProperty[] properties;

    bool showPresets;

    public override void OnGUI(
        MaterialEditor materialEditor, MaterialProperty[] properties
    )
    {
        base.OnGUI(materialEditor, properties);
        this.editor = materialEditor;
        this.materials = materialEditor.targets;
        this.properties = properties;

        EditorGUILayout.Space();
        this.showPresets = EditorGUILayout.Foldout(this.showPresets, "Presets", true);
        if (this.showPresets)
        {
            this.OpaquePreset();
            this.ClipPreset();
            this.FadePreset();
            this.TransparentPreset();
        }
    }
    bool HasPremultiplyAlpha => this.HasProperty("_PremulAlpha");

    bool HasProperty(string name) =>
    FindProperty(name, this.properties, false) != null;

    void SetProperty(string name, string keyword, bool value)
    {
        if (this.SetProperty(name, value ? 1f : 0f))
        {
            this.SetKeyword(keyword, value);
        }
    }

    bool SetProperty(string name, float value)
    {
        MaterialProperty property = FindProperty(name, this.properties, false);
        if (property != null)
        {
            property.floatValue = value;
            return true;
        }
        return false;
    }

    void SetKeyword(string keyword, bool enabled)
    {
        if (enabled)
        {
            foreach (Material m in this.materials)
            {
                m.EnableKeyword(keyword);
            }
        }
        else
        {
            foreach (Material m in this.materials)
            {
                m.DisableKeyword(keyword);
            }
        }
    }

    bool Clipping
    {
        set => this.SetProperty("_Clipping", "_CLIPPING", value);
    }

    bool PremultiplyAlpha
    {
        set => this.SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
    }

    BlendMode SrcBlend
    {
        set => this.SetProperty("_SrcBlend", (float)value);
    }

    BlendMode DstBlend
    {
        set => this.SetProperty("_DstBlend", (float)value);
    }

    bool ZWrite
    {
        set => this.SetProperty("_ZWrite", value ? 1f : 0f);
    }

    RenderQueue RenderQueue
    {
        set
        {
            foreach (Material m in this.materials)
            {
                m.renderQueue = (int)value;
            }
        }
    }

    void OpaquePreset()
    {
        if (this.PresetButton("Opaque"))
        {
            this.Clipping = false;
            this.PremultiplyAlpha = false;
            this.SrcBlend = BlendMode.One;
            this.DstBlend = BlendMode.Zero;
            this.ZWrite = true;
            this.RenderQueue = RenderQueue.Geometry;
        }
    }
    void ClipPreset()
    {
        if (this.PresetButton("Clip"))
        {
            this.Clipping = true;
            this.PremultiplyAlpha = false;
            this.SrcBlend = BlendMode.One;
            this.DstBlend = BlendMode.Zero;
            this.ZWrite = true;
            this.RenderQueue = RenderQueue.AlphaTest;
        }
    }
    void FadePreset()
    {
        if (this.PresetButton("Fade"))
        {
            this.Clipping = false;
            this.PremultiplyAlpha = false;
            this.SrcBlend = BlendMode.SrcAlpha;
            this.DstBlend = BlendMode.OneMinusSrcAlpha;
            this.ZWrite = false;
            this.RenderQueue = RenderQueue.Transparent;
        }
    }
    void TransparentPreset()
    {
        if (this.HasPremultiplyAlpha && this.PresetButton("Transparent"))
        {
            this.Clipping = false;
            this.PremultiplyAlpha = true;
            this.SrcBlend = BlendMode.One;
            this.DstBlend = BlendMode.OneMinusSrcAlpha;
            this.ZWrite = false;
            this.RenderQueue = RenderQueue.Transparent;
        }
    }


    bool PresetButton(string name)
    {
        if (GUILayout.Button(name))
        {
            this.editor.RegisterPropertyChangeUndo(name);
            return true;
        }
        return false;
    }
}