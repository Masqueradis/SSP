using System.Collections.Concurrent;
using System.Diagnostics;

namespace Lab5Task7;

readonly record struct Stats(long Sum, int Min, int Max, double Average)
{
    public static Stats operator +(Stats a, Stats b) => new(
        a.Sum + b.Sum,
        Math.Min(a.Min, b.Min),
        Math.Max(a.Max, b.Max),
        0);

    public Stats WithAverage(int count) => this with { Average = count == 0 ? 0 : (double)Sum / count };
}

static class Program
{
    static int _size = 10_000_000;

    static void Main(string[] args)
    {
        if (args.Length > 0 && int.TryParse(args[0], out int parsed) && parsed > 0)
        {
            _size = parsed;
        }

        Console.WriteLine("=== ЗАДАНИЕ 7. КОМПЛЕКСНОЕ: ПАРАЛЛЕЛЬНАЯ ОБРАБОТКА ДАННЫХ ===");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}");
        Console.WriteLine($"Размер массива: {_size:N0} (изменить: dotnet run -- 5000000)\n");

        int[] data = GenerateData(_size);

        Stats syncStats = ProcessSynchronously(data);
        Stats parallelStats = ProcessInParallel(data);
        Stats partitionedStats = ProcessWithPartitioner(data, Environment.ProcessorCount);
        Stats fourParts = ProcessWithPartitioner(data, 4);

        Console.WriteLine();
        Console.WriteLine("--- 7.4. Тяжёлая операция над элементом: честное сравнение ---");
        Console.WriteLine("  На дешёвой операции (сложение) накладные расходы параллелизма перекрывают выигрыш.");

        Stopwatch swHeavy = Stopwatch.StartNew();
        double heavySync = 0;
        for (int i = 0; i < data.Length; i++)
        {
            heavySync += HeavyWork(data[i]);
        }

        swHeavy.Stop();

        double heavyParallel = 0;
        double heavySplit = 0;
        object lockObj = new();

        Stopwatch swHeavyPar = Stopwatch.StartNew();
        Parallel.For(
            0,
            data.Length,
            () => 0.0,
            (i, _, local) => local + HeavyWork(data[i]),
            local =>
            {
                lock (lockObj)
                {
                    heavyParallel += local;
                }
            });
        swHeavyPar.Stop();

        Stopwatch swHeavyPart = Stopwatch.StartNew();
        var heavyPartitioner = Partitioner.Create(0, data.Length, data.Length / Environment.ProcessorCount);
        Parallel.ForEach(heavyPartitioner, range =>
        {
            double local = 0;
            for (int i = range.Item1; i < range.Item2; i++)
            {
                local += HeavyWork(data[i]);
            }

            lock (lockObj)
            {
                heavySplit += local;
            }
        });
        swHeavyPart.Stop();

        Console.WriteLine($"  Тяжёлая операция, синхронно          : {swHeavy.ElapsedMilliseconds} мс");
        Console.WriteLine($"  Тяжёлая операция, Parallel.For      : {swHeavyPar.ElapsedMilliseconds} мс (ускорение {(double)swHeavy.ElapsedMilliseconds / Math.Max(1, swHeavyPar.ElapsedMilliseconds):F2}x)");
        Console.WriteLine($"  Тяжёлая операция, Partitioner       : {swHeavyPart.ElapsedMilliseconds} мс (ускорение {(double)swHeavy.ElapsedMilliseconds / Math.Max(1, swHeavyPart.ElapsedMilliseconds):F2}x)");
        Console.WriteLine($"  Результаты совпадают: {Math.Abs(heavySync - heavyParallel) < 0.001 && Math.Abs(heavySync - heavySplit) < 0.001}");
        Console.WriteLine("  Вывод: параллелизм оправдан только когда работа на элемент заметно тяжелее");
        Console.WriteLine("  накладных расходов на запуск потоков и разбиение данных.\n");

