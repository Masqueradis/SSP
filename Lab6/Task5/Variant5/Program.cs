using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Lab6Task5;

static class Program
{
    static double Time(Action action)
    {
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    static bool Close(double a, double b, double tolerance = 1e-9)
    {
        double diff = Math.Abs(a - b);
        double scale = Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
        return diff / scale < tolerance;
    }

    static void Report(string what, double seqMs, double parMs, bool equal)
    {
        Console.WriteLine($"  {what,-34} последовательно {seqMs,8:F1} мс, параллельно {parMs,8:F1} мс, ускорение {seqMs / parMs,5:F2}x");
        Console.WriteLine($"  {string.Empty,-34} результаты совпали: {equal}");
    }


    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 5. ВАРИАНТ 5 ===");
        Console.WriteLine("Исходя из общего задания, продемонстрировать эффект от распараллеливания.");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");
        Variant5();
        Console.WriteLine();
        Console.WriteLine("=== ВАРИАНТ 5 ВЫПОЛНЕН ===");
        Pause();
    }
    static void Variant5()
    {
        Console.WriteLine("--- Вариант 5. Анализ текстовых данных ---");
        Console.WriteLine("  Параллельный подсчёт слов и поиск вхождений на сгенерированном тексте ~8 МБ.\n");

        StringBuilder sb = new(9_000_000);
        string[] words = { "многопоточность", "поток", "задача", "параллельный", "синхронизация", "мьютекс", "семафор", "дедлок" };
        Random rnd = new(3);
        while (sb.Length < 8_000_000)
        {
            sb.Append(words[rnd.Next(words.Length)]).Append(' ');
            if (sb.Length % 64 < words.Length)
            {
                sb.Append('\n');
            }
        }

        string text = sb.ToString();
        Console.WriteLine($"  Размер текста: {text.Length / 1_000_000:N2} МБ");

        string[] parts = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine($"  Строк для разбора: {parts.Length:N0}");

        long seqWords = 0;
        var seqCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        double seqMs = Time(() =>
        {
            foreach (string line in parts)
            {
                foreach (string word in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    seqWords++;
                    seqCounts.TryGetValue(word, out int c);
                    seqCounts[word] = c + 1;
                }
            }
        });

        long parWords = 0;
        Dictionary<string, int> parCounts = new(StringComparer.Ordinal);
        object sync = new();
        double parMs = Time(() =>
        {
            ConcurrentBag<Dictionary<string, int>> perChunk = new();
            long total = 0;

            Parallel.ForEach(
                Partitioner.Create(0, parts.Length, 64),
                range =>
                {
                    Dictionary<string, int> local = new(StringComparer.Ordinal);
                    long localWords = 0;
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        foreach (string word in parts[i].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        {
                            localWords++;
                            local.TryGetValue(word, out int c);
                            local[word] = c + 1;
                        }
                    }

                    Interlocked.Add(ref total, localWords);
                    perChunk.Add(local);
                });

            foreach (Dictionary<string, int> local in perChunk)
            {
                foreach ((string key, int value) in local)
                {
                    parCounts.TryGetValue(key, out int c);
                    parCounts[key] = c + value;
                }
            }

            parWords = total;
        });

        bool equal = seqWords == parWords && seqCounts.Count == parCounts.Count &&
                     seqCounts.All(kv => parCounts.TryGetValue(kv.Key, out int v) && v == kv.Value);
        Report("Подсчёт слов и частот", seqMs, parMs, equal);
        Console.WriteLine($"  Всего слов: {parWords:N0}, уникальных: {parCounts.Count}");
        foreach ((string word, int count) in parCounts.OrderByDescending(k => k.Value).Take(4))
        {
            Console.WriteLine($"    {word,-20} {count,10:N0}");
        }

        long found = 0;
        double findMs = Time(() => found = parts.AsParallel().Count(p => p.Contains("дедлок", StringComparison.Ordinal)));
        Console.WriteLine($"  Строк со словом \"дедлок\": {found:N0} за {findMs:F1} мс");
        Console.WriteLine("  Приём: каждый диапазон считает в свой словарь, словари соединяются после цикла.");
        Console.WriteLine("  Общий словарь с блокировкой дал бы правильный, но гораздо более медленный результат.\n");
    }

    static void Pause()
    {
        if (Console.IsInputRedirected)
        {
            return;
        }

        Console.Write("Нажмите Enter для выхода...");
        try
        {
            Console.ReadLine();
        }
        catch (Exception)
        {
        }
    }
}
