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
        Console.WriteLine("=== ЗАДАНИЕ 5. ВАРИАНТ 6 ===");
        Console.WriteLine("Исходя из общего задания, продемонстрировать эффект от распараллеливания.");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");
        Variant6();
        Console.WriteLine();
        Console.WriteLine("=== ВАРИАНТ 6 ВЫПОЛНЕН ===");
        Pause();
    }
    static double Simpson(Func<double, double> f, double a, double b, int n)
    {
        double h = (b - a) / n;
        double acc = f(a) + f(b);
        for (int i = 1; i < n; i++)
        {
            acc += (i % 2 == 0 ? 2 : 4) * f(a + i * h);
        }

        return acc * h / 3;
    }

    static double SimpsonParallel(Func<double, double> f, double a, double b, int n)
    {
        int chunks = Environment.ProcessorCount * 4;
        double h = (b - a) / n;
        int perChunk = n / chunks;
        double[] partial = new double[chunks];

        Parallel.For(0, chunks, c =>
        {
            int from = c * perChunk;
            int to = c == chunks - 1 ? n : from + perChunk;
            double acc = 0;
            for (int i = from; i < to; i++)
            {
                if (i == 0 || i == n)
                {
                    acc += f(a + i * h);
                }
                else
                {
                    acc += (i % 2 == 0 ? 2 : 4) * f(a + i * h);
                }
            }

            partial[c] = acc;
        });

        return partial.Sum() * h / 3;
    }

    static (double X, double F) Minimize(Func<double, double> f, double a, double b, int steps)
    {
        double step = (b - a) / steps;
        double bestX = a;
        double bestF = double.MaxValue;
        for (int i = 0; i <= steps; i++)
        {
            double x = a + i * step;
            double v = f(x);
            if (v < bestF)
            {
                bestF = v;
                bestX = x;
            }
        }

        return (bestX, bestF);
    }

    static (double X, double F) MinimizeParallel(Func<double, double> f, double a, double b, int steps)
    {
        int cores = Environment.ProcessorCount;
        double step = (b - a) / steps;
        double[] bestF = new double[cores];
        double[] bestX = new double[cores];
        Array.Fill(bestF, double.MaxValue);

        Parallel.For(0, cores, c =>
        {
            int from = (int)((long)steps * c / cores);
            int to = (int)((long)steps * (c + 1) / cores);
            double localF = double.MaxValue;
            double localX = a;
            for (int i = from; i <= to; i++)
            {
                double x = a + i * step;
                double v = f(x);
                if (v < localF)
                {
                    localF = v;
                    localX = x;
                }
            }

            bestF[c] = localF;
            bestX[c] = localX;
        });

        int winner = 0;
        for (int c = 1; c < cores; c++)
        {
            if (bestF[c] < bestF[winner])
            {
                winner = c;
            }
        }

        return (bestX[winner], bestF[winner]);
    }

    static double Derivative(Func<double, double> f, double x, double h, int samples)
    {
        double acc = 0;
        double delta = h / samples;
        for (int i = 0; i < samples; i++)
        {
            double p = x + i * delta;
            acc += (f(p + delta) - f(p - delta)) / (2 * delta);
        }

        return acc / samples;
    }

    static double DerivativeParallel(Func<double, double> f, double x, double h, int samples)
    {
        int chunks = Environment.ProcessorCount * 4;
        double delta = h / samples;
        int perChunk = Math.Max(1, samples / chunks);
        double[] partial = new double[chunks];

        Parallel.For(0, chunks, c =>
        {
            int from = c * perChunk;
            int to = c == chunks - 1 ? samples : Math.Min(from + perChunk, samples);
            double acc = 0;
            for (int i = from; i < to; i++)
            {
                double p = x + i * delta;
                acc += (f(p + delta) - f(p - delta)) / (2 * delta);
            }

            partial[c] = acc;
        });

        return partial.Sum() / samples;
    }

    static void Variant6()
    {
        Console.WriteLine("--- Вариант 6. Численные методы ---");
        Console.WriteLine("  Численное интегрирование методом Симпсона и поиск минимума функции.\n");

        const int n = 20_000_000;
        double seqIntegral = 0;
        double seqMs = Time(() => seqIntegral = Simpson(f => Math.Sin(f) * f + 0.5 * f * f, 0, 10, n));

        double parIntegral = 0;
        double parMs = Time(() => parIntegral = SimpsonParallel(f => Math.Sin(f) * f + 0.5 * f * f, 0, 10, n));

        Report("Интеграл Симпсона, 20 млн точек", seqMs, parMs, Close(seqIntegral, parIntegral, 1e-6));
        Console.WriteLine($"  Значение интеграла: {parIntegral:F9}");
        Console.WriteLine($"  Расхождение с последовательным расчётом: {Math.Abs(seqIntegral - parIntegral):E2}");
        Console.WriteLine("  Небольшое расхождение ожидаемо: при сложении 20 млн слагаемых порядок обхода");
        Console.WriteLine("  меняет округление, поэтому сравнение идёт по относительной погрешности.");

        (double seqX, double seqF) = (0, 0);
        double seqMinMs = Time(() => (seqX, seqF) = Minimize(f => Math.Sin(f) * 10 + (f - 3) * (f - 3) * 3, -5, 5, 4_000_000));
        (double parX, double parF) = (0, 0);
        double parMinMs = Time(() => (parX, parF) = MinimizeParallel(f => Math.Sin(f) * 10 + (f - 3) * (f - 3) * 3, -5, 5, 4_000_000));
        Report("Поиск минимума, 4 млн точек", seqMinMs, parMinMs, seqX == parX && seqF == parF);
        Console.WriteLine($"  Минимум функции: x = {parX:F6}, значение = {parF:F6}");

        double seqDeriv = 0;
        double dSeq = Time(() => seqDeriv = Derivative(f => Math.Sin(f) * f, 1.0, 2.0, 2000));
        double parDeriv = 0;
        double dPar = Time(() => parDeriv = DerivativeParallel(f => Math.Sin(f) * f, 1.0, 2.0, 2000));
        Report("Численная производная", dSeq, dPar, Close(seqDeriv, parDeriv, 1e-6));
        Console.WriteLine($"  Производная в точке 1: {parDeriv:F9}, расхождение {Math.Abs(seqDeriv - parDeriv):E2}");
        Console.WriteLine("  Приём: интеграл разбивается на чётное число интервалов, суммы складываются после.");
        Console.WriteLine("  Распараллеливать имеет смысл только тяжёлую подынтегральную функцию.\n");
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
