using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

public class CreateAssetBundles
{
    [MenuItem("Assets/Build AssetBundles")]
    public static void BuildAllAssetBundles()
    {
        string bundleDir = "Assets/AssetBundles";
        if( !Directory.Exists(bundleDir) )
        {
            Directory.CreateDirectory(bundleDir);
        }

        BuildPipeline.BuildAssetBundles(bundleDir, BuildAssetBundleOptions.UncompressedAssetBundle, BuildTarget.StandaloneWindows64);
    }
}
