using System.Collections.Concurrent;
using System.Diagnostics;

namespace Lab5Task12;

static class Program
{
    enum LockMode
    {
        None,
        PerSegment,
        PerPrime
    }

    const int LightSize = 8_000_000;
    const int PrimeLimit = 20_000_000;
    const int ListSize = 1_000_000;

    static readonly double[] Data = MakeData(LightSize);

    static void Main(string[] args)
    {
        Console.WriteLine("=== ЗАДАНИЕ 12. PARALLEL.FOR И СИНХРОНИЗАЦИЯ ОБЩЕГО РЕСУРСА ===");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");

        SequentialBaseline();
        ParallelWithLocalState();
        SharedResourceWithoutLock();
        SharedResourceWithLock();
        Compare();
        ArrayAndListExamples();

        Console.WriteLine("=== ЗАДАНИЕ 12 ВЫПОЛНЕНО ===");
        Pause();
    }

    static double[] MakeData(int n)
    {
        Random rnd = new(12345);
        double[] data = new double[n];
        for (int i = 0; i < n; i++)
        {
            data[i] = rnd.NextDouble() * 1000.0;
        }

        return data;
    }

    static (double Sum, double Min, double Max, double Avg) Sequential(double[] data)
    {
        double sum = 0;
        double min = double.MaxValue;
        double max = double.MinValue;

        for (int i = 0; i < data.Length; i++)
        {
            double v = data[i];
            sum += v;
            if (v < min)
            {
                min = v;
            }

            if (v > max)
            {
                max = v;
            }
        }

        return (sum, min, max, sum / data.Length);
    }

    static (double Sum, double Min, double Max, double Avg) ParallelWithState(double[] data)
    {
        ConcurrentBag<(double Sum, double Min, double Max)> parts = new();

        Parallel.For(
            0,
            data.Length,
            () => (Sum: 0.0, Min: double.MaxValue, Max: double.MinValue),
            (i, _, state) => (state.Sum + data[i], Math.Min(state.Min, data[i]), Math.Max(state.Max, data[i])),
            state => parts.Add(state));

        double sum = 0;
        double min = double.MaxValue;
        double max = double.MinValue;
        foreach ((double s, double mn, double mx) in parts)
        {
            sum += s;
            min = Math.Min(min, mn);
            max = Math.Max(max, mx);
        }

        return (sum, min, max, sum / data.Length);
    }

    static void Print(string label, (double Sum, double Min, double Max, double Avg) r)
    {
        Console.WriteLine($"  {label,-28} сумма: {r.Sum,20:F2}  мин: {r.Min:F4}  макс: {r.Max:F4}  среднее: {r.Avg:F4}");
    }

    static void SequentialBaseline()
    {
        Console.WriteLine("--- 12.1. Последовательное вычисление в одном потоке ---");
        Console.WriteLine($"  Один массив данных на {LightSize:N0} чисел используется всеми пунктами этого задания,");
        Console.WriteLine("  поэтому результаты можно сравнивать напрямую.\n");

        var sw = Stopwatch.StartNew();
        var seq = Sequential(Data);
        sw.Stop();
        double seqMs = sw.Elapsed.TotalMilliseconds;
        Print("Последовательно:", seq);
        Console.WriteLine($"  Время: {seqMs:F0} мс\n");
    }

    static void ParallelWithLocalState()
    {
        Console.WriteLine("--- 12.2. Параллельное вычисление с локальным состоянием диапазона ---");
        Console.WriteLine("  Перегрузка Parallel.For с localInit и localFinally даёт каждому диапазону");
        Console.WriteLine("  собственное состояние. Блокировок в горячем цикле нет вовсе, а сведение");
        Console.WriteLine("  итогов выполняется один раз после завершения цикла.\n");

        var sw = Stopwatch.StartNew();
        var par = ParallelWithState(Data);
        sw.Stop();
        Print("Parallel.For:", par);

        var seq = Sequential(Data);
        bool same = Math.Abs(par.Sum - seq.Sum) < 0.01 && Math.Abs(par.Avg - seq.Avg) < 0.0001;
        Console.WriteLine($"  Время: {sw.Elapsed.TotalMilliseconds:F0} мс");
        Console.WriteLine($"  Результат совпал с последовательным расчётом: {same}");
        Console.WriteLine();
        Console.WriteLine("  ВНИМАНИЕ: если объявить sum просто снаружи лямбды, а не в localInit,");
        Console.WriteLine("  все потоки будут писать в одну переменную и результат окажется неверным —");
        Console.WriteLine("  это показано в следующем пункте.\n");
    }

