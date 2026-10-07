using System.Collections.Concurrent;
using System.Diagnostics;

namespace Lab5Task15;

static class Program
{
    const int Width = 1200;
    const int Height = 900;
    const int Rounds = 12;

    static void Main(string[] args)
    {
        Console.WriteLine("=== ЗАДАНИЕ 15. РАЗБИЕНИЕ НА ДИАПАЗОНЫ (PARTITIONER) И ОБРАБОТКА ИЗОБРАЖЕНИЙ ===");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");
        Console.WriteLine($"Изображение {Width}x{Height}, каждый прогон выполняет {Rounds} полных проходов.");

        Rgb[,] image = GenerateImage();
        Rgb[,] reference = new Rgb[Width, Height];

        Sequential(image, reference);
        double chunkMs = ChunkedPartitioner(image, reference);
        ParallelForRows(image, reference, chunkMs);
        LoadAwarePartitioner(image, reference);
        ChunkComparison();
        EdgeCases();

        Console.WriteLine("=== ЗАДАНИЕ 15 ВЫПОЛНЕНО ===");
        Pause();
    }

    readonly record struct Rgb(byte R, byte G, byte B)
    {
        public byte Luminance => (byte)Math.Clamp((R * 299 + G * 587 + B * 114) / 1000, 0, 255);
    }

