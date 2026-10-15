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
        Console.WriteLine("=== ЗАДАНИЕ 5. ВАРИАНТ 3 ===");
        Console.WriteLine("Исходя из общего задания, продемонстрировать эффект от распараллеливания.");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");
        Variant3();
        Console.WriteLine();
        Console.WriteLine("=== ВАРИАНТ 3 ВЫПОЛНЕН ===");
        Pause();
    }
    static void MultiplySequential(double[,] a, double[,] b, double[,] result)
    {
        int n = a.GetLength(0);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                double acc = 0;
                for (int k = 0; k < n; k++)
                {
                    acc += a[i, k] * b[k, j];
                }

                result[i, j] = acc;
            }
        }
    }

    static void MultiplyParallel(double[,] a, double[,] b, double[,] result)
    {
        int n = a.GetLength(0);
        Parallel.For(0, n, i =>
        {
            for (int j = 0; j < n; j++)
            {
                double acc = 0;
                for (int k = 0; k < n; k++)
                {
                    acc += a[i, k] * b[k, j];
                }

                result[i, j] = acc;
            }
        });
    }

    static void TransposeSequential(double[,] a, double[,] t)
    {
        int n = a.GetLength(0);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                t[j, i] = a[i, j];
            }
        }
    }

    static void TransposeParallel(double[,] a, double[,] t)
    {
        int n = a.GetLength(0);
        Parallel.For(0, n, i =>
        {
            for (int j = 0; j < n; j++)
            {
                t[j, i] = a[i, j];
            }
        });
    }

    static bool Same(double[,] x, double[,] y)
    {
        for (int i = 0; i < x.GetLength(0); i++)
        {
            for (int j = 0; j < x.GetLength(1); j++)
            {
                if (Math.Abs(x[i, j] - y[i, j]) > 1e-6)
                {
                    return false;
                }
            }
        }

        return true;
    }

    static void Variant3()
    {
        Console.WriteLine("--- Вариант 3. Матричные вычисления ---");
        Console.WriteLine("  Умножение и транспонирование матриц 600x600, заполненных по формуле.\n");

        const int size = 600;
        double[,] a = new double[size, size];
        double[,] b = new double[size, size];
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                a[i, j] = (i + 1) * 0.5 + (j % 7);
                b[i, j] = (j + 1) * 0.25 - (i % 5);
            }
        }

        double[,] seq = new double[size, size];
        double seqMulMs = Time(() => MultiplySequential(a, b, seq));
        double[,] par = new double[size, size];
        double parMulMs = Time(() => MultiplyParallel(a, b, par));

        bool same = Same(seq, par);
        Report("Умножение матриц", seqMulMs, parMulMs, same);

        double[,] seqT = new double[size, size];
        double seqTrMs = Time(() => TransposeSequential(a, seqT));
        double[,] parT = new double[size, size];
        double parTrMs = Time(() => TransposeParallel(a, parT));
        Report("Транспонирование", seqTrMs, parTrMs, Same(seqT, parT));

        Console.WriteLine($"  Элемент [0,0] результата: {par[0, 0]:F4}, транспонированная A[0,0]: {parT[0, 0]:F4}");
        Console.WriteLine("  Приём: строки результата независимы, поэтому Parallel.For по строкам даёт");
        Console.WriteLine("  почти линейное ускорение. Строки матрицы хранятся построчно, что важно для кэша.\n");
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
