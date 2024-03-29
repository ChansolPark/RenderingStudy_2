using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomShaderGUI : ShaderGUI
{
    enum ShadowMode
    {
        On, Clip, Dither, Off
    }

    ShadowMode Shadows
    {
        set
        {
            if (this.SetProperty("_Shadows", (float)value))
            {
                this.SetKeyword("_SHADOWS_CLIP", value == ShadowMode.Clip);
                this.SetKeyword("_SHADOWS_DITHER", value == ShadowMode.Dither);
            }
        }
    }

    MaterialEditor editor;
    Object[] materials;
    MaterialProperty[] properties;

    bool showPresets;

    public override void OnGUI(
        MaterialEditor materialEditor, MaterialProperty[] properties
    )
    {
        EditorGUI.BeginChangeCheck();

        base.OnGUI(materialEditor, properties);
        this.editor = materialEditor;
        this.materials = materialEditor.targets;
        this.properties = properties;

        this.BakedEmission();

        EditorGUILayout.Space();
        this.showPresets = EditorGUILayout.Foldout(this.showPresets, "Presets", true);
        if (this.showPresets)
        {
            this.OpaquePreset();
            this.ClipPreset();
            this.FadePreset();
            this.TransparentPreset();
        }

        if (EditorGUI.EndChangeCheck())
        {
            this.SetShadowCasterPass();
            this.CopyLightMappingProperties();
        }
    }

    void CopyLightMappingProperties()
    {
        MaterialProperty mainTex = FindProperty("_MainTex", this.properties, false);
        MaterialProperty baseMap = FindProperty("_BaseMap", this.properties, false);
        if (mainTex != null && baseMap != null)
        {
            mainTex.textureValue = baseMap.textureValue;
            mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
        }
        MaterialProperty color = FindProperty("_Color", this.properties, false);
        MaterialProperty baseColor =
            FindProperty("_BaseColor", this.properties, false);
        if (color != null && baseColor != null)
        {
            color.colorValue = baseColor.colorValue;
        }
    }

    void BakedEmission()
    {
        EditorGUI.BeginChangeCheck();
        this.editor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Material m in this.editor.targets)
            {
                m.globalIlluminationFlags &=
                    ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
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

    void SetShadowCasterPass()
    {
        MaterialProperty shadows = FindProperty("_Shadows", this.properties, false);
        if (shadows == null || shadows.hasMixedValue)
        {
            return;
        }

        bool enabled = shadows.floatValue < (float)ShadowMode.Off;
        foreach (Material m in this.materials)
        {
            m.SetShaderPassEnabled("ShadowCaster", enabled);
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