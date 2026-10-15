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
        Console.WriteLine("=== ЗАДАНИЕ 5. ВАРИАНТ 7 ===");
        Console.WriteLine("Исходя из общего задания, продемонстрировать эффект от распараллеливания.");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");
        Variant7();
        Console.WriteLine();
        Console.WriteLine("=== ВАРИАНТ 7 ВЫПОЛНЕН ===");
        Pause();
    }
    static void ParallelMergeSort(int[] a)
    {
        int cores = Math.Max(1, Environment.ProcessorCount);
        int chunkSize = (a.Length + cores - 1) / cores;
        int chunkCount = (a.Length + chunkSize - 1) / chunkSize;

        Parallel.For(0, chunkCount, c =>
        {
            int from = c * chunkSize;
            int to = Math.Min(from + chunkSize, a.Length);
            Array.Sort(a, from, to - from, Comparer<int>.Default);
        });

        int[] buffer = new int[a.Length];
        for (int width = chunkSize; width < a.Length; width *= 2)
        {
            for (int start = 0; start < a.Length; start += width * 2)
            {
                int mid = Math.Min(start + width, a.Length);
                int end = Math.Min(start + width * 2, a.Length);
                Merge(a, buffer, start, mid, end);
                Array.Copy(buffer, start, a, start, end - start);
            }
        }
    }

    static void Merge(int[] a, int[] buffer, int left, int mid, int right)
    {
        int i = left;
        int j = mid;
        for (int k = left; k < right; k++)
        {
            if (i < mid && (j >= right || a[i] <= a[j]))
            {
                buffer[k] = a[i++];
            }
            else
            {
                buffer[k] = a[j++];
            }
        }
    }

    static int FindFirstParallel(int[] a, int target)
    {
        int cores = Environment.ProcessorCount;
        int[] found = new int[cores];
        Array.Fill(found, -1);

        Parallel.For(0, cores, c =>
        {
            int from = (int)((long)a.Length * c / cores);
            int to = (int)((long)a.Length * (c + 1) / cores);
            found[c] = Array.IndexOf(a, target, from, to - from);
        });

        int best = -1;
        for (int c = 0; c < cores; c++)
        {
            if (found[c] >= 0 && (best < 0 || found[c] < best))
            {
                best = found[c];
            }
        }

        return best;
    }

    static int QuickSelect(int[] a, int k)
    {
        int[] copy = (int[])a.Clone();
        int left = 0;
        int right = copy.Length - 1;
        while (left < right)
        {
            int pivotIndex = Partition(copy, left, right);
            if (pivotIndex == k)
            {
                return copy[k];
            }

            if (pivotIndex < k)
            {
                left = pivotIndex + 1;
            }
            else
            {
                right = pivotIndex - 1;
            }
        }

        return copy[left];
    }

    static int Partition(int[] a, int left, int right)
    {
        int pivot = a[(left + right) / 2];
        int i = left;
        int j = right;
        while (i <= j)
        {
            while (a[i] < pivot)
            {
                i++;
            }

            while (a[j] > pivot)
            {
                j--;
            }

            if (i <= j)
            {
                (a[i], a[j]) = (a[j], a[i]);
                i++;
                j--;
            }
        }

        return i - 1;
    }

    static void Variant7()
    {
        Console.WriteLine("--- Вариант 7. Сортировка и поиск ---");
        Console.WriteLine("  Сортировка 5 000 000 чисел и параллельный поиск первого вхождения.\n");

        const int n = 5_000_000;
        int[] data = new int[n];
        Random rnd = new(13);
        for (int i = 0; i < n; i++)
        {
            data[i] = rnd.Next(0, 10_000_000);
        }

        int[] std = (int[])data.Clone();
        double stdMs = Time(() => Array.Sort(std));

        int[] custom = (int[])data.Clone();
        double customMs = Time(() => ParallelMergeSort(custom));

        Report("Array.Sort против параллельной сортировки слиянием", stdMs, customMs, std.SequenceEqual(custom));

        int[] second = (int[])data.Clone();
        double stdMs2 = Time(() => Array.Sort(second, Comparer<int>.Default));
        int[] custom2 = (int[])data.Clone();
        double customMs2 = Time(() => ParallelMergeSort(custom2));
        Report("То же с компаратором", stdMs2, customMs2, second.SequenceEqual(custom2));

        int target = std[n / 2];
        int seqIndex = 0;
        double sMs = Time(() => seqIndex = Array.IndexOf(std, target));
        int parIndex = 0;
        double pMs = Time(() => parIndex = FindFirstParallel(std, target));
        Report($"Поиск первого вхождения (индекс {seqIndex})", sMs, pMs, seqIndex == parIndex);

        double sortMs = 0;
        int q = 0;
        double qMs = Time(() => q = QuickSelect(std, n / 2));
        sortMs = stdMs;
        Console.WriteLine($"  QuickSelect нашёл {q}-й элемент {q} за {qMs:F1} мс, полная сортировка заняла {sortMs:F1} мс");
        Console.WriteLine($"  Медиана массива: {std[n / 2]}, QuickSelect вернул то же значение: {q == std[n / 2]}");
        Console.WriteLine("  Приём: массив делится на блоки по числу ядер, каждый сортируется потоком,");
        Console.WriteLine("  затем блоки сливаются попарно. Встроенный Array.Sort уже делает нечто подобное");
        Console.WriteLine("  внутри, поэтому самописная версия редко его обгоняет.\n");
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
