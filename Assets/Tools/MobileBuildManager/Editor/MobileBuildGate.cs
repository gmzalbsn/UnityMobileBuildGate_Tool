using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class MobileBuildGate : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public const string PREF_AUTO_INCREMENT_ON_BUILD = "MBM_AutoIncrementOnBuild";

    public void OnPreprocessBuild(BuildReport report)
    {
        BuildTarget target = report.summary.platform;
        bool isAndroid = target == BuildTarget.Android;
        bool isiOS = target == BuildTarget.iOS;

        if (!isAndroid && !isiOS)
            return;

        BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
        string bundleId = PlayerSettings.GetApplicationIdentifier(group);
        if (string.IsNullOrWhiteSpace(bundleId))
        {
            throw new BuildFailedException(
                "BUILD BLOCKED:\n" +
                "Bundle Identifier is empty.\n\n" +
                "Where: Project Settings > Player > Other Settings > Identification\n" +
                "Example: com.company.product"
            );
        }

        if (EditorBuildSettings.scenes == null || EditorBuildSettings.scenes.Length == 0)
        {
            throw new BuildFailedException(
                "BUILD BLOCKED:\n" +
                "No scenes are added to Build Settings.\n\n" +
                "Where: File > Build Settings > Scenes In Build"
            );
        }

        if (isAndroid && !EditorUserBuildSettings.development)
        {
            if (!PlayerSettings.Android.useCustomKeystore)
            {
                throw new BuildFailedException(
                    "BUILD BLOCKED:\n" +
                    "Android Release build requires a custom keystore.\n\n" +
                    "Where: Project Settings > Player > Android > Publishing Settings"
                );
            }

            if (string.IsNullOrEmpty(PlayerSettings.Android.keystoreName))
            {
                throw new BuildFailedException(
                    "BUILD BLOCKED:\n" +
                    "Custom keystore is enabled but no keystore file is set.\n\n" +
                    "Where: Project Settings > Player > Android > Publishing Settings"
                );
            }
        }

        bool autoIncrement = EditorPrefs.GetBool(PREF_AUTO_INCREMENT_ON_BUILD, true);
        if (autoIncrement)
        {
            IncrementBuildNumbers(isAndroid, isiOS);
        }
        else
        {
            Debug.Log("[MobileBuildGate] Auto-increment on build is OFF.");
        }

        Debug.Log($"[MobileBuildGate] Validation passed. bundleVersion: {PlayerSettings.bundleVersion}");
    }

    private void IncrementBuildNumbers(bool isAndroid, bool isiOS)
    {
        if (isAndroid)
        {
            PlayerSettings.Android.bundleVersionCode++;
            Debug.Log($"[MobileBuildGate] Android versionCode -> {PlayerSettings.Android.bundleVersionCode}");
        }

        if (isiOS)
        {
            int build = 0;
            int.TryParse(PlayerSettings.iOS.buildNumber, out build);
            build++;
            PlayerSettings.iOS.buildNumber = build.ToString();
            Debug.Log($"[MobileBuildGate] iOS buildNumber -> {PlayerSettings.iOS.buildNumber}");
        }
    }
}