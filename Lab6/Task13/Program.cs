using System.Collections.Concurrent;
using System.Diagnostics;

namespace Lab5Task13;

static class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== ЗАДАНИЕ 13. PARALLEL.FOREACH: ОБРАБОТКА, ФИЛЬТРАЦИЯ, ОСТАНОВКА ===");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");

        BasicForeach();
        Filtering();
        BreakVsStop();
        Cancellation();
        AggregationAndComparison();

        Console.WriteLine("=== ЗАДАНИЕ 13 ВЫПОЛНЕНО ===");
        Pause();
    }

    static List<int> Numbers(int n) => Enumerable.Range(0, n).ToList();

    static long HeavyWork(int value)
    {
        long acc = 0;
        for (int i = 1; i <= 40_000; i++)
        {
            acc += (long)value * i % 9973;
        }

        return acc;
    }

    static void BasicForeach()
    {
        Console.WriteLine("--- 13.1. Базовый Parallel.ForEach ---");
        Console.WriteLine("  Parallel.ForEach сам разбивает коллекцию на части и обрабатывает их одновременно.\n");

        List<int> data = Numbers(2000);
        long sum = 0;
        object sync = new();

        Parallel.ForEach(data, value =>
        {
            long result = HeavyWork(value);
            lock (sync)
            {
                sum += result;
            }
        });

        long expected = 0;
        foreach (int value in data)
        {
            expected += HeavyWork(value);
        }

        Console.WriteLine($"  Обработано элементов: {data.Count:N0}");
        Console.WriteLine($"  Сумма с lock: {sum} — совпала с эталоном: {sum == expected}");
        Console.WriteLine($"  Для длительных операций Parallel.ForEach почти всегда выгоднее обычного цикла.\n");
    }

    static void Filtering()
    {
        Console.WriteLine("--- 13.2. Фильтрация ---");
        List<int> data = Numbers(500_000);

        var evenFirst = data.Where(x => x % 2 == 0).ToList();
        var sw = Stopwatch.StartNew();
        int countBefore = 0;
        Parallel.ForEach(evenFirst, _ => Interlocked.Increment(ref countBefore));
        sw.Stop();
        Console.WriteLine($"  Where() до Parallel.ForEach: обработано {countBefore:N0}, время {sw.ElapsedMilliseconds} мс");

        int countInside = 0;
        sw.Restart();
        Parallel.ForEach(data, value =>
        {
            if (value % 2 == 0)
            {
                Interlocked.Increment(ref countInside);
            }
        });
        sw.Stop();
        Console.WriteLine($"  Условие внутри тела: обработано {countInside:N0}, время {sw.ElapsedMilliseconds} мс");

        Console.WriteLine("  Итог одинаков, но Where() уменьшает объём параллельной работы, а фильтр внутри");
        Console.WriteLine("  тела экономит память на промежуточный список. Выбор зависит от доли отсева.\n");
    }

    static void BreakVsStop()
    {
        Console.WriteLine("--- 13.3. Досрочная остановка: Break и Stop ---");
        Console.WriteLine("  Break: итерации с меньшим индексом обязаны быть выполнены, остальные могут быть отброшены.");
        Console.WriteLine("  Stop:  останавливает цикл как можно быстрее, никаких гарантий по индексам нет.");
        Console.WriteLine("  В теле каждой итерации стоит задержка 3 мс, иначе цикл успевает завершиться");
        Console.WriteLine("  раньше, чем остановка подействует, и разница не будет видна.\n");

        const int target = 500;
        List<int> data = Numbers(1000);
        ParallelOptions options = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };

        ConcurrentBag<int> withBreak = new();
        var sw = Stopwatch.StartNew();
        Parallel.ForEach(data, options, (value, state) =>
        {
            Thread.Sleep(3);
            withBreak.Add(value);
            if (value == target)
            {
                state.Break();
            }
        });
        sw.Stop();
        var breakSet = new HashSet<int>(withBreak);
        bool guarantee = Enumerable.Range(0, target + 1).All(breakSet.Contains);
        Console.WriteLine($"  Break при значении {target}: обработано {withBreak.Count} из {data.Count}, {sw.ElapsedMilliseconds} мс");
        Console.WriteLine($"  Все индексы 0..{target} обработаны (гарантия Break): {guarantee}");
        Console.WriteLine($"  Максимальный обработанный индекс: {breakSet.Max()}");

        ConcurrentBag<int> withStop = new();
        sw.Restart();
        Parallel.ForEach(data, options, (value, state) =>
        {
            Thread.Sleep(3);
            withStop.Add(value);
            if (value == target)
            {
                state.Stop();
            }
        });
        sw.Stop();
        Console.WriteLine($"  Stop при значении {target}: обработано {withStop.Count} из {data.Count}, {sw.ElapsedMilliseconds} мс");
        Console.WriteLine("  Число обработанных элементов при Stop не детерминировано: важно лишь, что цикл");
        Console.WriteLine("  прервался сразу, а не досчитал коллекцию.");
        Console.WriteLine();
        Console.WriteLine("  Break гарантирует корректный частичный результат: всё до точки остановки посчитано.");
        Console.WriteLine("  Stop прерывает как можно раньше и подходит для поиска первого совпадения.\n");
    }

    static void Cancellation()
    {
        Console.WriteLine("--- 13.4. Отмена через CancellationToken ---");
        using CancellationTokenSource cts = new();
        List<int> data = Numbers(1_000_000);
        long processed = 0;
        var sw = Stopwatch.StartNew();

        Thread canceller = new(() =>
        {
            Thread.Sleep(60);
            cts.Cancel();
            Console.WriteLine($"  [{sw.ElapsedMilliseconds,5} мс] Внешний поток вызвал CancellationTokenSource.Cancel()");
        });

        canceller.Start();
        try
        {
            Parallel.ForEach(
                data,
                new ParallelOptions
                {
                    CancellationToken = cts.Token,
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                },
                value =>
                {
                    long acc = 0;
                    for (int i = 1; i <= 5_000; i++)
                    {
                        acc += (long)value * i % 9973;
                    }

                    Interlocked.Increment(ref processed);
                });

            Console.WriteLine("  Цикл завершился до отмены, успел обработать всё.");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"  Поймано OperationCanceledException: обработка остановлена по требованию.");
        }

        sw.Stop();
        canceller.Join();
        Console.WriteLine($"  Успело обработаться {processed:N0} из {data.Count:N0} элементов за {sw.ElapsedMilliseconds} мс");
        Console.WriteLine("  Отмена кооперативная: тело цикла дорабатывает текущую итерацию, затем цикл");
        Console.WriteLine("  бросает OperationCanceledException. Для раннего выхода читают IsCancellationRequested.");
        Console.WriteLine();
    }

    static void AggregationAndComparison()
    {
        Console.WriteLine("--- 13.5. Агрегация результатов и сравнение с обычным foreach ---");
        List<int> data = Numbers(3000);

        var sw = Stopwatch.StartNew();
        long seqMax = long.MinValue;
        long seqSum = 0;
        foreach (int value in data)
        {
            long r = HeavyWork(value);
            seqSum += r;
            seqMax = Math.Max(seqMax, r);
        }

        sw.Stop();
        double seqMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        long parSum = 0;
        long parMax = long.MinValue;
        object sync = new();
        Parallel.ForEach(
            data,
            () => (Sum: 0L, Max: long.MinValue),
            (value, _, local) =>
            {
                long r = HeavyWork(value);
                return (local.Sum + r, Math.Max(local.Max, r));
            },
            local =>
            {
                lock (sync)
                {
                    parSum += local.Sum;
                    parMax = Math.Max(parMax, local.Max);
                }
            });
        sw.Stop();
        double parMs = sw.Elapsed.TotalMilliseconds;

        Console.WriteLine($"  Обычный foreach:  {seqMs,8:F0} мс, сумма {seqSum}, максимум {seqMax}");
        Console.WriteLine($"  Parallel.ForEach: {parMs,8:F0} мс, сумма {parSum}, максимум {parMax}");
        Console.WriteLine($"  Ускорение: {seqMs / parMs:F2}x, результаты совпали: {seqSum == parSum && seqMax == parMax}");
        Console.WriteLine("  Локальное состояние диапазона убирает конкуренцию: блокировка берётся один раз");
        Console.WriteLine("  на диапазон, а не на каждую итерацию.\n");
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
