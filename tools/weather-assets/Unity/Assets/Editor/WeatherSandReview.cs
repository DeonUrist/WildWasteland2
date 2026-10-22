using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using WildWasteland.Modules.Weather;
using WildWasteland.Core;

// Asset artwork review explicitly requested by Denis; uses a byte-identical copy of the runtime renderer.
// No native Azure/post-processing, gameplay, performance test or simulated game verification.
public static class WeatherSandReview
{
    public static async void Render()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Output/Reviews/swirl-base_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")));
        Directory.CreateDirectory(output);
        string root="Assets/Weather/Generated/";
        var material=AssetDatabase.LoadAssetAtPath<Material>(root+"hurricane-sand.mat");
        if (material == null || ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("Hurricane material/shader is unavailable.");
        var smokeMaterial=new Material(AssetDatabase.LoadAssetAtPath<Material>(root+"smoke-particles.mat"));
        var renderer=new WeatherVortexSmoke(material,smokeMaterial);
        var smoke=new WeatherSmoke(smokeMaterial);
        var camera=new GameObject("Sand artwork camera").AddComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.095f,.045f,.022f);
        camera.nearClipPlane=.3f; camera.farClipPlane=16000; camera.fieldOfView=60;
        camera.depthTextureMode=DepthTextureMode.Depth; camera.allowHDR=true;
        var ground=GameObject.CreatePrimitive(PrimitiveType.Plane); ground.transform.localScale=Vector3.one*2400;
        var groundMaterial=new Material(Shader.Find("Unlit/Color")); groundMaterial.color=new Color(.24f,.13f,.05f);
        ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;
        var sunlight=new GameObject("Review sunlight").AddComponent<Light>(); sunlight.type=LightType.Directional;
        sunlight.transform.rotation=Quaternion.LookRotation(new Vector3(-.5f,-.55f,-.6f)); sunlight.intensity=1;
        RenderSettings.ambientLight=new Color(.2f,.13f,.08f);
        Color tint=new Color(153/255f,133/255f,112/255f), air=new Color(128/255f,89/255f,54/255f), wall=new Color(184/255f,154/255f,125/255f);
        if (QualitySettings.activeColorSpace==ColorSpace.Linear) { tint=tint.linear; air=air.linear; wall=wall.linear; }
        Shader.SetGlobalColor("_WWWeatherAir",air); Shader.SetGlobalVector("_WWWeatherSun",new Vector3(.5f,.55f,.6f).normalized);
        Shader.SetGlobalFloat("_WWWeatherInside",.5f); Shader.DisableKeyword("WW_DIAGNOSTICS_ON");
        var positions=new[] {new Vector3(-5500,0,500),new Vector3(-510,0,716),new Vector3(3584,0,181),new Vector3(7678,0,754)};
        var properties=new MaterialPropertyBlock(); properties.SetColor("_Tint",tint); properties.SetFloat("_Life",1);
        properties.SetColor("_CloudTint",wall);
        var wallProperties=new MaterialPropertyBlock(); wallProperties.SetColor("_Tint",wall); wallProperties.SetFloat("_Life",1);
        wallProperties.SetFloat("_Perimeter",1); wallProperties.SetFloat("_Critical",1);
        wallProperties.SetVector("_RegionAxisX",Vector3.right); wallProperties.SetVector("_RegionAxisZ",Vector3.forward); wallProperties.SetVector("_RegionSize",new Vector4(10000,2000,0,0));
        var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGBHalf);
        var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
        var previous=RenderTexture.active; camera.targetTexture=target;
        try
        {
            for (int view=0;view<8;view++)
            {
                camera.transform.position=view==0 ? new Vector3(1000,180,-15500) : view==1 ? positions[0]+new Vector3(0,70,-2250) : view==2 ? positions[0]+new Vector3(0,70,0) : view==3 ? positions[1]+new Vector3(0,30,0) : view==5 ? positions[1]+new Vector3(0,120,-1800) : view==6 ? positions[0]+new Vector3(0,35,-1450) : new Vector3(0,30,-1100);
                camera.transform.LookAt(view==0 ? new Vector3(700,950,500) : view==4 ? new Vector3(0,180,300) : view==5 ? positions[1]+Vector3.up*1250 : view==6 ? positions[0]+Vector3.up*120 : positions[view==3 ? 1 : 0]+new Vector3(view>=2 ? 600 : 0,view>=2 ? 1050 : 800,500));
                if (view==7) { camera.transform.position=positions[0]+new Vector3(0,100,-4800); camera.transform.LookAt(positions[0]+Vector3.up*1000); }
                for (int frame=0;frame<(view==0 ? 12 : 1);frame++)
                {
                    await System.Threading.Tasks.Task.Delay(200); // Let the previous editor frame's DrawMesh submissions expire.
                    float clock=53+frame*.15f; Shader.SetGlobalFloat("_WWWeatherClock",clock);
                    Shader.SetGlobalVector("_WWEye",camera.transform.position); renderer.Begin(camera);
                    smoke.Begin(camera);
                    wallProperties.SetFloat("_LeadingFront",view==4 ? 1 : 0);
                    for (int bank=0;bank<8;bank++) smoke.Draw(bank,new Vector3(-7000+bank*2000,0,view==4 ? 400 : 3100),Quaternion.identity,2000,500,WeatherVisualDetail.Max,view==4,wallProperties);
                    for (int slot=0;slot<(view==4 ? 0 : positions.Length);slot++)
                    {
                        Vector3 back=positions[slot]+new Vector3(0,600,350);
                        properties.SetVector("_BackLightA",new Vector4(back.x,back.y,back.z,.9f));
                        renderer.Draw(slot,positions[slot],Quaternion.identity,WeatherVisualDetail.Max,properties);
                    }
                    camera.Render(); RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,1600,900),0,0); pixels.Apply();
                    string name=view==0 ? "outside-"+frame.ToString("D2") : view==1 ? "close-wide" : view==2 ? "eye-wide" : view==3 ? "eye-thin" : view==5 ? "thin-swirl" : view==6 ? "ground-dust" : view==7 ? "wide-swirl" : "dense-front";
                    File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());
                }
            }
            File.WriteAllText(Path.Combine(output,"README.txt"),"UNITY ASSET RENDERS: exact runtime renderer/shader, Max detail, fixed artwork lighting. Native game atmosphere/post-processing absent. Not game screenshots or visual/performance verification. Outside frames are 0.15 s apart.\r\n");
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Output/LATEST-SAND-REVIEW.txt")),output);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); return; }
        finally
        {
            renderer.Dispose(); smoke.Dispose(); UnityEngine.Object.DestroyImmediate(smokeMaterial); camera.targetTexture=null; RenderTexture.active=previous;
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(groundMaterial);
        }
        Debug.Log("WW SAND ARTWORK REVIEW: "+output);
        EditorApplication.Exit(0);
    }
}
