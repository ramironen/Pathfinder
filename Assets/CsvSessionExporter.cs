using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// One row of session data for CSV export (registration, grid settings, stats).
/// </summary>
public struct SessionCsvPayload
{
    public string EndReason;
    public string PlayerName;
    public int PlayerAge;
    public string PlayerGender;
    public int IsRegistered;
    public int GameDurationSeconds;
    public int PathLength;
    public int NumberOfTurns;
    public float DisplayTime;
    public float SegmentDelay;
    public int ChancesPerPath;
    public int PathsTotal;
    public int Success;
    public int Fail;
    public int SecondsPlayed;
}

/// <summary>
/// Appends session rows to pathfinder_sessions.csv next to the game data folder
/// (project root in Editor; folder containing the .exe data folder in a player build).
/// </summary>
public static class CsvSessionExporter
{
    const string FileName = "pathfinder_sessions.csv";

    const string Header =
        "PlayerNumber," +
        "Time," +
        "PlayerName," +
        "PlayerAge [years]," +
        "PlayerGender," +
        "IsRegistered [0/1]," +
        "GameDuration [s]," +
        "PathLength [cells]," +
        "NumberOfTurns [-]," +
        "DisplayTime [s]," +
        "SegmentDelay [s]," +
        "ChancesPerPath [-]," +
        "PathsTotal [-]," +
        "Success [-]," +
        "Fail [-]," +
        "EndReason [-]," +
        "SecondsPlayed [s]";

    /// <summary>
    /// Editor / desktop player: CSV next to the project or install folder (parent of Assets / *_Data).
    /// Other platforms: <see cref="Application.persistentDataPath"/> (always writable).
    /// </summary>
    public static string GetCsvPath()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        string parent = Directory.GetParent(Application.dataPath)?.FullName;
        if (!string.IsNullOrEmpty(parent))
            return Path.Combine(parent, FileName);
#endif
        return Path.Combine(Application.persistentDataPath, FileName);
    }

    public static void AppendRow(SessionCsvPayload p)
    {
        string path = GetCsvPath();
        DateTime localNow = DateTime.Now;
        int playerNumber = ComputeNextPlayerNumber(path);

        string[] cells =
        {
            playerNumber.ToString(CultureInfo.InvariantCulture),
            Escape(FormatSimpleLocalTime(localNow)),
            Escape(p.PlayerName),
            p.PlayerAge.ToString(CultureInfo.InvariantCulture),
            Escape(p.PlayerGender),
            p.IsRegistered.ToString(CultureInfo.InvariantCulture),
            p.GameDurationSeconds.ToString(CultureInfo.InvariantCulture),
            p.PathLength.ToString(CultureInfo.InvariantCulture),
            p.NumberOfTurns.ToString(CultureInfo.InvariantCulture),
            p.DisplayTime.ToString("G9", CultureInfo.InvariantCulture),
            p.SegmentDelay.ToString("F2", CultureInfo.InvariantCulture),
            p.ChancesPerPath.ToString(CultureInfo.InvariantCulture),
            p.PathsTotal.ToString(CultureInfo.InvariantCulture),
            p.Success.ToString(CultureInfo.InvariantCulture),
            p.Fail.ToString(CultureInfo.InvariantCulture),
            Escape(p.EndReason),
            p.SecondsPlayed.ToString(CultureInfo.InvariantCulture)
        };

        string line = string.Join(",", cells);
        bool writeHeader = !File.Exists(path) || new FileInfo(path).Length == 0;

        try
        {
            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                if (writeHeader)
                    writer.WriteLine(Header);
                writer.WriteLine(line);
            }

            Debug.Log("Session CSV row written to: " + path);
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to write session CSV: " + ex.Message);
        }
    }

    /// <summary>Like 19-4-26 19:19 — local calendar, no seconds.</summary>
    static string FormatSimpleLocalTime(DateTime local)
    {
        int y2 = local.Year % 100;
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}-{1}-{2} {3}:{4}",
            local.Day,
            local.Month,
            y2.ToString("00", CultureInfo.InvariantCulture),
            local.Hour,
            local.Minute.ToString("00", CultureInfo.InvariantCulture));
    }

    static int ComputeNextPlayerNumber(string path)
    {
        if (!File.Exists(path))
            return 1;

        try
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length == 0)
                return 1;
            string h = lines[0];
            if (h.StartsWith("PlayerNumber", StringComparison.Ordinal) ||
                h.StartsWith("SessionIndex", StringComparison.Ordinal))
                return lines.Length;
            return lines.Length + 1;
        }
        catch
        {
            return 1;
        }
    }

    static string Escape(string value)
    {
        if (value == null)
            value = string.Empty;
        bool mustQuote = value.IndexOfAny(new[] { ',', '"', '\r', '\n', '\t' }) >= 0;
        string escaped = value.Replace("\"", "\"\"");
        if (mustQuote)
            return "\"" + escaped + "\"";
        return escaped;
    }
}
