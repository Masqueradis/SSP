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
        Console.WriteLine("=== ЗАДАНИЕ 5. ВАРИАНТ 1 ===");
        Console.WriteLine("Исходя из общего задания, продемонстрировать эффект от распараллеливания.");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");
        Variant1();
        Console.WriteLine();
        Console.WriteLine("=== ВАРИАНТ 1 ВЫПОЛНЕН ===");
        Pause();
    }
    static void Variant1()
    {
        Console.WriteLine("--- Вариант 1. Обработка больших массивов данных ---");
        Console.WriteLine("  Суммирование, поиск максимума и минимума на массиве из 40 000 000 чисел.\n");

        const int n = 40_000_000;
        double[] data = new double[n];
        Random rnd = new(7);
        for (int i = 0; i < n; i++)
        {
            data[i] = rnd.NextDouble() * 1000.0;
        }

        double seqSum = 0, seqMin = double.MaxValue, seqMax = double.MinValue;
        double seqMs = Time(() =>
        {
            for (int i = 0; i < n; i++)
            {
                double v = data[i];
                seqSum += v;
                seqMin = Math.Min(seqMin, v);
                seqMax = Math.Max(seqMax, v);
            }
        });

        double parSum = 0, parMin = double.MaxValue, parMax = double.MinValue;
        double parMs = Time(() =>
        {
            double[] sums = new double[Environment.ProcessorCount];
            double[] mins = new double[Environment.ProcessorCount];
            double[] maxs = new double[Environment.ProcessorCount];
            Array.Fill(mins, double.MaxValue);
            Array.Fill(maxs, double.MinValue);

            int cores = Environment.ProcessorCount;
            Parallel.For(0, cores, core =>
            {
                int from = (int)((long)n * core / cores);
                int to = (int)((long)n * (core + 1) / cores);
                double s = 0, mn = double.MaxValue, mx = double.MinValue;
                for (int i = from; i < to; i++)
                {
                    double v = data[i];
                    s += v;
                    mn = Math.Min(mn, v);
                    mx = Math.Max(mx, v);
                }

                sums[core] = s;
                mins[core] = mn;
                maxs[core] = mx;
            });

            parSum = sums.Sum();
            parMin = mins.Min();
            parMax = maxs.Max();
        });

        Report("Сумма, минимум, максимум", seqMs, parMs, Close(seqSum, parSum) && seqMin == parMin && seqMax == parMax);
        Console.WriteLine($"  Сумма: {parSum:N2}, минимум: {parMin:F4}, максимум: {parMax:F4}");
        Console.WriteLine("  Приём: массив режется на фиксированные диапазоны по числу ядер, каждый поток");
        Console.WriteLine("  считает свой итог, результаты складываются после цикла. Общих переменных нет.\n");
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