        Console.WriteLine("--- 7.5. Сравнение результатов ---");
        Console.WriteLine($"  Синхронно            : сумма {syncStats.Sum:N0}, мин {syncStats.Min}, макс {syncStats.Max}, среднее {syncStats.Average:F4}");
        Console.WriteLine($"  Parallel.For         : сумма {parallelStats.Sum:N0}, мин {parallelStats.Min}, макс {parallelStats.Max}, среднее {parallelStats.Average:F4}");
        Console.WriteLine($"  Partitioner (на ядра): сумма {partitionedStats.Sum:N0}, мин {partitionedStats.Min}, макс {partitionedStats.Max}, среднее {partitionedStats.Average:F4}");
        Console.WriteLine($"  Partitioner (4 части): сумма {fourParts.Sum:N0}, мин {fourParts.Min}, макс {fourParts.Max}, среднее {fourParts.Average:F4}");
        Console.WriteLine($"  Суммы совпадают: {syncStats.Sum == parallelStats.Sum && syncStats.Sum == partitionedStats.Sum && syncStats.Sum == fourParts.Sum}");
        Console.WriteLine($"  Средние совпадают: {Math.Abs(syncStats.Average - parallelStats.Average) < 0.0001}");

        Console.WriteLine("\n=== ЗАДАНИЕ 7 ВЫПОЛНЕНО ===");
        Pause();
    }

    static int[] GenerateData(int size)
    {
        int[] data = new int[size];
        Random rand = new(42);
        for (int i = 0; i < size; i++)
        {
            data[i] = rand.Next(1, 1000);
        }

        return data;
    }

    static Stats ProcessSynchronously(int[] data)
    {
        Console.WriteLine("--- 7.1. Синхронная обработка ---");
        Stopwatch sw = Stopwatch.StartNew();

        long sum = 0;
        int min = int.MaxValue;
        int max = int.MinValue;
        for (int i = 0; i < data.Length; i++)
        {
            int value = data[i];
            sum += value;
            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        sw.Stop();
        Console.WriteLine($"  Время: {sw.ElapsedMilliseconds} мс");
        return new Stats(sum, min, max, (double)sum / data.Length);
    }

    static Stats ProcessInParallel(int[] data)
    {
        Console.WriteLine("--- 7.2. Parallel.For с локальными переменными потока ---");
        Stopwatch sw = Stopwatch.StartNew();

        Stats total = new(0, int.MaxValue, int.MinValue, 0);
        object aggregatorLock = new();

        Parallel.For(
            0,
            data.Length,
            () => new Stats(0, int.MaxValue, int.MinValue, 0),
            (i, _, local) =>
            {
                int value = data[i];
                return new Stats(local.Sum + value, Math.Min(local.Min, value), Math.Max(local.Max, value), 0);
            },
            local =>
            {
                lock (aggregatorLock)
                {
                    total += local;
                }
            });

        sw.Stop();
        Console.WriteLine($"  Время: {sw.ElapsedMilliseconds} мс, блокировок в агрегаторе: не более {Environment.ProcessorCount}");
        return total.WithAverage(data.Length);
    }

    static Stats ProcessWithPartitioner(int[] data, int parts)
    {
        Console.WriteLine($"--- 7.3. Parallel.ForEach с Partitioner на {parts} частей ---");
        Stopwatch sw = Stopwatch.StartNew();

        int chunk = (data.Length + parts - 1) / parts;
        ConcurrentBag<Stats> results = new();

        var partitioner = Partitioner.Create(0, data.Length, Math.Max(1, chunk));
        Parallel.ForEach(partitioner, range =>
        {
            long sum = 0;
            int min = int.MaxValue;
            int max = int.MinValue;
            for (int i = range.Item1; i < range.Item2; i++)
            {
                int value = data[i];
                sum += value;
                if (value < min)
                {
                    min = value;
                }

                if (value > max)
                {
                    max = value;
                }
            }

            results.Add(new Stats(sum, min, max, 0));
        });

        sw.Stop();

        Stats total = new(0, int.MaxValue, int.MinValue, 0);
        foreach (Stats s in results)
        {
            total += s;
        }

        Console.WriteLine($"  Время: {sw.ElapsedMilliseconds} мс, частей обработано: {results.Count}");
        return total.WithAverage(data.Length);
    }

    static double HeavyWork(int value) =>
        Math.Sqrt(Math.Abs(Math.Sin(value * 0.001) * Math.Cos(value * 0.002) * 1000));

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
