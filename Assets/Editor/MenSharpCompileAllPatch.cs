#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Keep the MenSharp 0.1.3 workaround reproducible: VPM packages are ignored by Git.
[InitializeOnLoad]
public static class MenSharpCompileAllPatch
{
    [Serializable] private class Edit { public string old; public string @new; }
    [Serializable] private class Patch { public Edit[] edits; }
    static MenSharpCompileAllPatch() { EditorApplication.delayCall += Apply; }

    [MenuItem("Tools/TwentyOne/Repair MenSharp Compile All")]
    public static void Apply()
    {
        const string compiler = "Packages/io.tesca.mensharp/Editor/MenSharpCompiler.cs";
        const string patch = "Assets/Editor/MenSharpCompileAllPatch.json";
        const string manifest = "Packages/io.tesca.mensharp/package.json";
        if (!File.Exists(compiler) || !File.Exists(patch) || !File.Exists(manifest)) return;
        if (!File.ReadAllText(manifest).Contains("\"version\": \"0.1.3\"")) return;
        string text = File.ReadAllText(compiler).Replace("\r\n", "\n");
        if (text.Contains("// TwentyOne: recover the 0.1.3 emit-all failure")) return;
        var data = JsonUtility.FromJson<Patch>(File.ReadAllText(patch));
        foreach (var edit in data.edits)
        {
            if (!text.Contains(edit.old))
            {
                Debug.LogError("MenSharp CompileAll patch: package source changed; not modified.");
                return;
            }
            text = text.Replace(edit.old, edit.@new);
        }
        File.WriteAllText(compiler, text);
        AssetDatabase.ImportAsset(compiler, ImportAssetOptions.ForceUpdate);
        Debug.Log("MenSharp 0.1.3 CompileAll workaround installed.");
    }
}
#endif