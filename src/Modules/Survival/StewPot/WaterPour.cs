using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.Survival
{
    // A flattened, closed water sheet: both faces have thickness and reflect scene lighting.
    // Its mesh and buffers are reused; it lives on the runner, outside the native save hierarchy.
    internal sealed class WaterPour
    {
        const int Rows = 25, Sides = 12;
        static Material material, meatMaterial, otherStewMaterial;
        readonly GameObject go;
        readonly Mesh mesh;
        readonly MeshRenderer renderer;
        readonly Vector3[] vertices = new Vector3[Rows * Sides];
        readonly Vector3[] normals = new Vector3[Rows * Sides];
        readonly Vector2[] uv = new Vector2[Rows * Sides];
        readonly float phase;

        internal WaterPour(Transform parent)
        {
            if (material == null) CreateMaterial();
            go = new GameObject("StewPot.Stream"); go.transform.SetParent(parent, false);
            mesh = new Mesh { name = "StewPot.WaterSheet" }; mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            phase = (go.GetInstanceID() & 255) * .173f;
            var triangles = new int[(Rows - 1) * Sides * 6];
            int index = 0;
            for (int row = 0; row < Rows - 1; row++)
                for (int side = 0; side < Sides; side++)
                {
                    int a = row * Sides + side, b = row * Sides + (side + 1) % Sides;
                    int c = a + Sides, d = b + Sides;
                    triangles[index++] = a; triangles[index++] = b; triangles[index++] = c;
                    triangles[index++] = b; triangles[index++] = d; triangles[index++] = c;
                }
            mesh.vertices = vertices; mesh.triangles = triangles;
        }

        internal void Draw(Vector3 start, Vector3 across, float distance, float litres, float rate, int contents)
        {
            Material flow = contents == (int)Contents.Meat ? meatMaterial
                : contents > (int)Contents.Water ? otherStewMaterial : material;
            if (renderer.sharedMaterial != flow) renderer.sharedMaterial = flow;
            if (across.sqrMagnitude < .5f) across = Vector3.right;
            Vector3 depth = Vector3.Cross(Vector3.down, across).normalized;
            go.transform.position = start;
            float time = Time.time * 5f + phase;
            float width = Mathf.Lerp(.025f, .085f, Mathf.Sqrt(Mathf.Clamp01(litres / 20f))) * Mathf.Sqrt(rate / 2f);
            for (int row = 0; row < Rows; row++)
            {
                float t = row / (float)(Rows - 1);
                float fall = distance * t;
                float wave = time - fall * 13f;
                // Broad at the lip; gravity stretches it thinner and ripples travel down its edges.
                float ripple = 1f + .12f * Mathf.Sin(wave) + .07f * Mathf.Sin(wave * 2.7f + phase);
                float halfWidth = width * ripple / Mathf.Sqrt(1f + fall * 3f);
                float thickness = .004f * ripple / Mathf.Sqrt(1f + fall * 2f);
                var centre = Vector3.down * fall + depth * (Mathf.Sin(wave * .7f) * .004f * t)
                    + across * (Mathf.Sin(wave * .43f) * .006f * t);
                for (int side = 0; side < Sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / Sides;
                    float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
                    int i = row * Sides + side;
                    float crease = 1f + .16f * Mathf.Sin(wave * 1.8f + side * 1.3f) * t;
                    vertices[i] = centre + across * (x * halfWidth) + depth * (z * thickness * crease);
                    normals[i] = (across * (x / halfWidth) + depth * (z / thickness)).normalized;
                    uv[i] = new Vector2(x * .5f + .5f, fall * 2f - Time.time * 2.8f + phase);
                }
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv;
            mesh.bounds = new Bounds(Vector3.down * (distance * .5f), new Vector3(.5f, distance + .1f, .5f));
            renderer.enabled = true;
        }

        internal void SetVisible(bool value) { renderer.enabled = value; }

        internal void Dispose() { Object.Destroy(go); Object.Destroy(mesh); }

        static void CreateMaterial()
        {
            const int width = 64, height = 128;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "StewPot.FlowingWater", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1), v = y / (float)height;
                    float bands = Mathf.Sin(u * 57f + Mathf.Sin(v * Mathf.PI * 4f) * 1.7f)
                        * Mathf.Sin(v * Mathf.PI * 6f + u * 15f);
                    float glint = Mathf.Pow(Mathf.Max(0, Mathf.Sin(u * 83f + Mathf.Sin(v * Mathf.PI * 2f) * 2f)), 12f);
                    float alpha = .995f;
                    float gray = .36f + bands * .055f + glint * .28f;
                    pixels[y * width + x] = new Color(gray * .94f, gray * .97f, gray + .025f, alpha);
                }
            texture.SetPixels(pixels); texture.Apply(false, true); Object.DontDestroyOnLoad(texture);
            material = new Material(Shader.Find("Standard")) { name = "StewPot.GrayBlueWater", mainTexture = texture };
            material.SetFloat("_Mode", 3f); material.SetFloat("_Glossiness", .88f); material.SetFloat("_Metallic", .05f);
            material.SetInt("_SrcBlend", (int)BlendMode.One); material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0); material.SetOverrideTag("RenderType", "Transparent");
            material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON"); material.renderQueue = 3000;
            Object.DontDestroyOnLoad(material);
            // Stew uses an opaque shader pass, including its edges; texture alpha cannot make it translucent.
            for (int i = 0; i < pixels.Length; i++)
            {
                float shade = Mathf.Clamp01(pixels[i].r / .94f * 1.8f + .15f);
                pixels[i] = new Color(shade, shade, shade, 1f);
            }
            var stewTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "StewPot.FlowingStew", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear
            };
            stewTexture.SetPixels(pixels); stewTexture.Apply(false, true); Object.DontDestroyOnLoad(stewTexture);
            meatMaterial = OpaqueStew(stewTexture, "Meat", new Color(.78f, .12f, .075f, 1f));
            otherStewMaterial = OpaqueStew(stewTexture, "VegetableMushroomMixed", new Color(.9f, .36f, .055f, 1f));
        }

        static Material OpaqueStew(Texture2D texture, string name, Color color)
        {
            var stew = new Material(Shader.Find("Standard")) { name = "StewPot.Poured" + name, mainTexture = texture, color = color };
            stew.SetFloat("_Mode", 0f); stew.SetFloat("_Glossiness", .6f);
            stew.SetInt("_SrcBlend", (int)BlendMode.One); stew.SetInt("_DstBlend", (int)BlendMode.Zero);
            stew.SetInt("_ZWrite", 1); stew.SetOverrideTag("RenderType", "Opaque"); stew.renderQueue = 2000;
            Object.DontDestroyOnLoad(stew);
            return stew;
        }
    }
}
