using UnityEngine;
using UnityEditor;
using System;

public static class BuildAutomator
{
    [MenuItem("Build/Window")]
    public static void Build()
    {
        DateTime time = DateTime.Now;

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();

        buildPlayerOptions.scenes = new[] { "Assets/JHJ/01 Scenes/OutGameScene.unity", "Assets/JHJ/01 Scenes/TestInGameScene.unity" };
        buildPlayerOptions.locationPathName = $"C:/NoMorePotion/{time.ToString("yyyy-MM-dd HH_mm_ss")}/NoMorePotion.exe";
        buildPlayerOptions.target = BuildTarget.StandaloneWindows;
        buildPlayerOptions.options = BuildOptions.Development;

        BuildPipeline.BuildPlayer(buildPlayerOptions);
    }
}