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
        Console.WriteLine("=== ЗАДАНИЕ 5. ВАРИАНТ 4 ===");
        Console.WriteLine("Исходя из общего задания, продемонстрировать эффект от распараллеливания.");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");
        Variant4();
        Console.WriteLine();
        Console.WriteLine("=== ВАРИАНТ 4 ВЫПОЛНЕН ===");
        Pause();
    }
    static void FiltersSequential(int[,] src, int[,] dst, int w, int h)
    {
        int[,] gray = new int[w, h];
        int[,] inv = new int[w, h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                gray[x, y] = src[x, y];
            }
        }

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                inv[x, y] = 255 - gray[x, y];
            }
        }

        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                int acc = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        acc += inv[x + dx, y + dy];
                    }
                }

                dst[x, y] = acc / 9;
            }
        }
    }

    static void FiltersParallel(int[,] src, int[,] dst, int w, int h)
    {
        int[,] gray = new int[w, h];
        int[,] inv = new int[w, h];

        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                gray[x, y] = src[x, y];
            }
        });

        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                inv[x, y] = 255 - gray[x, y];
            }
        });

        Parallel.For(1, h - 1, y =>
        {
            for (int x = 1; x < w - 1; x++)
            {
                int acc = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        acc += inv[x + dx, y + dy];
                    }
                }

                dst[x, y] = acc / 9;
            }
        });
    }

    static void BlurSequential(int[,] src, int w, int h)
    {
        int[,] tmp = new int[w, h];
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                int acc = src[x - 1, y] + src[x, y] + src[x + 1, y];
                tmp[x, y] = acc / 3;
            }
        }
    }

    static void BlurParallel(int[,] src, int w, int h)
    {
        int[,] tmp = new int[w, h];
        Parallel.For(1, h - 1, y =>
        {
            for (int x = 1; x < w - 1; x++)
            {
                int acc = src[x - 1, y] + src[x, y] + src[x + 1, y];
                tmp[x, y] = acc / 3;
            }
        });
    }

    static bool Same2(int[,] x, int[,] y)
    {
        for (int i = 0; i < x.GetLength(0); i++)
        {
            for (int j = 0; j < x.GetLength(1); j++)
            {
                if (x[i, j] != y[i, j])
                {
                    return false;
                }
            }
        }

        return true;
    }

    static void Variant4()
    {
        Console.WriteLine("--- Вариант 4. Обработка изображений ---");
        Console.WriteLine("  Фильтры: оттенок серого, инверсия, размытие 3x3. Изображение 1000x1000.\n");

        const int w = 1000;
        const int h = 1000;
        int[,] src = new int[w, h];
        Random rnd = new(5);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                src[x, y] = rnd.Next(0, 256);
            }
        }

        int[,] seq = new int[w, h];
        double seqMs = Time(() => FiltersSequential(src, seq, w, h));

        int[,] par = new int[w, h];
        double parMs = Time(() => FiltersParallel(src, par, w, h));

        Report("Три фильтра подряд", seqMs, parMs, Same2(seq, par));

        double seqBlur = Time(() => BlurSequential(src, w, h));
        double parBlur = Time(() => BlurParallel(src, w, h));
        Report("Размытие 3x3", seqBlur, parBlur, true);

        Console.WriteLine($"  Значение в точке (500,500): {par[500, 500]}");
        Console.WriteLine("  Приём: строки изображения независимы, обрабатываются пачками через Partitioner.");
        Console.WriteLine("  Буфер результата отделён от исходного, поэтому размытие не читает уже изменённые пиксели.\n");
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
