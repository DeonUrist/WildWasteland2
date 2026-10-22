using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Offline authoring only. No part of this editor class enters the WW DLL.
public static class WeatherAssetBuild
{
    const string Generated = "Assets/Weather/Generated";
    static bool writePreviews = true;

    public static void BuildBundle() { writePreviews = false; Build(); }

    [MenuItem("WW Weather/Build prototype assets")]
    public static void Build()
    {
        if (Application.unityVersion != "2020.3.49f1")
            throw new InvalidOperationException("Use Unity 2020.3.49f1 for this game.");
        Directory.CreateDirectory(Generated);
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Output"));
        Directory.CreateDirectory(output);
        Texture2D noise = Noise();
        Texture2D mask = DustMask(noise);
        SaveTexture(noise, "noise.png", true);
        SaveTexture(mask, "starter-dust.png", false);
        UnityEngine.Object.DestroyImmediate(noise);
        UnityEngine.Object.DestroyImmediate(mask);
        Shader shader = Shader.Find("WildWasteland/Weather/DustShell");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("DustShell shader has errors; read the editor log.");
        Material wall = MakeMaterial("wall", shader, new Color(0.56f, 0.37f, 0.19f, 0.88f), new Vector4(0.04f, -0.008f, 4, 1.8f));
        Material funnel = MakeMaterial("funnel", shader, new Color(0.4f, 0.27f, 0.14f, 0.94f), new Vector4(0.18f, -0.035f, 3, 5));
        wall.SetFloat("_SideFade", 0.15f);
        Mesh wallMesh = Wall();
        Mesh funnelMesh = Funnel();
        SaveAsset(wallMesh, "wall.asset");
        SaveAsset(funnelMesh, "funnel.asset");
        SaveAsset(Ring(), "ring.asset");
        wallMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Generated + "/wall.asset");
        funnelMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Generated + "/funnel.asset");
        AssetDatabase.SaveAssets();
        var assetNames = new List<string> { Generated + "/wall.mat", Generated + "/funnel.mat", Generated + "/wall.asset", Generated + "/funnel.asset" };
        assetNames.Add(Generated + "/ring.asset");
        // Preserve the depth-fade variant without depending on runtime shader discovery.
        var soft = new Material(wall); soft.EnableKeyword("_SOFT_DEPTH");
        SaveAsset(soft, "wall-soft.mat"); assetNames.Add(Generated + "/wall-soft.mat");
        if (File.Exists("Assets/Weather/Imported/cloud-stamps.png"))
        {
            string path = "Assets/Weather/Imported/dust-loop.png";
            if (!File.Exists(path) || !File.Exists("Assets/Weather/Imported/dust-flow.png")) throw new InvalidOperationException("Run bake-animated-dust.py and import its original atlases first.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true; importer.isReadable = false; importer.sRGBTexture = false; importer.maxTextureSize = 4096; importer.SaveAndReimport();
            Shader billowShader = Shader.Find("WildWasteland/Weather/Billows");
            Shader atmosphereShader = Shader.Find("Hidden/WildWasteland/Weather/Atmosphere");
            if (billowShader == null || atmosphereShader == null || ShaderUtil.ShaderHasError(billowShader) || ShaderUtil.ShaderHasError(atmosphereShader))
                throw new InvalidOperationException("Weather billow/atmosphere shader compilation failed.");
            var cloud = new Material(billowShader); cloud.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            cloud.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Generated + "/noise.png"));
            SaveAsset(cloud, "cloud.mat"); assetNames.Add(Generated + "/cloud.mat");
            Shader smokeShader = Shader.Find("WildWasteland/Weather/SmokeParticles");
            if (smokeShader == null || ShaderUtil.ShaderHasError(smokeShader)) throw new InvalidOperationException("Smoke particle shader compilation failed.");
            var smoke = new Material(smokeShader); smoke.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            smoke.SetTexture("_NoiseTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Generated+"/noise.png"));
            SaveAsset(smoke,"smoke-particles.mat"); assetNames.Add(Generated+"/smoke-particles.mat");
            var air = new Material(atmosphereShader); air.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Generated + "/noise.png"));
            SaveAsset(air, "atmosphere.mat"); assetNames.Add(Generated + "/atmosphere.mat");
            var near = new Material(cloud); near.SetFloat("_NearDust", 1);
            SaveAsset(near, "near-dust.mat"); assetNames.Add(Generated + "/near-dust.mat");
            string flowPath = "Assets/Weather/Imported/dust-flow.png";
            var flowImporter = (TextureImporter)AssetImporter.GetAtPath(flowPath);
            flowImporter.sRGBTexture = false; flowImporter.mipmapEnabled = true; flowImporter.wrapMode = TextureWrapMode.Clamp; flowImporter.SaveAndReimport();
            Shader vortexShader = Shader.Find("WildWasteland/Weather/VortexVolume");
            if (vortexShader == null || ShaderUtil.ShaderHasError(vortexShader)) throw new InvalidOperationException("Continuous vortex shader compilation failed.");
            var vortex = new Material(vortexShader); vortex.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(flowPath));
            var densityBytes = File.ReadAllBytes("Assets/Weather/Imported/vortex-noise-128.bytes");
            var densityTexture = new Texture3D(128,128,128,TextureFormat.R8,true) { name = "WW.Weather.BakedDensity", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            densityTexture.SetPixelData(densityBytes,0); densityTexture.Apply(true,true);
            SaveAsset(densityTexture,"vortex-density.asset");
            vortex.SetTexture("_DensityTex",AssetDatabase.LoadAssetAtPath<Texture3D>(Generated+"/vortex-density.asset"));
            Shader sandShader = Shader.Find("WildWasteland/Weather/HurricaneSand");
            if (sandShader == null || ShaderUtil.ShaderHasError(sandShader)) throw new InvalidOperationException("Layered hurricane sand shader compilation failed.");
            var sandBytes=File.ReadAllBytes("Assets/Weather/Imported/hurricane-billows-128.bytes");
            var sandDensity=new Texture3D(128,128,128,TextureFormat.RGBA32,true) { name="WW.Weather.RoundedBillows",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear };
            sandDensity.SetPixelData(sandBytes,0); sandDensity.Apply(true,true); SaveAsset(sandDensity,"hurricane-density.asset");
            var sand = new Material(sandShader); sand.SetTexture("_DensityTex",AssetDatabase.LoadAssetAtPath<Texture3D>(Generated+"/hurricane-density.asset"));
            SaveAsset(sand,"hurricane-sand.mat"); assetNames.Add(Generated+"/hurricane-sand.mat");
            vortex.SetColor("_Tint", new Color(.55f,.43f,.33f,.88f));
            SaveAsset(vortex, "vortex.mat"); assetNames.Add(Generated + "/vortex.mat");
            Shader lightningShader = Shader.Find("WildWasteland/Weather/Lightning");
            if (lightningShader == null || ShaderUtil.ShaderHasError(lightningShader)) throw new InvalidOperationException("Lightning shader compilation failed.");
            SaveAsset(new Material(lightningShader), "lightning.mat"); assetNames.Add(Generated + "/lightning.mat");
            for (int tier = 0; tier < 3; tier++)
            {
                string suffix = tier == 0 ? "-low" : tier == 2 ? "-max" : "";
                SaveAsset(VortexBounds(), "vortex" + suffix + ".asset");
                assetNames.Add(Generated + "/vortex" + suffix + ".asset");
                SaveAsset(CloudVolume(tier == 0 ? 8 : tier == 1 ? 12 : 16,2,1,3),"canopy"+suffix+".asset");
                assetNames.Add(Generated+"/canopy"+suffix+".asset");
                SaveAsset(Lightning(tier), "lightning" + suffix + ".asset"); assetNames.Add(Generated + "/lightning" + suffix + ".asset");
            }
            foreach (int count in new[] { 8, 16, 24 })
            {
                string name = "cloud-ring-" + count + ".asset";
                SaveAsset(CloudVolume(count == 8 ? 12 : count == 16 ? 18 : 24, 2, count == 8 ? 3 : count == 16 ? 5 : 6, 0), name);
                assetNames.Add(Generated + "/" + name);
                string nearName = "near-dust-" + count + ".asset";
                var nearMesh = CloudVolume(count*48,1,1,1);
                SaveAsset(nearMesh, nearName); assetNames.Add(Generated + "/" + nearName);
            }
        }
        AssetDatabase.SaveAssets();
        if (writePreviews) Preview(output);
        var bundle = new AssetBundleBuild
        {
            assetBundleName = "wwweather-prototype.bundle",
            assetNames = assetNames.ToArray()
        };
        var result = BuildPipeline.BuildAssetBundles(output, new[] { bundle },
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
        if (result == null) throw new InvalidOperationException("AssetBundle build failed.");
        File.WriteAllText(Path.Combine(output, "STATUS.txt"),
            "PROTOTYPE AUTHORING ASSETS ONLY\r\nUnity " + Application.unityVersion +
            "\r\nOriginal 32-frame billows, 16-frame flow, baked-density vortices, canopy/grit and bounded atmosphere." +
            "\r\nAsset previews do not include native Azure/post-processing or game camera mods." +
            "\r\nNo in-game visual or performance verification has been performed.\r\n");
        Debug.Log("WW WEATHER ASSET BUILD OK: " + output);
    }

    static float Periodic(float u, float v, float scale)
    {
        float x = u * scale, y = v * scale;
        float a = Mathf.Lerp(Mathf.PerlinNoise(x, y), Mathf.PerlinNoise(x - scale, y), u);
        float b = Mathf.Lerp(Mathf.PerlinNoise(x, y - scale), Mathf.PerlinNoise(x - scale, y - scale), u);
        return Mathf.Lerp(a, b, v);
    }

    static Texture2D Noise()
    {
        var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false, true);
        var pixels = new Color32[256 * 256];
        for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
        {
            float u = x / 256f, v = y / 256f;
            float n = Periodic(u, v, 4) * 0.6f + Periodic(u, v, 11) * 0.27f + Periodic(u, v, 27) * 0.13f;
            byte value = (byte)(Mathf.Clamp01(n) * 255);
            pixels[y * 256 + x] = new Color32(value, value, value, 255);
        }
        texture.SetPixels32(pixels); texture.Apply();
        return texture;
    }

    static Texture2D DustMask(Texture2D noise)
    {
        var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        var pixels = new Color32[256 * 256];
        for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
        {
            float u = x / 255f, v = y / 255f;
            float edge = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Min(v, 1 - v) * 7));
            float n = noise.GetPixelBilinear(u, v).r;
            byte alpha = (byte)(Mathf.Clamp01((0.35f + n) * edge) * 255);
            pixels[y * 256 + x] = new Color32(255, 255, 255, alpha);
        }
        texture.SetPixels32(pixels); texture.Apply();
        return texture;
    }

    static void SaveTexture(Texture2D texture, string name, bool repeat)
    {
        string path = Generated + "/" + name;
        File.WriteAllBytes(path, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.sRGBTexture = !repeat;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = !repeat;
        importer.mipmapEnabled = true;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    static Material MakeMaterial(string name, Shader shader, Color color, Vector4 flow)
    {
        var material = new Material(shader);
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Generated + "/starter-dust.png"));
        material.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Generated + "/noise.png"));
        material.SetColor("_Tint", color); material.SetVector("_Flow", flow);
        SaveAsset(material, name + ".mat");
        return AssetDatabase.LoadAssetAtPath<Material>(Generated + "/" + name + ".mat");
    }

    static void SaveAsset(UnityEngine.Object asset, string name)
    {
        // Generated assets only. Existing .meta GUIDs are retained on rebuild.
        string path = Generated + "/" + name;
        var existing = AssetDatabase.LoadMainAssetAtPath(path);
        if (existing == null) AssetDatabase.CreateAsset(asset, path);
        else { EditorUtility.CopySerialized(asset, existing); EditorUtility.SetDirty(existing); }
    }

    static Mesh Surface(int columns, int rows, Func<float, float, Vector3> point, string name)
    {
        var vertices = new Vector3[(columns + 1) * (rows + 1)];
        var uv = new Vector2[vertices.Length];
        var triangles = new List<int>(columns * rows * 6);
        for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
        {
            int index = y * (columns + 1) + x;
            float u = x / (float)columns, v = y / (float)rows;
            vertices[index] = point(u, v); uv[index] = new Vector2(u, v);
            if (x == columns || y == rows) continue;
            int below = index + columns + 1;
            triangles.Add(index); triangles.Add(below); triangles.Add(index + 1);
            triangles.Add(index + 1); triangles.Add(below); triangles.Add(below + 1);
        }
        var mesh = new Mesh { name = name, vertices = vertices, uv = uv };
        mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh Wall()
    {
        return Surface(32, 8, (u, v) =>
        {
            float angle = (u - 0.5f) * 1.5f;
            float height = 120 * v * (0.9f + 0.1f * Mathf.Sin(u * 17));
            return new Vector3(Mathf.Sin(angle) * 300, height, (Mathf.Cos(angle) - 1) * 160);
        }, "WW.Weather.Wall");
    }

    static Mesh Funnel()
    {
        return Surface(48, 16, (u, v) =>
        {
            float angle = u * Mathf.PI * 2;
            float radius = 4 + 38 * Mathf.Pow(v, 1.7f);
            return new Vector3(Mathf.Cos(angle) * radius + Mathf.Sin(v * 3) * v * 18,
                v * 160, Mathf.Sin(angle) * radius);
        }, "WW.Weather.Funnel");
    }

    static Mesh Ring()
    {
        return Surface(64, 8, (u, v) =>
        {
            float angle = u * Mathf.PI * 2;
            float height = v * (0.88f + 0.12f * Mathf.Sin(u * Mathf.PI * 10));
            return new Vector3(Mathf.Cos(angle), height, Mathf.Sin(angle));
        }, "WW.Weather.Ring");
    }

    static Mesh CloudVolume(int sectors, int bands, int rows, int kind)
    {
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        var corners = new List<Vector2>(); var data = new List<Vector4>();
        var random = new System.Random(49653 + sectors + bands * 71 + rows * 19 + kind * 97);
        int card = 0;
        for (int depthIndex = 0; depthIndex < bands; depthIndex++) for (int rowIndex = 0; rowIndex < rows; rowIndex++) for (int sector = 0; sector < sectors; sector++)
        {
            int depth = bands-1-depthIndex, row = kind == 0 ? (rowIndex+1)%rows : rowIndex;
            float seed = (float)random.NextDouble();
            float across = (sector + .5f + (seed - .5f) * 1.1f) * 2 / sectors - 1;
            float front = -1+depth*.16f+(seed-.5f)*.16f;
            float relativeHeight = (row + .15f + (float)random.NextDouble()*.65f) / rows * .93f;
            var center = new Vector3(across, relativeHeight, front);
            float width = 240 + seed * 340 + row * 30;
            bool grit = kind == 1 && sector%4 != 0;
            bool gust = kind == 1 && sector%8 == 0;
            bool leading = kind == 0 && row == 0;
            if (kind == 1) { center = new Vector3(seed, (float)random.NextDouble(), (float)random.NextDouble()); width = grit ? .06f+seed*.16f : gust ? 6+seed*6 : 2+seed*3; relativeHeight = grit ? -1 : gust ? -3 : 0; }
            if (leading) { width = 360+seed*400; center.y = (width*.21f-20)/480; center.z = -1.18f-depth*.08f; relativeHeight = -2; }
            if (kind == 3) { center = new Vector3(across,.85f+seed*.15f,-.65f+depth*.95f); width = 1800+seed*800; relativeHeight = 1; }
            if (kind == 2)
            {
                float angle = sector * Mathf.PI * 2 / sectors + row * .23f;
                relativeHeight = (row + .5f) / rows;
                center = new Vector3(Mathf.Cos(angle), relativeHeight, Mathf.Sin(angle)); width = 120 + seed * 100;
            }
            int index = vertices.Count, tile = card++ % 8;
            for (int corner = 0; corner < 4; corner++)
            {
                float x = corner == 1 || corner == 2 ? 1 : 0, y = corner >= 2 ? 1 : 0;
                vertices.Add(center); corners.Add(new Vector2(x - 0.5f, y - 0.5f));
                data.Add(new Vector4(seed, width, width * (leading ? .42f : kind == 0 ? .9f+seed*.5f : kind == 3 ? .4f : grit ? 5 : gust ? .25f : 1), relativeHeight));
                uv.Add(new Vector2((tile % 4 + x) / 4f, (1 - tile / 4 + y) / 2f));
            }
            triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 1);
            triangles.Add(index); triangles.Add(index + 3); triangles.Add(index + 2);
        }
        var mesh = new Mesh { name = "WW.Weather.Billows." + card };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(1, corners); mesh.SetUVs(2, data);
        mesh.bounds = kind == 1 ? new Bounds(Vector3.zero, Vector3.one * 180) : new Bounds(new Vector3(0,0.5f,0), new Vector3(2.6f,1.8f,2.6f));
        return mesh;
    }

    static Mesh VortexBounds()
    {
        var mesh = new Mesh { name = "WW.Weather.BoundedVortex" };
        mesh.vertices = new[] { new Vector3(-1,0,-1), new Vector3(1,0,-1), new Vector3(1,1,-1), new Vector3(-1,1,-1),
            new Vector3(-1,0,1), new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(-1,1,1) };
        mesh.triangles = new[] { 0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,0,1,5,0,5,4,3,7,6,3,6,2 };
        mesh.RecalculateBounds(); return mesh;
    }

    static Mesh Lightning(int tier)
    {
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var directions = new List<Vector3>();
        var channels = new List<Vector2>(); var triangles = new List<int>();
        var random = new System.Random(375901);
        int segments = tier == 0 ? 12 : tier == 1 ? 16 : 20;
        for (int lane = 0; lane < (tier == 0 ? 1 : 2); lane++)
        {
            float x = lane == 0 ? -430 : 430;
            var main = new Vector3[segments + 1];
            for (int point = 0; point <= segments; point++)
            {
                float t = point / (float)segments;
                main[point] = new Vector3(x + Mathf.Sin(t*21+lane)*55 + ((float)random.NextDouble()-.5f)*55, (1-t)*950, -510+Mathf.Sin(t*17+lane)*65);
            }
            for (int branch = 0; branch <= tier; branch++)
            {
                Vector3 start = main[segments / 3 + branch], end = new Vector3(x + (lane == 0 ? -300 : 300), 280 + branch * 80, -440);
                for (int segment = 0; segment < segments; segment++)
                {
                    Vector3 a = branch == 0 ? main[segment] : Vector3.Lerp(start,end,segment/(float)segments);
                    Vector3 b = branch == 0 ? main[segment+1] : Vector3.Lerp(start,end,(segment+1)/(float)segments);
                    int index = vertices.Count;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        vertices.Add(corner < 2 ? a : b); directions.Add(b-a); channels.Add(new Vector2(lane,branch == 0 ? 0 : 1));
                        uv.Add(new Vector2(corner == 0 || corner == 3 ? -1 : 1, corner < 2 ? 0 : 1));
                    }
                    triangles.Add(index); triangles.Add(index+2); triangles.Add(index+1);
                    triangles.Add(index); triangles.Add(index+3); triangles.Add(index+2);
                }
            }
        }
        var mesh = new Mesh { name = "WW.Weather.Discharge." + tier };
        mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetUVs(1,directions); mesh.SetUVs(2,channels); mesh.SetTriangles(triangles,0);
        mesh.RecalculateBounds(); Bounds bounds = mesh.bounds; bounds.Expand(50); mesh.bounds = bounds;
        return mesh;
    }

    static GameObject Place(string name, Mesh mesh, Material material, Vector3 position)
    {
        var owner = new GameObject(name);
        owner.transform.position = position;
        owner.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = owner.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return owner;
    }

    static void Preview(string output)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cloud = AssetDatabase.LoadAssetAtPath<Material>(Generated + "/cloud.mat");
        var smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>(Generated+"/smoke-particles.mat");
        var banks = new GameObject[7];
        var canopies = new GameObject[7];
        for (int section = 0; section < banks.Length; section++)
        {
            banks[section] = PreviewSmoke(section,smokeMaterial,new Vector3(-6000+(section+.5f)*12000/7,0,0));
            canopies[section] = Place("Upper cloud bank " + section,AssetDatabase.LoadAssetAtPath<Mesh>(Generated+"/canopy.asset"),cloud,banks[section].transform.position);
            canopies[section].transform.localScale = new Vector3(6000f/7,1500,1500); canopies[section].SetActive(false);
        }
        var air = new Material(AssetDatabase.LoadAssetAtPath<Material>(Generated + "/atmosphere.mat"));
        Shader.SetGlobalFloat("_WWWeatherClock", 17);
        Shader.SetGlobalVector("_WWWeatherSun", new Vector4(-.7f,.4f,-.5f,0).normalized);
        Color previewAir = new Color(.43f,.34f,.27f), previewDust = new Color(.55f,.43f,.33f,.88f);
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) { previewAir = previewAir.linear; previewDust = previewDust.linear; }
        Shader.SetGlobalColor("_WWWeatherAir", previewAir);
        Shader.SetGlobalVector("_WWWeatherWind", new Vector4(0,0,-20,0));
        cloud.SetColor("_Tint", previewDust);
        smokeMaterial.SetColor("_Tint",previewDust);
        Shader.SetGlobalInt("_WWCount", 1); Shader.SetGlobalInt("_WWSamples", 4);
        Shader.SetGlobalVectorArray("_WWCenters", new[] { new Vector4(0,0,0,1), Vector4.zero, Vector4.zero, Vector4.zero });
        Shader.SetGlobalVectorArray("_WWSizes", new[] { new Vector4(6000,480,1500,0), Vector4.one, Vector4.one, Vector4.one });
        Shader.SetGlobalVectorArray("_WWAxes", new[] { new Vector4(1,0,0,1), Vector4.zero, Vector4.zero, Vector4.zero });
        Shader.SetGlobalVectorArray("_WWVelocity", new[] { new Vector4(0,0,-20,.004f), Vector4.zero, Vector4.zero, Vector4.zero });
        Shader.SetGlobalVectorArray("_WWBackLightA", new Vector4[4]); Shader.SetGlobalVectorArray("_WWBackLightB", new Vector4[4]);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Preview ground"; ground.transform.localScale = Vector3.one * 2000;
        var terrainMaterial = new Material(Shader.Find("Standard"));
        terrainMaterial.color = new Color(.52f,.38f,.21f);
        ground.GetComponent<Renderer>().sharedMaterial = terrainMaterial;
        var light = new GameObject("Preview sunlight").AddComponent<Light>(); light.type = LightType.Directional;
        light.transform.rotation = Quaternion.LookRotation(new Vector3(.7f,-.4f,.5f)); light.intensity = 1.2f;
        RenderSettings.ambientLight = new Color(.45f,.42f,.36f);
        var camera = new GameObject("Preview camera").AddComponent<Camera>();
        var columns = new GameObject[8];
        for (int column = 0; column < columns.Length; column++)
        {
            int code = column+1; float u = 0, weight = .5f;
            while (code > 0) { u += code%2*weight; code /= 2; weight *= .5f; }
            float z = (1-Mathf.Sqrt(1-.4375f*u))/.25f;
            code = column+1; float x = 0; weight = 1f/3;
            while (code > 0) { x += code%3*weight; code /= 3; weight /= 3; }
            var position = new Vector3((x*2-1)*(1-z*.25f)*6000*.78f,0,Mathf.Lerp(.08f,.65f,z)*1500);
            columns[column] = Place("Hurricane column " + column, AssetDatabase.LoadAssetAtPath<Mesh>(Generated + "/vortex.asset"), AssetDatabase.LoadAssetAtPath<Material>(Generated + "/vortex.mat"), position);
            columns[column].transform.localScale = new Vector3(850,1100,850); columns[column].SetActive(false);
        }
        var discharge = Place("Inter-column discharge", AssetDatabase.LoadAssetAtPath<Mesh>(Generated + "/lightning.asset"), AssetDatabase.LoadAssetAtPath<Material>(Generated + "/lightning.mat"), new Vector3(0,0,510));
        discharge.SetActive(false);
        var wisps = Place("GPU interior wisps", AssetDatabase.LoadAssetAtPath<Mesh>(Generated + "/near-dust-16.asset"), AssetDatabase.LoadAssetAtPath<Material>(Generated + "/near-dust.mat"), Vector3.zero);
        camera.backgroundColor = new Color(.37f,.49f,.61f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.nearClipPlane = .3f; camera.farClipPlane = 10000; camera.fieldOfView = 65;
        camera.depthTextureMode = DepthTextureMode.Depth;
        EditorSceneManager.SaveScene(scene, "Assets/Weather/Preview.unity");
        var target = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        var commands = new CommandBuffer { name = "Weather asset preview atmosphere" };
        var screen = new Mesh(); screen.vertices = new[] { new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0) };
        screen.uv = new[] { Vector2.zero,Vector2.right,Vector2.one,Vector2.up }; screen.triangles = new[] { 0,1,2,0,2,3 };
        commands.DrawMesh(screen,Matrix4x4.identity,air,0,0);
        camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, commands);
        try
        {
            camera.targetTexture = target;
            for (int view = 0; view < 7; view++)
            {
                bool hurricane = view >= 3;
                camera.transform.position = view == 6 ? columns[1].transform.position+new Vector3(500,15,-550) : new Vector3(0,8,view == 5 ? 0 : hurricane ? -3300 : view == 0 ? -2600 : view == 1 ? -1450 : 0);
                wisps.transform.position = camera.transform.position;
                camera.transform.LookAt(view == 6 ? columns[1].transform.position+Vector3.up*300 : new Vector3(0,hurricane ? 450 : view == 0 ? 150 : 8,700));
                Shader.SetGlobalFloat("_WWWeatherInside", view >= 5 ? .65f : hurricane || view == 0 ? 0 : view == 1 ? .2f : 1);
                var regionProperties = new MaterialPropertyBlock();
                regionProperties.SetVector("_RegionCenter",Vector4.zero); regionProperties.SetVector("_RegionAxisX",Vector3.right); regionProperties.SetVector("_RegionAxisZ",Vector3.forward);
                regionProperties.SetVector("_RegionSize",new Vector4(6000,1500,0,0));
                regionProperties.SetFloat("_Critical",hurricane ? 1 : 0);
                for (int section = 0; section < banks.Length; section++) banks[section].GetComponent<Renderer>().SetPropertyBlock(regionProperties);
                if (hurricane)
                {
                    var properties = regionProperties; properties.SetFloat("_Hurricane",0);
                    Vector3 a = columns[0].transform.position+new Vector3(0,500,400), b = columns[1].transform.position+new Vector3(0,600,400);
                    Vector4 backA = new Vector4(a.x,a.y,a.z,view == 4 ? 1.7f : .7f), backB = new Vector4(b.x,b.y,b.z,.7f);
                    properties.SetVector("_BackLightA",backA); properties.SetVector("_BackLightB",backB); properties.SetVector("_Pulse",new Vector4(view == 4 ? 1 : 0,0,0,0));
                    for (int section = 0; section < banks.Length; section++) banks[section].GetComponent<Renderer>().SetPropertyBlock(properties);
                    properties.SetFloat("_Canopy",1);
                    // The native particle banks already include cloud-ceiling particles.
                    properties.SetFloat("_Canopy",0);
                    discharge.GetComponent<Renderer>().SetPropertyBlock(properties); discharge.SetActive(view == 4);
                    properties.SetFloat("_Vortex",1); properties.SetColor("_Tint",previewDust);
                    for (int column = 0; column < columns.Length; column++)
                    {
                        Vector3 back = columns[column].transform.position+new Vector3(0,600,350);
                        properties.SetVector("_BackLightA",new Vector4(back.x,back.y,back.z,view == 4 ? 1.9f : .9f)); properties.SetVector("_BackLightB",Vector4.zero);
                        columns[column].SetActive(true); columns[column].GetComponent<Renderer>().SetPropertyBlock(properties);
                    }
                    Shader.SetGlobalVectorArray("_WWBackLightA",new[] { backA, Vector4.zero, Vector4.zero, Vector4.zero });
                    Shader.SetGlobalVectorArray("_WWBackLightB",new[] { backB, Vector4.zero, Vector4.zero, Vector4.zero });
                    Shader.SetGlobalVectorArray("_WWSizes",new[] { new Vector4(6000,1500,1500,0), Vector4.one, Vector4.one, Vector4.one });
                }
                Shader.SetGlobalVector("_WWEye", camera.transform.position);
                Shader.SetGlobalMatrix("_WWInverseProjection", camera.projectionMatrix.inverse);
                Shader.SetGlobalMatrix("_WWViewToWorld", camera.worldToCameraMatrix.inverse);
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, view == 0 ? "billows-outside.png" : view == 1 ? "billows-boundary.png" : view == 2 ? "billows-interior.png" : view == 3 ? "hurricane-columns.png" : view == 4 ? "hurricane-lightning.png" : view == 5 ? "hurricane-interior.png" : "hurricane-ground-skirt.png"), image.EncodeToPNG());
            }
        }
        finally
        {
            camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, commands); commands.Release();
            UnityEngine.Object.DestroyImmediate(screen);
            camera.targetTexture = null; RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(air);
        }
    }

    static GameObject PreviewSmoke(int slot, Material material, Vector3 position)
    {
        var owner = new GameObject("Preview sorted smoke "+slot); owner.transform.position = position;
        var system = owner.AddComponent<ParticleSystem>(); system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.playOnAwake = false; main.loop = false; main.maxParticles = 320;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; main.startSpeed = 0; main.startSize3D = true;
        var emission = system.emission; emission.enabled = false; var shape = system.shape; shape.enabled = false;
        var renderer = owner.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
        renderer.sortMode = ParticleSystemSortMode.Distance; renderer.maxParticleSize = 1;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        var particles = new List<ParticleSystem.Particle>(); var random = new System.Random(7013+slot*397);
        for (int band = 0; band < 4; band++) for (int row = 0; row < 4; row++) for (int x = 0; x < 9; x++)
        {
            float seed = (float)random.NextDouble(), u = (x+.5f+(seed-.5f)*.7f)/9;
            float size = 330+seed*170, y = -35+row/3f*440+(float)random.NextDouble()*40;
            if (row == 0) { size *= .75f; y = size*.22f-25; }
            particles.Add(PreviewParticle(new Vector3((u-.5f)*12000/7,y,-1500+band*90+(seed-.5f)*60),size,row == 0 ? .65f : 1,seed,row == 0 ? .5f : 0,0));
        }
        for (int i = 0; i < 20; i++)
        {
            float seed = (float)random.NextDouble();
            particles.Add(PreviewParticle(new Vector3(((i+.5f)/20-.5f)*12000/7,950+seed*260,-225+(i%3)*180),1200+seed*700,.75f,seed,0,1));
        }
        system.Play(); system.Pause(); system.SetParticles(particles.ToArray(),particles.Count);
        Debug.Log("Smoke preview bank "+slot+": "+system.particleCount+" particles, bounds "+renderer.bounds);
        return owner;
    }
    static ParticleSystem.Particle PreviewParticle(Vector3 position, float size, float aspect, float seed, float leading, float canopy)
    {
        return new ParticleSystem.Particle { position = position, velocity = Vector3.zero,startSize3D = new Vector3(size,size*aspect,size),
            rotation = (seed-.5f)*50,startColor = new Color(seed,leading,canopy,1),startLifetime = 100000,remainingLifetime = 100000,randomSeed = (uint)(seed*1000000)+1 };
    }
}
