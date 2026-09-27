using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace Lab4Task2;

static class HeapInfo
{
    private static readonly string[] GenerationNames = { "Gen0", "Gen1", "Gen2", "LOH " };

    public static long Total() => GC.GetTotalMemory(forceFullCollection: true);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void FullCollect()
    {
        for (int i = 0; i < 3; i++)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }

    public static long LohSizeBytes()
    {
        GCMemoryInfo info = GC.GetGCMemoryInfo();
        ReadOnlySpan<GCGenerationInfo> generations = info.GenerationInfo;

        return generations.Length > 3 ? generations[3].SizeAfterBytes : -1;
    }

    public static long LohFragmentationBytes()
    {
        GCMemoryInfo info = GC.GetGCMemoryInfo();
        ReadOnlySpan<GCGenerationInfo> generations = info.GenerationInfo;

        return generations.Length > 3 ? generations[3].FragmentationAfterBytes : -1;
    }

    public static string DescribeLoh()
    {
        GCMemoryInfo info = GC.GetGCMemoryInfo();
        ReadOnlySpan<GCGenerationInfo> generations = info.GenerationInfo;

        if (generations.Length <= 3)
        {
            return $"LOH недоступен: в GenerationInfo всего {generations.Length} записей";
        }

        GCGenerationInfo loh = generations[3];
        double fragPercent = loh.SizeAfterBytes > 0
            ? 100.0 * loh.FragmentationAfterBytes / loh.SizeAfterBytes
            : 0;

        return $"размер после GC: {loh.SizeAfterBytes / 1024.0 / 1024.0:F2} МБ, " +
               $"фрагментация: {loh.FragmentationAfterBytes / 1024.0 / 1024.0:F2} МБ ({fragPercent:F1} %)";
    }

    public static void Print(string title, long deltaBytes)
    {
        Console.WriteLine($"--- {title} ---");
        Console.WriteLine($"  Всего в куче: {Total() / 1024.0 / 1024.0:F2} МБ   (прирост: {deltaBytes / 1024.0 / 1024.0:F2} МБ)");
        Console.WriteLine($"  LOH: {DescribeLoh()}");
        Console.WriteLine($"  Сборок: Gen0={GC.CollectionCount(0)}, Gen1={GC.CollectionCount(1)}, Gen2={GC.CollectionCount(2)}");

        GCMemoryInfo info = GC.GetGCMemoryInfo();
        for (int i = 0; i < Math.Min(info.GenerationInfo.Length, GenerationNames.Length); i++)
        {
            GCGenerationInfo gen = info.GenerationInfo[i];
            Console.WriteLine($"    {GenerationNames[i]}: {gen.SizeAfterBytes / 1024.0 / 1024.0,8:F2} МБ" +
                              $"  (до GC: {gen.SizeBeforeBytes / 1024.0 / 1024.0,8:F2} МБ," +
                              $" фрагм. {gen.FragmentationAfterBytes / 1024.0 / 1024.0:F2} МБ)");
        }

        using Process p = Process.GetCurrentProcess();
        p.Refresh();
        Console.WriteLine($"  Working Set: {p.WorkingSet64 / 1024.0 / 1024.0:F2} МБ, Private: {p.PrivateMemorySize64 / 1024.0 / 1024.0:F2} МБ");
    }
}