    static void SharedResourceWithoutLock()
    {
        Console.WriteLine("--- 12.3. Общий ресурс БЕЗ синхронизации: потеря обновлений ---");
        Console.WriteLine($"  Сложим числа от 0 до {LightSize:N0} напрямую в общую ячейку массива.");

        int iterations = LightSize;
        long[] shared = new long[1];
        var sw = Stopwatch.StartNew();
        Parallel.For(0, iterations, i => { shared[0] += i; });
        sw.Stop();

        long expected = (long)iterations * (iterations - 1) / 2;
        Console.WriteLine($"  Время: {sw.Elapsed.TotalMilliseconds:F0} мс");
        Console.WriteLine($"  Ожидаемая сумма: {expected}");
        Console.WriteLine($"  Полученная сумма: {shared[0]}");
        Console.WriteLine($"  Ошибка: {expected - shared[0]} ({(expected - shared[0]) * 100.0 / expected:F2}%)");
        Console.WriteLine("  Причина: shared[0] += i — это чтение, сложение и запись в три отдельных шага.");
        Console.WriteLine("  Между чтением и записью успевает вклиниться другой поток, и часть слагаемых теряется.");
        Console.WriteLine("  Parallel.For не защищает общие переменные: это остаётся заботой разработчика.\n");
    }

    static void SharedResourceWithLock()
    {
        Console.WriteLine("--- 12.4. Общий ресурс С синхронизацией ---");
        int iterations = LightSize;
        long expected = (long)iterations * (iterations - 1) / 2;

        object sync = new();
        long[] withLock = new long[1];
        var sw = Stopwatch.StartNew();
        Parallel.For(0, iterations, i =>
        {
            lock (sync)
            {
                withLock[0] += i;
            }
        });
        sw.Stop();
        Console.WriteLine($"  lock на весь расчёт:           {sw.Elapsed.TotalMilliseconds,6:F0} мс, сумма {withLock[0]}, ошибка {expected - withLock[0]}");

        long interlocked = 0;
        sw.Restart();
        Parallel.For(0, iterations, i => Interlocked.Add(ref interlocked, i));
        sw.Stop();
        Console.WriteLine($"  Interlocked.Add:               {sw.Elapsed.TotalMilliseconds,6:F0} мс, сумма {interlocked}, ошибка {expected - interlocked}");

        long viaLocal = 0;
        sw.Restart();
        Parallel.For(
            0,
            iterations,
            () => 0L,
            (i, _, state) => state + i,
            local => Interlocked.Add(ref viaLocal, local));
        sw.Stop();
        Console.WriteLine($"  Локальный аккумулятор + сведение:{sw.Elapsed.TotalMilliseconds,6:F0} мс, сумма {viaLocal}, ошибка {expected - viaLocal}");

        Console.WriteLine();
        Console.WriteLine("  Вывод: lock корректен, но сериализует всё вычисление и убивает параллелизм.");
        Console.WriteLine("  Interlocked корректен и дешевле lock, но всё равно создаёт точку конкуренции.");
        Console.WriteLine("  Лучший вариант — вообще не трогать общий ресурс из тела цикла: накапливать");
        Console.WriteLine("  в локальном состоянии диапазона и свести результаты один раз после цикла.\n");
    }

    static void Compare()
    {
        Console.WriteLine("--- 12.5. Сравнение: последовательно, параллельно, параллельно с lock ---");
        Console.WriteLine($"  Работа: подсчёт простых чисел до {PrimeLimit:N0} методом сегментированного решета.");
        Console.WriteLine("  Первый запуск делаем без замера, чтобы прогреть JIT и честно сравнивать.");
        Console.WriteLine();

        int expected = SequentialPrimeCount(PrimeLimit);

        (int count, double ms) seq = Measure(() => SequentialPrimeCount(PrimeLimit));
        (int count, double ms) par = Measure(() => ParallelPrimeCount(PrimeLimit, LockMode.None));
        (int count, double ms) cold = Measure(() => ParallelPrimeCount(PrimeLimit, LockMode.PerSegment));
        (int count, double ms) hot = Measure(() => ParallelPrimeCount(PrimeLimit, LockMode.PerPrime));

        Console.WriteLine($"  {"Вариант",-44}{"Простых",10}{"Время, мс",12}{"Ускорение",12}");
        Console.WriteLine($"  {new string('-', 78)}");
        Console.WriteLine($"  {"Последовательное решето",-44}{seq.count,10}{seq.ms,12:F0}{"1.00x",12}");
        Console.WriteLine($"  {"Parallel.For, сведение после цикла",-44}{par.count,10}{par.ms,12:F0}{seq.ms / par.ms,11:F2}x");
        Console.WriteLine($"  {"Parallel.For, lock раз на сегмент",-44}{cold.count,10}{cold.ms,12:F0}{seq.ms / cold.ms,11:F2}x");
        Console.WriteLine($"  {"Parallel.For, lock на каждый простой",-44}{hot.count,10}{hot.ms,12:F0}{seq.ms / hot.ms,11:F2}x");
        Console.WriteLine($"  {new string('-', 78)}");
        bool allCorrect = seq.count == expected && par.count == expected && cold.count == expected && hot.count == expected;
        Console.WriteLine($"  Верный ответ: {expected:N0}. Все варианты сошлись: {allCorrect}");
        Console.WriteLine();

        if (allCorrect)
        {
            Console.WriteLine($"  Лучший результат: сведение после цикла, {seq.ms / par.ms:F2}x относительно последовательного расчёта.");
        }

        if (hot.ms > par.ms)
        {
            Console.WriteLine($"  Замок, взятый на каждый найденный элемент, в {(hot.ms / par.ms):F1} раза медленнее");
            Console.WriteLine("  варианта со сведением после цикла: это горячий замок, все потоки стоят в очереди.");
        }
        else
        {
            Console.WriteLine("  Замок на каждый элемент не дал заметного проигрыша — элементов слишком мало.");
        }

        if (cold.ms <= hot.ms)
        {
            Console.WriteLine($"  Замок раз на сегмент ({cold.ms:F0} мс) дешевле, чем на каждый элемент ({hot.ms:F0} мс):");
            Console.WriteLine("  дорога не сам lock, а частота его взятия. Чем реже он берётся, тем меньше вредит.");
        }

        Console.WriteLine("  Общий вывод: выигрыш даёт не сам Parallel.For, а распараллеливание без общих ресурсов.");
        Console.WriteLine();
    }

