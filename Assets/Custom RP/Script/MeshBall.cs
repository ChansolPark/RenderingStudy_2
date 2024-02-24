using UnityEngine;
using UnityEngine.Rendering;

public class MeshBall : MonoBehaviour
{
    private static int baseColorId = Shader.PropertyToID("_BaseColor"),
        metallicId = Shader.PropertyToID("_Metallic"),
        smoothnessId = Shader.PropertyToID("_Smoothness");

    [SerializeField]
    private Mesh mesh = default;

    [SerializeField]
    private Material material = default;
    private Matrix4x4[] matrices = new Matrix4x4[1023];
    private Vector4[] baseColors = new Vector4[1023];
    private float[] metallic = new float[1023];
    private float[] smoothness = new float[1023];

    [SerializeField]
    LightProbeProxyVolume lightProbeVolume = null;

    private MaterialPropertyBlock block;


    private void Awake()
    {
        for (int i = 0; i < this.matrices.Length; i++)
        {
            this.matrices[i] = Matrix4x4.TRS(
              Random.insideUnitSphere * 10f, Quaternion.identity, Vector3.one
             );

            this.baseColors[i] =
              new Vector4(Random.value, Random.value, Random.value, 1f);

            this.metallic[i] = Random.value < 0.25f ? 1f : 0f;
            this.smoothness[i] = Random.Range(0.05f, 0.95f);
        }
    }

    private void Update()
    {
        if (this.block == null)
        {
            this.block = new MaterialPropertyBlock();
            this.block.SetVectorArray(baseColorId, this.baseColors);
            this.block.SetFloatArray(metallicId, this.metallic);
            this.block.SetFloatArray(smoothnessId, this.smoothness);

            if (!this.lightProbeVolume)
            {
                var positions = new Vector3[1023];
                for (int i = 0; i < this.matrices.Length; i++)
                {
                    positions[i] = this.matrices[i].GetColumn(3);
                }
                var lightProbes = new SphericalHarmonicsL2[1023];
                LightProbes.CalculateInterpolatedLightAndOcclusionProbes(
                    positions, lightProbes, null
                );
                this.block.CopySHCoefficientArraysFrom(lightProbes);
            }
        }
        Graphics.DrawMeshInstanced(
            this.mesh, 0, this.material, this.matrices, 1023, this.block,
            ShadowCastingMode.On, true, 0, null,
            this.lightProbeVolume ?
                LightProbeUsage.UseProxyVolume : LightProbeUsage.CustomProvided,
            this.lightProbeVolume);
    }
}