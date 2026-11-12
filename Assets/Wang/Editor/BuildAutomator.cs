using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System;

public static class BuildAutomator
{
    [MenuItem("Build/Window")]
    public static void Build()
    {
        int exitCode = 1;
        try
        {
            DateTime time = DateTime.Now;

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();

            buildPlayerOptions.scenes = new[] { "Assets/Scenes/OutGameScene.unity", "Assets/Scenes/MainScene.unity" };
            buildPlayerOptions.locationPathName = $"C:/NoMorePotion/{time.ToString("yyyy-MM-dd HH_mm_ss")}/NoMorePotion.exe";
            buildPlayerOptions.target = BuildTarget.StandaloneWindows;
            buildPlayerOptions.options = BuildOptions.Development;

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            if (report.summary.result == BuildResult.Succeeded)
            {
                exitCode = 0;
                Debug.Log($"Build succeeded: {buildPlayerOptions.locationPathName}");
            }
            else
            {
                Debug.LogError($"Build failed: {report.summary.result}");
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        // 젠킨스에는 빌드 결과를 종료 코드로 전달하고, 메뉴 실행 시에는 에디터를 유지한다.
        if (Application.isBatchMode == true)
            EditorApplication.Exit(exitCode);
    }
}