    static (int Count, double Ms) Measure(Func<int> run)
    {
        var sw = Stopwatch.StartNew();
        int count = run();
        sw.Stop();
        return (count, sw.Elapsed.TotalMilliseconds);
    }

    static int[] BasePrimes(int limit)
    {
        bool[] sieve = new bool[limit + 1];
        List<int> primes = new();
        for (int i = 2; i <= limit; i++)
        {
            if (sieve[i])
            {
                continue;
            }

            primes.Add(i);
            for (long j = (long)i * i; j <= limit; j += i)
            {
                sieve[j] = true;
            }
        }

        return primes.ToArray();
    }

    static int SequentialPrimeCount(int limit)
    {
        bool[] sieve = new bool[limit + 1];
        int count = 0;
        for (int i = 2; i <= limit; i++)
        {
            if (sieve[i])
            {
                continue;
            }

            count++;
            for (long j = (long)i * i; j <= limit; j += i)
            {
                sieve[j] = true;
            }
        }

        return count;
    }

    static int ParallelPrimeCount(int limit, LockMode mode)
    {
        int[] basePrimes = BasePrimes((int)Math.Sqrt(limit) + 1);
        int segmentSize = 1_000_000;
        int segments = (limit / segmentSize) + 1;
        object sync = new();
        int shared = 0;
        int[] perSegment = new int[segments];

        Parallel.For(0, segments, s =>
        {
            int lo = s * segmentSize + 1;
            int hi = Math.Min(lo + segmentSize - 1, limit);
            if (lo < 2)
            {
                lo = 2;
            }

            if (hi < lo)
            {
                return;
            }

            bool[] segment = new bool[hi - lo + 1];
            foreach (int p in basePrimes)
            {
                long start = Math.Max((long)p * p, ((lo + p - 1) / (long)p) * p);
                for (long j = start; j <= hi; j += p)
                {
                    segment[j - lo] = true;
                }
            }

            int found = 0;
            for (int i = 0; i < segment.Length; i++)
            {
                if (!segment[i])
                {
                    found++;
                }
            }

            if (mode == LockMode.PerSegment)
            {
                lock (sync)
                {
                    shared += found;
                }
            }
            else if (mode == LockMode.PerPrime)
            {
                for (int k = 0; k < found; k++)
                {
                    lock (sync)
                    {
                        shared++;
                    }
                }
            }
            else
            {
                perSegment[s] = found;
            }
        });

        return mode == LockMode.None ? perSegment.Sum() : shared;
    }

    static void ArrayAndListExamples()
    {
        Console.WriteLine("--- 12.6. Примеры с Array и List как общим ресурсом ---");

        List<int> list = new(ListSize);
        Console.WriteLine($"  List<int> без блокировки, {ListSize:N0} добавлений:");
        try
        {
            Parallel.For(0, ListSize, i => list.Add(i));
            Console.WriteLine($"    Добавлено {list.Count:N0} из {ListSize:N0}. {(list.Count == ListSize ? "Счётчик совпал, но поведение всё равно не гарантировано спецификацией." : "Часть данных потеряна — это недопустимо.")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Исключение: {ex.GetType().Name}. Список повреждён, его нельзя использовать дальше.");
        }

        object sync = new();
        list = new List<int>(ListSize);
        var sw = Stopwatch.StartNew();
        Parallel.For(0, ListSize, i =>
        {
            lock (sync)
            {
                list.Add(i);
            }
        });
        sw.Stop();
        Console.WriteLine($"    С lock: {list.Count:N0} элементов, {sw.Elapsed.TotalMilliseconds:F0} мс — корректно, но потоки выстроены в очередь.");

        int[] counter = new int[1];
        sw.Restart();
        Parallel.For(0, ListSize, i => Interlocked.Increment(ref counter[0]));
        sw.Stop();
        Console.WriteLine($"    Счётчик в array[0] через Interlocked.Increment: {counter[0]:N0}, {sw.Elapsed.TotalMilliseconds:F0} мс — корректно.");
        Console.WriteLine();
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