static class Program
{
    private static void Header(string text)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(text);
        Console.WriteLine(new string('=', 78));
    }

    private static void SubHeader(string text)
    {
        Console.WriteLine();
        Console.WriteLine($"### {text}");
    }

    static void Main()
    {
        Console.WriteLine("ЛАБОРАТОРНАЯ РАБОТА 4 — ЗАДАНИЕ 2: ИССЛЕДОВАНИЕ LOH (Large Object Heap)");
        Console.WriteLine($"Среда: {Environment.Version}  |  Порог LOH: 85 000 байт");
        Console.WriteLine($"Серверный GC: {GCSettings.IsServerGC}  |  Concurrency: {GCSettings.LatencyMode}");

        Threshold();
        LohMonitoring();
        Compaction();

        Console.WriteLine();
        Console.WriteLine("Нажмите любую клавишу...");
        try { Console.ReadKey(); } catch { }
    }

    private static void Threshold()
    {
        Header("ЗАДАНИЕ 2.1 — СОЗДАНИЕ ОБЪЕКТОВ В LOH");

        byte[] small = new byte[80_000];
        byte[] large = new byte[90_000];

        Console.WriteLine("Простые массивы байт:");
        Console.WriteLine($"  small = new byte[80000]: поколение Gen{GC.GetGeneration(small)}, размер {small.Length} байт");
        Console.WriteLine($"  large = new byte[90000]: поколение Gen{GC.GetGeneration(large)}, размер {large.Length} байт");
        Console.WriteLine("  => Объекты крупнее 85 000 байт попадают в LOH, который является частью поколения 2.");
        Console.WriteLine("  => В LOH попадает САМА ПАМЯТЬ МАССИВА, а не объект-обёртка:");

        object boxedSmall = 42;
        object boxedLarge = Enumerable.Range(0, 30_000).ToArray();
        Console.WriteLine($"  упакованный int: поколение Gen{GC.GetGeneration(boxedSmall)} (LOH не занимает)");
        Console.WriteLine($"  массив int[30000] (120 008 байт данных): поколение Gen{GC.GetGeneration(boxedLarge)} -> LOH");
        Console.WriteLine($"  => Точный порог — 85 000 байт ПОЛНОГО размера объекта; массивы из ссылок имеют иной порог.");
        Console.WriteLine($"  => Справка: {GCSettings.LargeObjectHeapCompactionMode}");

        SubHeader("Поиск порога перебором размеров массива");
        int threshold = FindThreshold();
        Console.WriteLine($"  Первый размер ДАННЫХ, при котором массив попадает в LOH: {threshold} байт");
        Console.WriteLine($"  Полный размер объекта при этом: {threshold + ArrayHeaderSize} байт (заголовок массива byte[] на x64 = {ArrayHeaderSize} байт)");
        Console.WriteLine($"  Проверка: byte[{threshold - 1}] -> {Describe(threshold - 1)}");
        Console.WriteLine($"  Проверка: byte[{threshold}] -> {Describe(threshold)}");
        Console.WriteLine("  => Порог 85 000 байт применяется к ПОЛНОМУ размеру объекта, а не к длине массива:");
        Console.WriteLine($"     длина данных byte[] = полный размер - {ArrayHeaderSize} (заголовок), то есть {threshold} байт,");
        Console.WriteLine($"     а последний массив, который ЕЩЁ помещается в обычную кучу, имеет {threshold - 1} байт данных.");
    }

    private const int ArrayHeaderSize = 24;

    private static string Describe(int size)
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        byte[] buffer = new byte[size];
        int generation = GC.GetGeneration(buffer);
        GC.KeepAlive(buffer);

        return generation == 2
            ? $"Gen{generation} -> LOH"
            : $"Gen{generation} -> обычная куча";
    }

    private static int FindThreshold()
    {
        int low = 80_000;
        int high = 100_000;

        while (low < high)
        {
            int mid = low + (high - low) / 2;

            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            byte[] probe = new byte[mid];
            bool inLoh = GC.GetGeneration(probe) == 2;
            GC.KeepAlive(probe);

            if (inLoh)
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }

        return low;
    }

    private static void LohMonitoring()
    {
        Header("ЗАДАНИЕ 2.2 — МОНИТОРИНГ LOH: 100 объектов по 100 КБ");

        HeapInfo.FullCollect();
        long baseline = HeapInfo.Total();
        HeapInfo.Print("1. До выделения", 0);

        SubHeader("Выделяем 100 объектов по 100 КБ");
        var keepAlive = new List<byte[]>();
        for (int i = 0; i < 100; i++)
        {
            keepAlive.Add(new byte[100 * 1024]);
        }
        GC.KeepAlive(keepAlive);

        HeapInfo.FullCollect();
        long afterAllocate = HeapInfo.Total();
        HeapInfo.Print("2. После выделения + GC.Collect(2)", afterAllocate - baseline);
        Console.WriteLine($"  Ожидаемый прирост данных: {100 * 100 * 1024 / 1024.0 / 1024.0:F2} МБ");
        Console.WriteLine("  => GC.Collect(2) НЕ освобождает LOH: объекты ещё достижимы из keepAlive.");

        SubHeader("Отпускаем ссылки на массивы");
        keepAlive.Clear();
        keepAlive.TrimExcess();
        HeapInfo.FullCollect();

        long afterRelease = HeapInfo.Total();
        HeapInfo.Print("3. После освобождения ссылок + GC.Collect(2)", afterRelease - baseline);
        Console.WriteLine("  => Объекты стали недостижимы, и Gen2/LOH-сборка их освободила.");

        SubHeader("Почему обычный GC.Collect(2) не уплотняет LOH");
        Console.WriteLine($"  Режим уплотнения LOH: {GCSettings.LargeObjectHeapCompactionMode}");
        Console.WriteLine("  CompactOnce означает «уплотнить при ближайшей полной сборке», но даже он");
        Console.WriteLine("  не срабатывает в фоновых сборках по умолчанию — смотрим следующий шаг.");
    }

    private static void Compaction()
    {
        Header("ДОПОЛНИТЕЛЬНО — ПРИНУДИТЕЛЬНАЯ КОМПАКТАЦИЯ LOH");

        HeapInfo.FullCollect();
        long baseline = HeapInfo.Total();
        Console.WriteLine($"Старт: {baseline / 1024.0 / 1024.0:F2} МБ, LOH: {HeapInfo.DescribeLoh()}");

        SubHeader("Создаём LOH-объекты разного размера, затем оставляем половину");
        var keepAlive = new List<byte[]>();
        for (int i = 0; i < 200; i++)
        {
            byte[] block = new byte[100_000 + i * 37];
            if (i % 2 == 0)
            {
                keepAlive.Add(block);
            }
        }
        GC.KeepAlive(keepAlive);

        HeapInfo.FullCollect();
        long beforeCompaction = HeapInfo.Total();
        Console.WriteLine($"После выделения 200 объектов (100 удерживается): {beforeCompaction / 1024.0 / 1024.0:F2} МБ");
        Console.WriteLine($"  LOH: {HeapInfo.DescribeLoh()}");

        SubHeader("Освобождаем оставшиеся ссылки и собираем БЕЗ уплотнения");
        keepAlive.Clear();
        keepAlive.TrimExcess();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);
        GC.WaitForPendingFinalizers();
        long afterNonCompact = HeapInfo.Total();
        Console.WriteLine($"Сборка без уплотнения: {afterNonCompact / 1024.0 / 1024.0:F2} МБ");
        Console.WriteLine($"  LOH: {HeapInfo.DescribeLoh()}");

        SubHeader("Включаем CompactOnce и запускаем обычную полную сборку");
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        long afterCompact = HeapInfo.Total();
        Console.WriteLine($"Сборка с уплотнением: {afterCompact / 1024.0 / 1024.0:F2} МБ");
        Console.WriteLine($"  LOH: {HeapInfo.DescribeLoh()}");

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        HeapInfo.FullCollect();
        Console.WriteLine($"Итог после уплотнения: {HeapInfo.Total() / 1024.0 / 1024.0:F2} МБ");
        Console.WriteLine($"  LOH: {HeapInfo.DescribeLoh()}");

        Header("ВЫВОДЫ ПО ЗАДАНИЮ 2");
        Console.WriteLine("1) LOH (Large Object Heap) — отдельная область кучи для объектов >= 85 000 байт.");
        Console.WriteLine("2) Управляется вместе с поколением 2: GC.GetGeneration для LOH-объекта вернёт 2.");
        Console.WriteLine("3) По умолчанию LOH НЕ уплотняется: свободные места остаются между живыми");
        Console.WriteLine("   объектами, чтобы не перемещать большие массивы (это дорого).");
        Console.WriteLine("4) Уплотнить LOH можно принудительно: GCSettings.LargeObjectHeapCompactionMode");
        Console.WriteLine("   = CompactOnce, затем GC.Collect(2) — фрагментация падает.");
        Console.WriteLine("5) Поэтому частые операции с большими буферами (создание/удаление) приводят к");
        Console.WriteLine("   росту LOH-фрагментации: массив следует переиспользовать, а не пересоздавать.");
    }
}
