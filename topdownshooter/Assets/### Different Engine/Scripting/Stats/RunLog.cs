using System;
using System.IO;
using System.Text;
using UnityEngine;

// a finished run written out as a text file for the player to send us: who, where, how it
// ended, and everything RunStats counted (RunStats.Report). the files go in a Runs folder in the
// game's save folder (Application.persistentDataPath: on Windows
// %USERPROFILE%\AppData\LocalLow\OFF-BY-ONE\Wanjian\Runs), one a run; logging the same run
// again writes over its file
public static class RunLog
{
    private static string lastFile;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => lastFile = null;

    public static string Folder => Native(Path.Combine(Application.persistentDataPath, "Runs"));

    // writes the run, and returns the file, or null if it couldn't be written
    public static string Save(bool won)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            if (lastFile == null)
            {
                string map = MapSelection.Current != null ? MapSelection.Current.title : "Unknown map";
                string name = $"Run {DateTime.Now:yyyy-MM-dd HH-mm-ss} {Safe(map)}.txt";
                lastFile = Native(Path.Combine(Folder, name));
            }
            File.WriteAllText(lastFile, Text(won), Encoding.UTF8);
            return lastFile;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"RunLog: couldn't write the run: {e.Message}");
            return null;
        }
    }

    private static string Text(bool won)
    {
        var stats = UnityEngine.Object.FindFirstObjectByType<StatContext>();
        var character = stats != null && stats.Character != null ? stats.Character : CharacterSelection.Current;
        var map = MapSelection.Current;

        var s = new StringBuilder();
        s.AppendLine($"Wanjian {Application.version}, {DateTime.Now:yyyy-MM-dd HH:mm}");
        s.AppendLine($"{(character != null ? character.displayName : "Unknown")} on {(map != null ? map.title : "Unknown map")}, {GameMode.Current}");
        s.AppendLine(won ? "Won" : "Lost");
        s.AppendLine();
        s.Append(RunStats.Report());
        s.AppendLine();
        s.AppendLine($"{SystemInfo.operatingSystem}, {SystemInfo.processorType}, {SystemInfo.graphicsDeviceName}, {Screen.width}x{Screen.height}");
        return s.ToString();
    }

    // the folder open in the OS's file browser, brought to the front, the run's file picked out
    public static void Reveal(string file)
    {
        if (string.IsNullOrEmpty(file)) file = lastFile;
        string folder = string.IsNullOrEmpty(file) ? Folder : Path.GetDirectoryName(file);
        try
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(File.Exists(file ?? "") ? file : folder);
#elif UNITY_STANDALONE_WIN
            if (File.Exists(file ?? "")) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file}\"");
            else System.Diagnostics.Process.Start("explorer.exe", $"\"{folder}\"");
#elif UNITY_STANDALONE_OSX
            if (File.Exists(file ?? "")) System.Diagnostics.Process.Start("open", $"-R \"{file}\"");
            else System.Diagnostics.Process.Start("open", $"\"{folder}\"");
#else
            Application.OpenURL("file://" + folder);
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning($"RunLog: couldn't open {folder}: {e.Message}");
            Application.OpenURL("file://" + folder);
        }
    }

    // the OS's own separators, so Explorer takes the path as it is shown
    private static string Native(string path) => Path.GetFullPath(path).Replace('/', Path.DirectorySeparatorChar);

    private static string Safe(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }
}
