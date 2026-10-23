using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ZombieAssetBuild
{
    static readonly string[] Sources = { "zombie idle", "zombie walk", "Zombie_Run", "zombie crawl", "running crawl", "zombie attack", "zombie neck bite", "zombie biting", "zombie biting (2)", "Zombie Agonizing", "zombie scream", "Zombie Stand Up", "Zombie Stand Up (1)", "Zombie_Stand_Up" };
    static readonly string[] Names = { "Idle", "Walk", "Run", "Crawl", "CrawlRun", "Attack", "NeckBite", "Bite", "Bite2", "Agony", "Scream", "GetUp1", "GetUp2", "GetUp3" };

    public static void Build()
    {
        var assets = new List<string>();
        for (int i = 0; i < Sources.Length; i++)
        {
            string path = "Assets/Source/" + Sources[i] + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            var takes = importer.defaultClipAnimations;
            if (takes.Length != 1) throw new InvalidOperationException(path + ": expected one take, got " + takes.Length);
            var take = takes[0];
            bool loop = i < 5;
            importer.clipAnimations = new[] { new ModelImporterClipAnimation {
                name = Names[i], takeName = take.takeName, firstFrame = take.firstFrame, lastFrame = take.lastFrame,
                loopTime = loop, loopPose = loop,
                lockRootRotation = true, keepOriginalOrientation = true,
                lockRootHeightY = true, keepOriginalPositionY = true, heightFromFeet = false,
                lockRootPositionXZ = true, keepOriginalPositionXZ = true
            } };
            importer.SaveAndReimport();
            Avatar avatar = null; AnimationClip clip = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Avatar) avatar = (Avatar)asset;
                var candidate = asset as AnimationClip;
                if (candidate != null && candidate.name == Names[i]) clip = candidate;
            }
            if (avatar == null || !avatar.isValid || !avatar.isHuman || clip == null || !clip.humanMotion)
                throw new InvalidOperationException(path + ": invalid humanoid animation");
            string output = "Assets/Clips/" + Names[i] + ".anim";
            var copy = new AnimationClip(); EditorUtility.CopySerialized(clip, copy);
            AssetDatabase.DeleteAsset(output); AssetDatabase.CreateAsset(copy, output); assets.Add(output);
            Debug.Log("Zombie clip: " + Names[i] + "; humanoid=" + clip.humanMotion + "; seconds=" + clip.length + "; speed=" + clip.averageSpeed);
        }
        AssetDatabase.SaveAssets(); Directory.CreateDirectory("Build");
        var build = new AssetBundleBuild { assetBundleName = "zombie-anims-v3.bundle", assetNames = assets.ToArray() };
        if (BuildPipeline.BuildAssetBundles("Build", new[] { build }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64) == null)
            throw new InvalidOperationException("Zombie bundle build failed");
        Debug.Log("Zombie bundle complete: " + assets.Count + " humanoid clips; native ragdolls retain death.");
    }
}
