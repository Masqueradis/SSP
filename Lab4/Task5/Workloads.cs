using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Lab4Task5;

sealed class AnomalyStats
{
    public int Total { get; set; }
    public int Anomalies { get; set; }
    public int DateParseFailures { get; set; }
    public int TooShort { get; set; }
    public int NotEnoughParts { get; set; }
}

sealed class LogAnalyzer
{
    private const string FixedDateFormat = "yyyy-MM-dd HH:mm:ss.fff";

    private static readonly Regex CodeRegex = new(@"Code:\s*(\d+)", RegexOptions.Compiled);

    private readonly bool _useParseExact;
    private readonly AnomalyStats _stats = new();

    public LogAnalyzer(bool useParseExact)
    {
        _useParseExact = useParseExact;
    }

    public AnomalyStats Stats => _stats;

    public List<string> Generate(int count, Profiler profiler)
    {
        using (profiler.Enter("GenerateData"))
        {
            List<string> logs = new List<string>(count + 100);
            Random random = new Random(42);
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string[] levels = { "INFO", "WARNING", "ERROR", "DEBUG" };

        for (int i = 0; i < count; i++)
        {
            logs.Add(ComposeLog(random, chars, levels));
        }

        for (int i = 0; i < 100; i++)
        {
            logs.Add($"ANOMALY: {Guid.NewGuid()} | CRITICAL | Ошибка");
        }

        return logs;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string ComposeLog(Random random, string chars, string[] levels)
    {
        StringBuilder sb = new StringBuilder(160);
        sb.Append($"USER{random.Next(1, 100)}: ");
        sb.Append($"{DateTime.Now.AddMinutes(-random.Next(0, 300)):yyyy-MM-dd HH:mm:ss.fff} | ");
        sb.Append(levels[random.Next(levels.Length)]).Append(" | ");

        for (int j = 0; j < 50; j++)
        {
            sb.Append(chars[random.Next(chars.Length)]);
        }

        sb.Append($" | Code: {random.Next(100, 999)}");
        return sb.ToString();
    }

    public List<string> Filter(List<string> logs, Profiler profiler)
    {
        using (profiler.Enter("FilterLogs (IndexOf)"))
        {
            List<string> filtered = new List<string>(logs.Count);
            foreach (string log in logs)
            {
                if (log.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    log.IndexOf("WARNING", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    log.IndexOf("CRITICAL", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    filtered.Add(log);
                }
            }

            return filtered;
        }
    }

    public List<string> ProcessLogs(List<string> logs, Profiler profiler)
    {
        using (profiler.Enter("ProcessLogs"))
        {
            List<string> anomalies = new List<string>();
            _stats.Total = logs.Count;

            using (profiler.Enter("FilterLogs (IndexOf)"))
            {
                List<string> filtered = new List<string>(logs.Count);
                foreach (string log in logs)
                {
                    if (log.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        log.IndexOf("WARNING", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        log.IndexOf("CRITICAL", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        filtered.Add(log);
                    }
                }

                using (profiler.Enter("IsAnomaly (total with children)"))
                {
                    foreach (string log in filtered)
                    {
                        if (IsAnomaly(log, profiler))
                        {
                            _stats.Anomalies++;
                            anomalies.Add(log);
                        }
                    }
                }

                return anomalies;
            }
        }
    }

    private bool IsAnomaly(string log, Profiler profiler)
    {
        bool result;

        using (profiler.Enter("IsAnomaly.CheckLength"))
        {
            if (log.Length < 100)
            {
                _stats.TooShort++;
                return false;
            }
        }

        string[] parts;
        using (profiler.Enter("IsAnomaly.String.Split"))
        {
            parts = log.Split('|');
            if (parts.Length < 3)
            {
                _stats.NotEnoughParts++;
                return false;
            }
        }

        string dateText;
        using (profiler.Enter("IsAnomaly.ExtractDate"))
        {
            string head = parts[0];
            int separator = head.IndexOf(": ", StringComparison.Ordinal);
            dateText = separator >= 0 ? head[(separator + 2)..].Trim() : head.Trim();

            if (dateText.Length == 0)
            {
                _stats.DateParseFailures++;
                return false;
            }
        }

        DateTime logDate;
        using (profiler.Enter(_useParseExact
                   ? "IsAnomaly.DateTime.ParseExact"
                   : "IsAnomaly.DateTime.TryParse"))
        {
            if (_useParseExact)
            {
                bool ok = DateTime.TryParseExact(
                    dateText,
                    FixedDateFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out logDate);

                if (!ok)
                {
                    _stats.DateParseFailures++;
                    return false;
                }
            }
            else
            {
                if (!DateTime.TryParse(dateText, out logDate))
                {
                    _stats.DateParseFailures++;
                    return false;
                }
            }
        }

        int code;
        using (profiler.Enter("IsAnomaly.Regex.Match"))
        {
            Match match = CodeRegex.Match(parts[^1]);
            code = match.Success && int.TryParse(match.Groups[1].Value, out int parsed) ? parsed : 0;
        }

        using (profiler.Enter("IsAnomaly.Checks"))
        {
            bool hasAnomaly = log.Contains("ANOMALY", StringComparison.OrdinalIgnoreCase);
            bool isRecent = logDate > DateTime.Now.AddDays(-1);
            result = (hasAnomaly && isRecent) || (code > 500 && isRecent) || (hasAnomaly && code > 500);
        }

        return result;
    }

    public static string FixedFormat => FixedDateFormat;
}

sealed class WordFrequencyAnalyzer
{
    private static readonly char[] SplitChars = { ' ', '|', ':' };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "from", "this", "that", "user", "info", "error", "code",
        "debug", "warning", "warn", "fatal", "critical", "trace"
    };

    public Dictionary<string, int> CountWords(List<string> logs, Profiler profiler)
    {
        using (profiler.Enter("OwnCode.CountWords"))
        {
            Dictionary<string, int> frequency = new(StringComparer.OrdinalIgnoreCase);
            long tokens = 0;

            foreach (string log in logs)
            {
                foreach (string token in Tokenize(log, profiler))
                {
                    tokens++;
                    using (profiler.Enter("OwnCode.DictionaryLookup"))
                    {
                        frequency.TryGetValue(token, out int count);
                        frequency[token] = count + 1;
                    }
                }
            }

            Tokens = tokens;
            return frequency;
        }
    }

    public long Tokens { get; private set; }

    private List<string> Tokenize(string log, Profiler profiler)
    {
        using (profiler.Enter("OwnCode.Tokenize"))
        {
            string[] parts = log.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            List<string> words = new List<string>(parts.Length);

            foreach (string token in parts)
            {
                if (IsWord(token))
                {
                    words.Add(token);
                }
            }

            return words;
        }
    }

    private static bool IsWord(string token)
    {
        if (token.Length < 3 || StopWords.Contains(token))
        {
            return false;
        }

        foreach (char symbol in token)
        {
            if (!char.IsLetter(symbol))
            {
                return false;
            }
        }

        return true;
    }

    public string BuildReport(Dictionary<string, int> frequency, int top, Profiler profiler)
    {
        using (profiler.Enter("OwnCode.BuildReport"))
        {
            List<string> lines = new List<string>(top);
            foreach (KeyValuePair<string, int> pair in frequency.OrderByDescending(p => p.Value).Take(top))
            {
                lines.Add($"{pair.Key} = {pair.Value}");
            }

            using (profiler.Enter("OwnCode.String.Join"))
            {
                return string.Join("; ", lines);
            }
        }
    }
}

static class TextCorpus
{
    private static readonly string[] Messages =
    {
        "Connection timeout while requesting resource from remote host",
        "Failed to open database connection pool after exhausting retries",
        "User session expired and required re-authentication",
        "Cache entry evicted because memory pressure exceeded threshold",
        "Request completed successfully with status code",
        "Retrying request to service after transient network failure",
        "Background worker processed pending messages from queue",
        "Authentication token refreshed automatically before expiration",
        "Disk write latency exceeded configured threshold warning",
        "Scheduled job finished and released acquired resources"
    };

    private static readonly string[] Hosts = { "host-web-01", "host-web-02", "host-db-01", "host-cache-01" };

    private static readonly string[] Users = { "ivanov", "petrova", "sidorov", "kuznetsova" };

    public static List<string> Build(int lines)
    {
        Random random = new Random(11);
        List<string> corpus = new(lines);

        for (int i = 0; i < lines; i++)
        {
            DateTime moment = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local)
                .AddSeconds(random.Next(0, 20_000_000));

            string level = random.Next(100) switch
            {
                < 60 => "INFO",
                < 90 => "WARNING",
                _ => "ERROR"
            };

            string message = Messages[random.Next(Messages.Length)];
            string host = Hosts[random.Next(Hosts.Length)];
            string user = Users[random.Next(Users.Length)];

            corpus.Add($"{moment:yyyy-MM-dd HH:mm:ss.fff} | {level} | {host} | user={user} | {message}");
        }

        return corpus;
    }
}
