using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public class MobileBuildManagerWindow : EditorWindow
{
    private const string PREF_AUTO_INCREMENT_ON_BUILD = MobileBuildGate.PREF_AUTO_INCREMENT_ON_BUILD;

    private GUIContent _iconOk;
    private GUIContent _iconWarn;
    private GUIContent _iconError;

    private enum Status
    {
        Ok,
        Warning,
        Error
    }

    private class Row
    {
        public string field;
        public string value;
        public Status status;
        public string note;

        public Row(string field, string value, Status status, string note)
        {
            this.field = field;
            this.value = value;
            this.status = status;
            this.note = note;
        }
    }

    private readonly List<Row> _rows = new List<Row>();
    private Vector2 _scroll;

    [MenuItem("Tools/Mobile Build Manager")]
    public static void Open()
    {
        var window = GetWindow<MobileBuildManagerWindow>("Mobile Build Manager");
        window.minSize = new Vector2(820, 620);
        window.RefreshValidator();
        window.Show();
    }

    private void OnEnable()
    {
        _iconOk = EditorGUIUtility.IconContent("TestPassed");
        _iconWarn = EditorGUIUtility.IconContent("console.warnicon");
        _iconError = EditorGUIUtility.IconContent("console.erroricon");

        RefreshValidator();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Mobile Build Manager (v1.0)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Validator + read-only version/build info. Build blocking + auto-increment are handled by MobileBuildGate.",
            EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space(10);

        DrawTopBar();

        EditorGUILayout.Space(10);

        DrawAutoIncrementToggle();

        EditorGUILayout.Space(10);

        DrawVersionAndBuildInfo();

        EditorGUILayout.Space(10);

        DrawValidator();

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Refresh Validator", GUILayout.Height(28)))
        {
            RefreshValidator();
        }
    }

    private void DrawTopBar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open Player Settings", GUILayout.Height(26)))
            {
                SettingsService.OpenProjectSettings("Project/Player");
            }

            if (GUILayout.Button("Open Build Settings", GUILayout.Height(26)))
            {
                EditorWindow.GetWindow(Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Switch to Android", GUILayout.Height(26)))
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                RefreshValidator();
            }

            if (GUILayout.Button("Switch to iOS", GUILayout.Height(26)))
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
                RefreshValidator();
            }
        }
    }

    private void DrawAutoIncrementToggle()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Build Number Auto-Increment", EditorStyles.boldLabel);

            bool autoInc = EditorPrefs.GetBool(PREF_AUTO_INCREMENT_ON_BUILD, true);
            bool newAutoInc =
                EditorGUILayout.ToggleLeft("Auto-increment build numbers on every build (recommended)", autoInc);

            if (newAutoInc != autoInc)
            {
                EditorPrefs.SetBool(PREF_AUTO_INCREMENT_ON_BUILD, newAutoInc);
            }

            EditorGUILayout.LabelField(
                "Rule: Build numbers are ONLY changed by MobileBuildGate during the build process. The window never increments them.",
                EditorStyles.wordWrappedMiniLabel
            );
        }
    }

    private void DrawVersionAndBuildInfo()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Version & Build Numbers (Read-only)", EditorStyles.boldLabel);

            // comment: Current target
            var activeTarget = EditorUserBuildSettings.activeBuildTarget;
            EditorGUILayout.LabelField($"Active Build Target: {activeTarget}");

            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("bundleVersion:", GUILayout.Width(140));
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion) ? "(empty)" : PlayerSettings.bundleVersion,
                    GUILayout.Height(18));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Android versionCode:", GUILayout.Width(140));
                EditorGUILayout.SelectableLabel(PlayerSettings.Android.bundleVersionCode.ToString(),
                    GUILayout.Height(18));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("iOS buildNumber:", GUILayout.Width(140));
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrWhiteSpace(PlayerSettings.iOS.buildNumber)
                        ? "(empty)"
                        : PlayerSettings.iOS.buildNumber, GUILayout.Height(18));
            }

            EditorGUILayout.Space(8);
            bool autoInc = EditorPrefs.GetBool(PREF_AUTO_INCREMENT_ON_BUILD, true);
            if (autoInc)
            {
                string nextAndroid = (PlayerSettings.Android.bundleVersionCode + 1).ToString();
                string nextIos = PredictNextIosBuildNumber();

                EditorGUILayout.LabelField($"Next Android versionCode (on build): {nextAndroid}");
                EditorGUILayout.LabelField($"Next iOS buildNumber (on build): {nextIos}");
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Auto-increment is OFF. Build numbers will NOT change automatically when you build.",
                    MessageType.Info);
            }
        }
    }

    private string PredictNextIosBuildNumber()
    {
        int current = 0;
        int.TryParse(PlayerSettings.iOS.buildNumber, out current);
        return (current + 1).ToString();
    }

    private void DrawValidator()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Validator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Green = OK, Yellow = Warning, Red = Build will be blocked (for Android/iOS builds).",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Status", GUILayout.Width(60));
                GUILayout.Label("Field", GUILayout.Width(280));
                GUILayout.Label("Current Value", GUILayout.ExpandWidth(true));
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(260));
            foreach (var row in _rows)
            {
                DrawRow(row);
                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawRow(Row row)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(GetIcon(row.status), GUILayout.Width(60));
                GUILayout.Label(row.field, GUILayout.Width(280));
                GUILayout.Label(row.value, GUILayout.ExpandWidth(true));
            }

            if (!string.IsNullOrWhiteSpace(row.note))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(row.note, EditorStyles.wordWrappedMiniLabel);
            }
        }
    }

    private GUIContent GetIcon(Status status)
    {
        if (status == Status.Ok) return _iconOk;
        if (status == Status.Warning) return _iconWarn;
        return _iconError;
    }

    private void RefreshValidator()
    {
        _rows.Clear();

        var activeTarget = EditorUserBuildSettings.activeBuildTarget;
        bool isAndroid = activeTarget == BuildTarget.Android;
        bool isiOS = activeTarget == BuildTarget.iOS;
        bool isMobile = isAndroid || isiOS;

        _rows.Add(new Row(
            "Build Target",
            activeTarget.ToString(),
            isMobile ? Status.Ok : Status.Warning,
            isMobile
                ? "Mobile target selected."
                : "Not mobile. Build blocking checks apply only for Android/iOS builds."
        ));
        BuildTargetGroup group =
            isMobile ? BuildPipeline.GetBuildTargetGroup(activeTarget) : BuildTargetGroup.Standalone;
        string bundleId = PlayerSettings.GetApplicationIdentifier(group);

        if (string.IsNullOrWhiteSpace(bundleId))
        {
            _rows.Add(new Row(
                "Bundle Identifier",
                "(empty)",
                isMobile ? Status.Error : Status.Warning,
                "Where: Project Settings > Player > Other Settings > Identification\nExample: com.company.product"
            ));
        }
        else
        {
            _rows.Add(new Row(
                "Bundle Identifier",
                bundleId,
                Status.Ok,
                "Set."
            ));

            bool suspicious = bundleId.Contains(" ") || !bundleId.Contains(".");
            if (suspicious)
            {
                _rows.Add(new Row(
                    "Bundle Identifier Format",
                    bundleId,
                    Status.Warning,
                    "Typical format: com.company.product (no spaces, includes at least one dot)."
                ));
            }
        }

        string company = PlayerSettings.companyName;
        _rows.Add(new Row(
            "Company Name",
            string.IsNullOrWhiteSpace(company) ? "(empty)" : company,
            string.IsNullOrWhiteSpace(company) ? Status.Warning : Status.Ok,
            "Where: Project Settings > Player > Company Name"
        ));
        string product = PlayerSettings.productName;
        _rows.Add(new Row(
            "Product Name",
            string.IsNullOrWhiteSpace(product) ? "(empty)" : product,
            string.IsNullOrWhiteSpace(product) ? Status.Warning : Status.Ok,
            "Where: Project Settings > Player > Product Name"
        ));
        int sceneCount = EditorBuildSettings.scenes != null ? EditorBuildSettings.scenes.Length : 0;
        if (sceneCount <= 0)
        {
            _rows.Add(new Row(
                "Scenes In Build",
                "(none)",
                isMobile ? Status.Error : Status.Warning,
                "Where: File > Build Settings > Scenes In Build"
            ));
        }
        else
        {
            _rows.Add(new Row(
                "Scenes In Build",
                sceneCount.ToString(),
                Status.Ok,
                "At least one scene is included."
            ));
        }

        if (isAndroid)
        {
            bool isDevBuild = EditorUserBuildSettings.development;

            if (!isDevBuild)
            {
                bool useCustom = PlayerSettings.Android.useCustomKeystore;
                string ks = PlayerSettings.Android.keystoreName;

                if (!useCustom || string.IsNullOrWhiteSpace(ks))
                {
                    _rows.Add(new Row(
                        "Android Keystore (Release)",
                        "(missing)",
                        Status.Error,
                        "Release builds require signing.\nWhere: Project Settings > Player > Android > Publishing Settings"
                    ));
                }
                else
                {
                    _rows.Add(new Row(
                        "Android Keystore (Release)",
                        "Set",
                        Status.Ok,
                        "Keystore configured for release builds."
                    ));
                }
            }
            else
            {
                _rows.Add(new Row(
                    "Android Keystore (Release)",
                    "N/A (Development Build)",
                    Status.Ok,
                    "Keystore is not required for development builds."
                ));
            }
        }
        else
        {
            _rows.Add(new Row(
                "Android Keystore (Release)",
                "N/A",
                Status.Ok,
                "Only relevant for Android builds."
            ));
        }
    }
}