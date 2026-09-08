using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("Использование: DumpReader <путь_к_дампу1> [<путь_к_дампу2>]");
            Console.WriteLine("Один аргумент — анализ одного дампа, два — сравнение снимков.");
            return;
        }

        string first = args[0];
        if (!File.Exists(first))
        {
            Console.WriteLine($"Файл не найден: {first}");
            return;
        }

        try
        {
            AnalyzeDump(first, "ДАМП 1");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Не удалось проанализировать дамп 1: {ex.Message}");
            return;
        }

        if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
        {
            string second = args[1];
            if (!File.Exists(second))
            {
                Console.WriteLine($"Файл не найден: {second}");
                return;
            }

            Console.WriteLine();
            AnalyzeDump(second, "ДАМП 2");
            Console.WriteLine();
            CompareDumps(first, second);
        }
    }

    static ClrRuntime CreateRuntime(DataTarget dt)
    {
        var clr = dt.ClrVersions.FirstOrDefault();
        if (clr is null)
        {
            throw new InvalidOperationException(
                "CLR не обнаружена в дампе (возможно, это дамп без управляемой кучи или создан под другой платформой).");
        }
        return clr.CreateRuntime();
    }

    static Dictionary<string, (long Count, ulong Size)> CollectStats(string dumpPath)
    {
        var stats = new Dictionary<string, (long Count, ulong Size)>();
        using var dt = DataTarget.LoadDump(dumpPath);
        var runtime = CreateRuntime(dt);
        var heap = runtime.Heap;

        foreach (var obj in heap.EnumerateObjects())
        {
            if (!obj.IsValid || obj.IsFree) continue;

            string typeName = obj.Type?.Name ?? "Unknown";
            ulong size = obj.Size;

            if (!stats.ContainsKey(typeName)) stats[typeName] = (0, 0);
            var (count, totalSize) = stats[typeName];
            stats[typeName] = (count + 1, totalSize + size);
        }
        return stats;
    }

    static (ulong LiveHeapSize, long RootCount) GetHeapSummary(string dumpPath)
    {
        ulong liveSize = 0;
        long roots = 0;
        using var dt = DataTarget.LoadDump(dumpPath);
        var runtime = CreateRuntime(dt);
        var heap = runtime.Heap;

        foreach (var obj in heap.EnumerateObjects())
        {
            if (obj.IsValid && !obj.IsFree) liveSize += obj.Size;
        }

        try { roots = heap.EnumerateRoots().Count(); } catch { roots = -1; }

        return (liveSize, roots);
    }

    static void AnalyzeDump(string dumpPath, string label)
    {
        Console.WriteLine($"========== {label}: {Path.GetFileName(dumpPath)} ==========");

        var stats = CollectStats(dumpPath);
        var (liveHeapSize, rootCount) = GetHeapSummary(dumpPath);

        Console.WriteLine($"Живая куча: {liveHeapSize / 1024.0 / 1024.0:F2} MB");
        if (rootCount >= 0) Console.WriteLine($"Корни GC: {rootCount:N0}");

        Console.WriteLine("ТОП-20 типов по занимаемой памяти:");
        Console.WriteLine($"{"Тип",-50} {"Кол-во",10} {"Размер",12}");
        Console.WriteLine(new string('-', 75));
        foreach (var kv in stats.OrderByDescending(x => x.Value.Size).Take(20))
        {
            Console.WriteLine($"{kv.Key,-50} {kv.Value.Count,10:N0} {kv.Value.Size / 1024.0 / 1024.0,10:F2} MB");
        }
        Console.WriteLine($"Всего типов: {stats.Count:N0}");
    }

    static void CompareDumps(string first, string second)
    {
        Console.WriteLine($"========== СРАВНЕНИЕ: {Path.GetFileName(first)} vs {Path.GetFileName(second)} ==========");

        var s1 = CollectStats(first);
        var s2 = CollectStats(second);
        var (h1, _) = GetHeapSummary(first);
        var (h2, _) = GetHeapSummary(second);

        Console.WriteLine($"Живая куча: дамп1 = {h1 / 1024.0 / 1024.0:F2} MB, дамп2 = {h2 / 1024.0 / 1024.0:F2} MB, " +
                          $"разница = {(h2 > h1 ? "+" : "")}{(long)((h2 - h1) / 1024.0 / 1024.0)} MB");

        Console.WriteLine("ТОП-10 типов с наибольшим ростом размера (дамп2 - дамп1):");
        Console.WriteLine($"{"Тип",-50} {"Кол-во (1 -> 2)",22} {"Размер (1 -> 2)",26}");
        Console.WriteLine(new string('-', 102));

        var allTypes = s1.Keys.Union(s2.Keys).ToList();
        var deltas = new List<(string Type, long CountDelta, long SizeDelta, long Count1, long Count2, long Size1MB, long Size2MB)>();

        foreach (var t in allTypes)
        {
            s1.TryGetValue(t, out var a);
            s2.TryGetValue(t, out var b);

            long sizeDelta = (long)(b.Size - a.Size);
            long countDelta = b.Count - a.Count;

            if (sizeDelta >= 1024)
            {
                deltas.Add((t, countDelta, sizeDelta, a.Count, b.Count,
                    (long)(a.Size / 1024.0 / 1024.0), (long)(b.Size / 1024.0 / 1024.0)));
            }
        }

        foreach (var d in deltas.OrderByDescending(x => x.SizeDelta).Take(10))
        {
            Console.WriteLine($"{d.Type,-50} {d.Count1,10:N0} -> {d.Count2,10:N0}   {d.Size1MB,9} -> {d.Size2MB,8} MB");
        }

        long newTypes = s2.Keys.Except(s1.Keys).Count();
        long goneTypes = s1.Keys.Except(s2.Keys).Count();
        Console.WriteLine($"\nТипов появилось: {newTypes:N0}, исчезло: {goneTypes:N0}.");
        Console.WriteLine("=> Рост кучи между снимками указывает на удерживаемые объекты (потенциальные утечки).");
    }
}