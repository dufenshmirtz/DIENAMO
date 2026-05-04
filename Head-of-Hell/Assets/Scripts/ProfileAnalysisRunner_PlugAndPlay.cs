using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class ProfileAnalysisRunner_PlugAndPlay : MonoBehaviour
{
    [Serializable]
    private struct LoadingStageText
    {
        public float progress;
        public string title;
        public string hint;
    }

    [Header("Pipeline EXE")]
    public string exeName = "runtime_profile_pipeline.exe";

    [Header("UI")]
    public ProfileAnalysisPanelUI panelUI;
    public GameObject loadingScreenRoot;
    [SerializeField] private Sprite[] loadingEyeFrames;
    [SerializeField] private LoadingStageText[] loadingStageTexts;

    [Header("Safety")]
    public int processTimeoutMs = 120000; // 120 sec

    private bool isRunning;
    private RuntimeLoadingOverlay runtimeLoadingOverlay;

    public async void RunPipelineAndRefresh()
    {
        if (isRunning)
            return;

        isRunning = true;
        ShowLoadingScreen();

        try
        {
            UpdateLoadingProgress(0.05f, "Preparing analysis", "Collecting telemetry files");
            PlugAndPlayPipelineRequest request = BuildPipelineRequest();
            SynchronizationContext unityContext = SynchronizationContext.Current;

            await Task.Yield();
            PlugAndPlayPipelineResult result = await Task.Run(() =>
                RunPipelineInternal(
                    request,
                    (progress, message, hint) =>
                    {
                        if (unityContext == null)
                            return;

                        unityContext.Post(_ => UpdateLoadingProgress(progress, message, hint), null);
                    }
                )
            );

            if (panelUI != null)
            {
                UpdateLoadingProgress(0.95f, "Reloading profile data", "Applying refreshed charts and stats");
                panelUI.RefreshAnalysis();

                if (panelUI.profileAnalysisRoot != null)
                    panelUI.profileAnalysisRoot.SetActive(true);
            }
            else
            {
                UnityEngine.Debug.LogWarning("Pipeline finished but panelUI is not assigned.");
            }

            UnityEngine.Debug.Log("Profiles JSON path: " + request.ProfilesJsonPath);
            UnityEngine.Debug.Log($"[profile_pipeline.exe] STDOUT:\n{result.Stdout}");

            if (!string.IsNullOrWhiteSpace(result.Stderr))
                UnityEngine.Debug.LogWarning($"[profile_pipeline.exe] STDERR:\n{result.Stderr}");

            UnityEngine.Debug.Log("PIPELINE SUCCESS -> JSON READY");
            UnityEngine.Debug.Log("RUNTIME JSON PATH = " + request.UnityJsonPath);
            UnityEngine.Debug.Log("RUNTIME JSON LAST WRITE = " + File.GetLastWriteTime(request.UnityJsonPath));

            if (!result.CompletedProgressReported)
                UpdateLoadingProgress(1f, "Analysis complete", "Profile data updated");

            await Task.Delay(180);
        }
        catch (Exception ex)
        {
            UpdateLoadingProgress(
                runtimeLoadingOverlay != null ? runtimeLoadingOverlay.CurrentProgress : 0f,
                "Analysis failed",
                "Check Unity Console for details"
            );
            UnityEngine.Debug.LogError("Plug-and-play pipeline failed: " + ex.Message);

            if (panelUI != null && panelUI.profileAnalysisRoot != null)
                panelUI.profileAnalysisRoot.SetActive(true);
        }
        finally
        {
            HideLoadingScreen();
            isRunning = false;
        }
    }

    private PlugAndPlayPipelineRequest BuildPipelineRequest()
    {
        string exePath = Path.Combine(
            Application.streamingAssetsPath,
            "Analysis",
            "Bin",
            exeName
        );

        if (!File.Exists(exePath))
            throw new FileNotFoundException("Pipeline EXE not found: " + exePath);

        string documentsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games",
            "Head of Hell"
        );

        string telemetryDir = Path.Combine(documentsRoot, "Telemetry", "Build");
        string runtimeOutDir = Path.Combine(documentsRoot, "PipelineOutputs", "runtime", "out");
        string runtimeEloOutDir = Path.Combine(documentsRoot, "PipelineOutputs", "runtime", "out_elo");

        string unityJsonPath = Path.Combine(
            Application.persistentDataPath,
            "ProfileAnalysis",
            "profile_analysis.json"
        );

        string profilesJsonPath = Path.Combine(
            Application.persistentDataPath,
            "profiles.json"
        );

        string unityJsonDir = Path.GetDirectoryName(unityJsonPath);
        if (!string.IsNullOrWhiteSpace(unityJsonDir))
            Directory.CreateDirectory(unityJsonDir);

        Directory.CreateDirectory(telemetryDir);
        Directory.CreateDirectory(runtimeOutDir);
        Directory.CreateDirectory(runtimeEloOutDir);

        string[] telemetryFiles = Directory.GetFiles(telemetryDir, "*.json", SearchOption.TopDirectoryOnly);
        if (telemetryFiles.Length == 0)
            throw new Exception("No telemetry JSON files found in Build folder: " + telemetryDir);

        return new PlugAndPlayPipelineRequest
        {
            ExePath = exePath,
            TelemetryDir = telemetryDir,
            RuntimeOutDir = runtimeOutDir,
            RuntimeEloOutDir = runtimeEloOutDir,
            UnityJsonPath = unityJsonPath,
            ProfilesJsonPath = profilesJsonPath
        };
    }

    private PlugAndPlayPipelineResult RunPipelineInternal(
        PlugAndPlayPipelineRequest request,
        Action<float, string, string> onProgress
    )
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = request.ExePath,
            Arguments =
                $"--telemetry-dir \"{request.TelemetryDir}\" " +
                $"--out-dir \"{request.RuntimeOutDir}\" " +
                $"--elo-out-dir \"{request.RuntimeEloOutDir}\" " +
                $"--unity-output \"{request.UnityJsonPath}\" " +
                $"--profiles-json \"{request.ProfilesJsonPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        StringBuilder stdout = new StringBuilder();
        StringBuilder stderr = new StringBuilder();
        bool completedProgressReported = false;

        using (Process process = new Process())
        {
            process.StartInfo = psi;
            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                    return;

                stdout.AppendLine(e.Data);

                if (!TryParseProgressLine(e.Data, out float progress, out string message, out string hint))
                    return;

                if (progress >= 1f)
                    completedProgressReported = true;

                onProgress?.Invoke(progress, message, hint);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                    return;

                stderr.AppendLine(e.Data);
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(processTimeoutMs))
            {
                try { process.Kill(); } catch { }
                throw new Exception("Pipeline EXE timed out.");
            }

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception(
                    "Pipeline EXE failed with exit code " + process.ExitCode +
                    "\nSTDERR:\n" + stderr
                );
            }
        }

        if (!File.Exists(request.UnityJsonPath))
            throw new FileNotFoundException("profile_analysis.json was not generated: " + request.UnityJsonPath);

        return new PlugAndPlayPipelineResult
        {
            Stdout = stdout.ToString(),
            Stderr = stderr.ToString(),
            CompletedProgressReported = completedProgressReported
        };
    }

    private void ShowLoadingScreen()
    {
        if (panelUI != null && panelUI.profileAnalysisRoot != null)
            panelUI.profileAnalysisRoot.SetActive(false);

        if (loadingScreenRoot != null)
        {
            loadingScreenRoot.SetActive(true);
            return;
        }

        if (runtimeLoadingOverlay == null)
            runtimeLoadingOverlay = RuntimeLoadingOverlay.Create(GetReferenceFont(), loadingEyeFrames);

        runtimeLoadingOverlay.Show();
    }

    private void HideLoadingScreen()
    {
        if (loadingScreenRoot != null)
        {
            loadingScreenRoot.SetActive(false);
            return;
        }

        if (runtimeLoadingOverlay != null)
            runtimeLoadingOverlay.Hide();
    }

    private void UpdateLoadingProgress(float progress, string message = null, string hint = null)
    {
        if (runtimeLoadingOverlay != null)
        {
            ResolveStageText(progress, message, hint, out string resolvedTitle, out string resolvedHint);
            runtimeLoadingOverlay.SetProgress(progress, resolvedTitle, resolvedHint);
        }
    }

    private void ResolveStageText(float progress, string fallbackTitle, string fallbackHint, out string title, out string hint)
    {
        float roundedProgress = (float)Math.Round(progress, 2, MidpointRounding.AwayFromZero);

        if (loadingStageTexts != null)
        {
            for (int i = 0; i < loadingStageTexts.Length; i++)
            {
                if (Mathf.Abs(loadingStageTexts[i].progress - roundedProgress) > 0.001f)
                    continue;

                title = string.IsNullOrWhiteSpace(loadingStageTexts[i].title) ? fallbackTitle : loadingStageTexts[i].title;
                hint = string.IsNullOrWhiteSpace(loadingStageTexts[i].hint) ? fallbackHint : loadingStageTexts[i].hint;
                return;
            }
        }

        switch (Mathf.RoundToInt(roundedProgress * 100f))
        {
            case 5:
                title = "Preparing dem filez";
                hint = "O VROMOESOROUXAKIAS TON PAIRNEI";
                return;
            case 20:
                title = "Extracting features";
                hint = "Reading match telemetry";
                return;
            case 35:
                title = "Extracting features";
                hint = "Feature datasets ready";
                return;
            case 45:
                title = "Calculating Elo";
                hint = "Updating player ratings";
                return;
            case 65:
                title = "Building profile summary";
                hint = "Aggregating player behaviour";
                return;
            case 82:
                title = "Exporting Unity data";
                hint = "Writing profile_analysis.json";
                return;
            case 95:
                title = "Reloading profile data";
                hint = "Applying refreshed charts and stats";
                return;
            case 100:
                title = "Analysis complete";
                hint = "Profile data updated";
                return;
            default:
                title = fallbackTitle;
                hint = fallbackHint;
                return;
        }
    }

    private static bool TryParseProgressLine(string line, out float progress, out string message, out string hint)
    {
        progress = 0f;
        message = null;
        hint = null;

        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("PROGRESS:", StringComparison.Ordinal))
            return false;

        string[] parts = line.Split(new[] { ':' }, 4);
        if (parts.Length < 3)
            return false;

        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out progress))
            return false;

        message = parts[2];
        hint = parts.Length >= 4 ? parts[3] : null;
        return true;
    }

    private TMP_FontAsset GetReferenceFont()
    {
        if (panelUI != null)
        {
            if (panelUI.profileNameText != null && panelUI.profileNameText.font != null)
                return panelUI.profileNameText.font;

            if (panelUI.styleLabelText != null && panelUI.styleLabelText.font != null)
                return panelUI.styleLabelText.font;
        }

        return TMP_Settings.defaultFontAsset;
    }

    private class PlugAndPlayPipelineRequest
    {
        public string ExePath;
        public string TelemetryDir;
        public string RuntimeOutDir;
        public string RuntimeEloOutDir;
        public string UnityJsonPath;
        public string ProfilesJsonPath;
    }

    private class PlugAndPlayPipelineResult
    {
        public string Stdout;
        public string Stderr;
        public bool CompletedProgressReported;
    }
}
