#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// This optional package is already referenced as a compiled assembly. Its source
// triggers MenSharp 0.1.3 UnplacedLabel errors even for unrelated entry points.
[InitializeOnLoad]
public static class MenSharpForeignLibraryPatch
{
    static MenSharpForeignLibraryPatch() { EditorApplication.delayCall += Apply; }
    public static void Apply()
    {
        const string path = "Packages/io.tesca.mensharp/Editor/MenSharpCompiler.cs";
        if (!File.Exists(path)) return;
        string source = File.ReadAllText(path).Replace("\r\n", "\n");
        if (source.Contains("// TwentyOne: optional foundation library uses its compiled assembly.")) return;
        const string before = "foreach (string path in foreign)\n        {";
        const string after = "foreach (string path in foreign)\n        {\n            // TwentyOne: optional foundation library uses its compiled assembly.\n            if (path.Replace('\\\\', '/').Contains(\"idv.jlchntoz.vrcw-foundation/\")) continue;";
        if (!source.Contains(before)) { Debug.LogError("MenSharp library patch: compiler source changed."); return; }
        File.WriteAllText(path, source.Replace(before, after));
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }
}
#endif
