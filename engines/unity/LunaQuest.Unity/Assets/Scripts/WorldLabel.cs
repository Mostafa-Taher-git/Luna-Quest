using UnityEngine;

namespace LunaQuest.Unity
{
    /// <summary>A world-space name that follows the selected language.</summary>
    public sealed class WorldLabel : MonoBehaviour
    {
        public TextMesh Mesh;
        public string En = "";
        public string Ar = "";
        public bool Billboard;

        public void Init(TextMesh mesh, string en, string ar, bool billboard)
        {
            Mesh = mesh;
            En = en;
            Ar = ar;
            Billboard = billboard;
            Apply();
        }

        public void Apply()
        {
            if (Mesh == null) return;
            Mesh.text = L.T(En, Ar);
            Mesh.font = L.Font;
            var renderer = Mesh.GetComponent<MeshRenderer>();
            if (renderer != null && L.Font != null)
                renderer.sharedMaterial = L.Font.material;
        }

        private void LateUpdate()
        {
            if (!Billboard) return;
            var camera = Camera.main;
            if (camera != null)
                transform.rotation = camera.transform.rotation;
        }
    }
}
