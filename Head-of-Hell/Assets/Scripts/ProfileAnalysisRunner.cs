using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class ProfileAnalysisRunner : MonoBehaviour
{
    [Header("Runtime Pipeline")]
    public string pipelineExeName = "runtime_profile_pipeline.exe";

    [Header("UI")]
    public ProfileAnalysisPanelUI panelUI;
    public GameObject loadingScreenRoot;

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
            PipelineRequest request = BuildPipelineRequest();
            SynchronizationContext unityContext = SynchronizationContext.Current;

            await Task.Yield();
            PipelineResult result = await Task.Run(() =>
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

            UnityEngine.Debug.Log("PIPELINE EXE: " + request.ExePath);
            UnityEngine.Debug.Log("PIPELINE ARGS: " + request.Arguments);
            UnityEngine.Debug.Log("PIPELINE WORKDIR: " + request.WorkingDirectory);
            UnityEngine.Debug.Log($"[runtime_profile_pipeline] STDOUT:\n{result.Stdout}");

            if (!string.IsNullOrWhiteSpace(result.Stderr))
                UnityEngine.Debug.LogWarning($"[runtime_profile_pipeline] STDERR:\n{result.Stderr}");

            UnityEngine.Debug.Log("Full analysis pipeline completed.");
            LogIfExists(Path.Combine(result.OutDir, "dataset_round_level.csv"));
            LogIfExists(Path.Combine(result.OutDir, "dataset_profile_level.csv"));
            LogIfExists(Path.Combine(result.EloOutDir, "elo_overall.csv"));
            LogIfExists(result.UnityOutput);

            if (panelUI != null)
            {
                UpdateLoadingProgress(0.95f, "Reloading profile data", "Applying refreshed charts and stats");
                panelUI.RefreshAnalysis();

                if (panelUI.profileAnalysisRoot != null)
                    panelUI.profileAnalysisRoot.SetActive(true);
            }

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
            UnityEngine.Debug.LogError("Failed to run full pipeline: " + ex.Message);

            if (panelUI != null && panelUI.profileAnalysisRoot != null)
                panelUI.profileAnalysisRoot.SetActive(true);
        }
        finally
        {
            HideLoadingScreen();
            isRunning = false;
        }
    }

    private PipelineRequest BuildPipelineRequest()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        string telemetryDir = Path.Combine(documents, "My Games", "Head of Hell", "Telemetry");
        string telemetryBuildDir = Path.Combine(telemetryDir, "Build");

        if (Directory.Exists(telemetryBuildDir))
            telemetryDir = telemetryBuildDir;

        string analysisRoot = Path.Combine(documents, "My Games", "Head of Hell", "Analysis");
        string outDir = Path.Combine(analysisRoot, "out");
        string eloOutDir = Path.Combine(analysisRoot, "out_elo");
        string unityOutput = Path.Combine(Application.streamingAssetsPath, "ProfileAnalysis", "profile_analysis.json");

        Directory.CreateDirectory(analysisRoot);
        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(eloOutDir);

        string unityOutputDir = Path.GetDirectoryName(unityOutput);
        if (!string.IsNullOrWhiteSpace(unityOutputDir))
            Directory.CreateDirectory(unityOutputDir);

        string exePath = Path.Combine(Application.streamingAssetsPath, "Analysis", pipelineExeName);

        if (!File.Exists(exePath))
            throw new FileNotFoundException("Pipeline exe not found: " + exePath);

        string args =
            $"--telemetry-dir \"{telemetryDir}\" " +
            $"--out-dir \"{outDir}\" " +
            $"--elo-out-dir \"{eloOutDir}\" " +
            $"--unity-output \"{unityOutput}\"";

        return new PipelineRequest
        {
            ExePath = exePath,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(exePath),
            OutDir = outDir,
            EloOutDir = eloOutDir,
            UnityOutput = unityOutput
        };
    }

    private PipelineResult RunPipelineInternal(
        PipelineRequest request,
        Action<float, string, string> onProgress
    )
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = request.ExePath,
            Arguments = request.Arguments,
            WorkingDirectory = request.WorkingDirectory,
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

            process.WaitForExit();
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new Exception($"runtime_profile_pipeline failed with exit code {process.ExitCode}");

            return new PipelineResult
            {
                OutDir = request.OutDir,
                EloOutDir = request.EloOutDir,
                UnityOutput = request.UnityOutput,
                Stdout = stdout.ToString(),
                Stderr = stderr.ToString(),
                CompletedProgressReported = completedProgressReported
            };
        }
    }

    private void LogIfExists(string path)
    {
        if (File.Exists(path))
            UnityEngine.Debug.Log($"Updated file: {path} | LastWrite={File.GetLastWriteTime(path)}");
        else
            UnityEngine.Debug.LogWarning("Missing expected file: " + path);
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
            runtimeLoadingOverlay = RuntimeLoadingOverlay.Create(GetReferenceFont());

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
            runtimeLoadingOverlay.SetProgress(progress, message, hint);
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

    private class PipelineResult
    {
        public string OutDir;
        public string EloOutDir;
        public string UnityOutput;
        public string Stdout;
        public string Stderr;
        public bool CompletedProgressReported;
    }

    private class PipelineRequest
    {
        public string ExePath;
        public string Arguments;
        public string WorkingDirectory;
        public string OutDir;
        public string EloOutDir;
        public string UnityOutput;
    }
}
