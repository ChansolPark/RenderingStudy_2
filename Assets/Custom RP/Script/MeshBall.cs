using UnityEngine;

public class MeshBall : MonoBehaviour
{
    private static int baseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField]
    private Mesh mesh = default;

    [SerializeField]
    private Material material = default;
    private Matrix4x4[] matrices = new Matrix4x4[1023];
    private Vector4[] baseColors = new Vector4[1023];
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
        }
    }

    private void Update()
    {
        if (this.block == null)
        {
            this.block = new MaterialPropertyBlock();
            this.block.SetVectorArray(baseColorId, this.baseColors);
        }
        Graphics.DrawMeshInstanced(this.mesh, 0, this.material, this.matrices, 1023, this.block);
    }
}