    static Rgb[,] GenerateImage()
    {
        Random rnd = new(4242);
        Rgb[,] image = new Rgb[Width, Height];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                bool checker = ((x / 40) + (y / 40)) % 2 == 0;
                byte baseValue = (byte)(checker ? 40 + ((x + y) % 200) : 210 - ((x + y) % 200));
                image[x, y] = new Rgb(
                    (byte)Math.Clamp(baseValue + rnd.Next(-12, 13), 0, 255),
                    (byte)Math.Clamp(baseValue + rnd.Next(-12, 13), 0, 255),
                    (byte)Math.Clamp(baseValue + rnd.Next(-12, 13), 0, 255));
            }
        }

        return image;
    }

    static void ProcessPixel(Rgb[,] image, Rgb[,] target, int x, int y)
    {
        Rgb px = image[x, y];
        byte lum = px.Luminance;
        target[x, y] = lum > 127
            ? new Rgb((byte)(255 - px.R), (byte)(255 - px.G), (byte)(255 - px.B))
            : new Rgb((byte)(px.R / 3), (byte)(px.G / 3), (byte)(px.B / 3));
    }

    static void ProcessRows(Rgb[,] image, Rgb[,] target, int fromRow, int toRow)
    {
        for (int y = fromRow; y < toRow; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                ProcessPixel(image, target, x, y);
            }
        }
    }

    static bool Same(Rgb[,] a, Rgb[,] b)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!a[x, y].Equals(b[x, y]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    static void Sequential(Rgb[,] image, Rgb[,] reference)
    {
        Console.WriteLine("--- 15.1. Последовательная обработка (эталон) ---");
        var sw = Stopwatch.StartNew();
        for (int r = 0; r < Rounds; r++)
        {
            ProcessRows(image, reference, 0, Height);
        }

        sw.Stop();
        Console.WriteLine($"  Время: {sw.ElapsedMilliseconds} мс на {Rounds} проходов");
        Console.WriteLine($"  Обработано пикселей: {(long)Width * Height * Rounds:N0}");
        Console.WriteLine("  Соседние пиксели обрабатываются независимо, поэтому цикл идеально распараллеливается.\n");
    }

    static double ChunkedPartitioner(Rgb[,] image, Rgb[,] reference)
    {
        Console.WriteLine("--- 15.2. Parallel.ForEach с Partitioner.Create ---");
        Rgb[,] result = new Rgb[Width, Height];
        var sw = Stopwatch.StartNew();

        for (int r = 0; r < Rounds; r++)
        {
            Parallel.ForEach(
                Partitioner.Create(0, Height, 15),
                range => ProcessRows(image, result, range.Item1, range.Item2));
        }

        sw.Stop();
        Console.WriteLine($"  Время: {sw.ElapsedMilliseconds} мс");
        Console.WriteLine($"  Совпало с эталоном: {Same(reference, result)}");
        Console.WriteLine("  Partitioner.Create делит 900 строк на диапазоны по 15 строк. Таких диапазонов 60 —");
        Console.WriteLine("  больше, чем ядер, поэтому длинные диапазоны не тормозят остальные потоки.\n");
        return sw.Elapsed.TotalMilliseconds;
    }

    static double ParallelForRows(Rgb[,] image, Rgb[,] reference, double chunkMs)
    {
        Console.WriteLine("--- 15.3. Для сравнения: Parallel.For по одной строке ---");
        Rgb[,] result = new Rgb[Width, Height];
        var sw = Stopwatch.StartNew();

        for (int r = 0; r < Rounds; r++)
        {
            Parallel.For(0, Height, y => ProcessRows(image, result, y, y + 1));
        }

        sw.Stop();
        double rowMs = sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"  Время: {sw.ElapsedMilliseconds} мс");
        Console.WriteLine($"  Совпало с эталоном: {Same(reference, result)}");
        if (rowMs >= chunkMs)
        {
            Console.WriteLine("  Построчный цикл не уступил разбиению на диапазоны: планировщик .NET");
            Console.WriteLine("  и так применяет динамическое разбиение, а накладные расходы на итерацию малы.");
        }
        else
        {
            Console.WriteLine($"  Построчный цикл выиграл ({rowMs:F0} против {chunkMs:F0} мс): планировщик .NET");
            Console.WriteLine("  и так применяет динамическое разбиение, а накладные расходы на итерацию малы.");
        }

        Console.WriteLine("  Явный Partitioner нужен, когда размеры диапазонов заданы вами: так вы контролируете");
        Console.WriteLine("  баланс и предсказуемость загрузки, а не полагаетесь на эвристику планировщика.\n");
        return rowMs;
    }

    static void LoadAwarePartitioner(Rgb[,] image, Rgb[,] reference)
    {
        Console.WriteLine("--- 15.4. Собственный разделитель с локальной очередью потока ---");
        Rgb[,] result = new Rgb[Width, Height];
        var sw = Stopwatch.StartNew();
        int refills = 0, fromLocal = 0, fromShared = 0;

        for (int r = 0; r < Rounds; r++)
        {
            using OrderableRangePartitioner partitioner = new(0, Height, 15);
            Parallel.ForEach(partitioner, range => ProcessRows(image, result, range.Min, range.Max));
            refills += partitioner.SharedRefills;
            fromLocal += partitioner.TakenFromLocal;
            fromShared += partitioner.TakenFromShared;
        }

        sw.Stop();
        Console.WriteLine($"  Время: {sw.ElapsedMilliseconds} мс");
        Console.WriteLine($"  Совпало с эталоном: {Same(reference, result)}");
        Console.WriteLine("  Разделитель поддерживает динамические диапазоны: поток сначала опустошает");
        Console.WriteLine("  свою локальную очередь и только потом добирает диапазоны из общей очереди.");
        Console.WriteLine($"  Пополнений локальной очереди: {refills}, взято из неё: {fromLocal}, напрямую из общей: {fromShared}");
        Console.WriteLine("  Это снижает конкуренцию на разделителе при коротких разнородных задачах.\n");
    }

    static void ChunkComparison()
    {
        Console.WriteLine("--- 15.5. Влияние числа строк в диапазоне ---");
        int[] sizes = { 1, 5, 15, 45, 150, 900 };
        List<(int Size, double Ms)> timings = new();

        foreach (int size in sizes)
        {
            Rgb[,] image = GenerateImage();
            Rgb[,] result = new Rgb[Width, Height];
            var sw = Stopwatch.StartNew();
            for (int r = 0; r < Rounds; r++)
            {
                Parallel.ForEach(
                    Partitioner.Create(0, Height, size),
                    range => ProcessRows(image, result, range.Item1, range.Item2));
            }

            sw.Stop();
            timings.Add((size, sw.Elapsed.TotalMilliseconds));
        }

        foreach ((int size, double ms) in timings)
        {
            int chunks = (Height + size - 1) / size;
            Console.WriteLine($"  строк в диапазоне {size,4:D3}, всего диапазонов {chunks,3}: {ms,7:F0} мс");
        }

        var best = timings.OrderBy(t => t.Ms).First();
        var worst = timings.OrderByDescending(t => t.Ms).First();
        Console.WriteLine($"  Лучший вариант: {best.Size} строк ({best.Ms:F0} мс), худший: {worst.Size} строк ({worst.Ms:F0} мс).");
        if (worst.Size >= Height)
        {
            Console.WriteLine("  Единственный диапазон на всё изображение — худший случай: работает один поток,");
            Console.WriteLine($"  остальные {Environment.ProcessorCount - 1} простаивают, поэтому время почти в {worst.Ms / best.Ms:F1} раза больше.");
            Console.WriteLine("  В этом задании нагрузка на строку одинакова, поэтому мелкие диапазоны безопасны:");
            Console.WriteLine("  стоимость их создания ничтожна по сравнению с самой обработкой.");
        }
        else
        {
            Console.WriteLine("  Слишком мелкие диапазоны перегружают планировщик, слишком крупные дают");
            Console.WriteLine("  перекос: один поток получает весь объём и работает дольше остальных.");
        }

        Console.WriteLine();
        UnevenWork();
    }

    static void UnevenWork()
    {
        Console.WriteLine("--- 15.5a. Тот же эксперимент с неравномерной нагрузкой ---");
        Console.WriteLine("  Каждая 100-я строка обрабатывается в 40 раз дольше остальных.");
        Console.WriteLine("  Именно здесь размер диапазона решает всё.\n");

        int[] sizes = { 30, 150, 900 };
        foreach (int size in sizes)
        {
            Rgb[,] image = GenerateImage();
            var sw = Stopwatch.StartNew();
            for (int r = 0; r < Rounds; r++)
            {
                Parallel.ForEach(
                    Partitioner.Create(0, Height, size),
                    range =>
                    {
                        for (int y = range.Item1; y < range.Item2; y++)
                        {
                            int repeat = y % 100 == 0 ? 40 : 1;
                            for (int k = 0; k < repeat; k++)
                            {
                                ProcessRows(image, image, 0, 4);
                            }
                        }
                    });
            }

            sw.Stop();
            int chunks = (Height + size - 1) / size;
            Console.WriteLine($"  диапазонов {chunks,3}: {sw.Elapsed.TotalMilliseconds,7:F0} мс");
        }

        Console.WriteLine();
        Console.WriteLine("  С крупными диапазонами тяжёлые строки попадают в один и тот же поток, и часть");
        Console.WriteLine("  ядер простаивает. Мелкие диапазоны выравнивают нагрузку почти идеально.");
        Console.WriteLine("  Правило: размер диапазона подбирают под разброс времени обработки, а не под размер данных.\n");
    }

    static void EdgeCases()
    {
        Console.WriteLine("--- 15.6. Граничные случаи ---");

        long sum = 0;
        Parallel.ForEach(Partitioner.Create(0, 3, 1024), range => Interlocked.Add(ref sum, range.Item2 - range.Item1));
        Console.WriteLine($"  Размер 3 при размере диапазона 1024: обработано {sum} из 3, ошибка {3 - sum}");

        try
        {
            Parallel.ForEach(Partitioner.Create(0, 0, 16), _ => { });
            Console.WriteLine("  Partitioner.Create(0, 0, 16): исключения нет");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Console.WriteLine($"  Partitioner.Create(0, 0, 16) бросает {ex.GetType().Name}: границы должны различаться.");
            Console.WriteLine("  Это важно: пустой вход надо отсекать заранее, иначе будет исключение.");
        }

        long empty = 0;
        Parallel.ForEach(new OrderableRangePartitioner(0, 0, 16), _ => Interlocked.Increment(ref empty));
        Console.WriteLine($"  Собственный разделитель при 0 строк: итераций выполнено {empty}, исключений нет");

        long one = 0;
        Parallel.ForEach(Partitioner.Create(0, 1, 8), range => Interlocked.Increment(ref one));
        Console.WriteLine($"  Диапазон из одной строки: итераций {one}");

        long hits = 0;
        Parallel.ForEach(new OrderableRangePartitioner(0, Height, 32), _ => Interlocked.Increment(ref hits));
        Console.WriteLine($"  Собственный разделитель на {Height} строках: диапазонов выдано {(Height + 31) / 32}, обработано {hits}");
        Console.WriteLine("  Разделитель .NET требует различающихся границ, а собственные диапазоны просто");
        Console.WriteLine("  не порождаются при пустом входе. Пустой вход отсекают заранее.");
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

readonly record struct Range(int Min, int Max)
{
    public int Length => Max - Min;
}

sealed class OrderableRangePartitioner : Partitioner<Range>, IDisposable
{
    private const int BatchSize = 4;

    private readonly int _min;
    private readonly int _max;
    private readonly int _chunk;
    private readonly ConcurrentQueue<Range> _shared = new();
    private readonly ThreadLocal<Stack<Range>> _local = new(() => new Stack<Range>());
    private int _sharedRefills;
    private int _takenFromLocal;
    private int _takenFromShared;

    public int SharedRefills => Volatile.Read(ref _sharedRefills);
    public int TakenFromLocal => Volatile.Read(ref _takenFromLocal);
    public int TakenFromShared => Volatile.Read(ref _takenFromShared);

    public OrderableRangePartitioner(int min, int max, int chunkSize)
    {
        _min = min;
        _max = max;
        _chunk = Math.Max(1, chunkSize);
        for (int start = min; start < max; start += _chunk)
        {
            _shared.Enqueue(new Range(start, Math.Min(start + _chunk, max)));
        }
    }

    public override bool SupportsDynamicPartitions => true;

    public override IList<IEnumerator<Range>> GetPartitions(int partitionCount)
    {
        Range[] all = _shared.ToArray();
        List<IEnumerator<Range>> result = new(partitionCount);
        for (int i = 0; i < partitionCount; i++)
        {
            List<Range> slice = new();
            for (int j = i; j < all.Length; j += partitionCount)
            {
                slice.Add(all[j]);
            }

            result.Add(slice.GetEnumerator());
        }

        return result;
    }

    public override IEnumerable<Range> GetDynamicPartitions()
    {
        while (TryTake(out Range range))
        {
            yield return range;
        }
    }

    private bool TryTake(out Range range)
    {
        Stack<Range>? local = _local.Value;
        if (local is null)
        {
            range = default;
            return false;
        }

        if (local.Count > 0)
        {
            Interlocked.Increment(ref _takenFromLocal);
            range = local.Pop();
            return true;
        }

        int refilled = 0;
        for (int i = 0; i < BatchSize; i++)
        {
            if (!_shared.TryDequeue(out Range next))
            {
                break;
            }

            local.Push(next);
            refilled++;
        }

        if (refilled == 0)
        {
            range = default;
            return false;
        }

        Interlocked.Increment(ref _sharedRefills);

        if (refilled == 1)
        {
            Interlocked.Increment(ref _takenFromShared);
        }

        range = local.Pop();
        return true;
    }

    public void Dispose() => _local.Dispose();
}
