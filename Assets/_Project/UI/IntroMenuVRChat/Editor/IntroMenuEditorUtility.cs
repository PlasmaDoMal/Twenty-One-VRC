using System;
using UnityEngine;
using VRC.Udon;

// MenSharp removes its authoring components before ClientSim starts.
// Editor checks must read the actual Udon program, identified by its source.
public static class IntroMenuEditorUtility
{
    public static UdonBehaviour FindProgram(string name)
    {
        foreach (var behaviour in UnityEngine.Object.FindObjectsOfType<UdonBehaviour>(true))
        {
            if (behaviour.programSource is MenSharpProgramAsset && behaviour.programSource.name == name)
                return behaviour;
        }
        return null;
    }

    public static T Read<T>(UdonBehaviour behaviour, string field)
    {
        if (behaviour == null) throw new InvalidOperationException("Missing MenSharp menu program.");
        return (T)behaviour.GetProgramVariable(field);
    }
}
