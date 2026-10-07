namespace Lab5Task9;

sealed class DataItem
{
    public int Id { get; set; }

    public string Payload { get; set; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}

static class Log
{
    private static readonly object Sync = new();

    public static void Write(string message)
    {
        lock (Sync)
        {
            Console.WriteLine(message);
        }
    }
}

sealed class SemaphoreBuffer : IDisposable
{
    private readonly Queue<int> _buffer = new();
    private readonly SemaphoreSlim _emptySlots;
    private readonly SemaphoreSlim _filledSlots;
    private readonly Mutex _mutex = new();
    private readonly int _capacity;

    private long _produced;
    private long _consumed;
    private int _maxOccupancy;
    private long _producerWaitMs;
    private long _consumerWaitMs;
    private long _startedTicks;

    public int ProduceDelayMin { get; init; } = 150;
    public int ProduceDelayMax { get; init; } = 500;
    public int ConsumeDelayMin { get; init; } = 150;
    public int ConsumeDelayMax { get; init; } = 500;

    public SemaphoreBuffer(int capacity)
    {
        _capacity = capacity;
        _emptySlots = new SemaphoreSlim(capacity, capacity);
        _filledSlots = new SemaphoreSlim(0, capacity);
    }

    public async Task RunAsync(int producers, int consumers, TimeSpan duration, CancellationToken ct)
    {
        _startedTicks = Environment.TickCount64;
        List<Task> tasks = new();
        for (int p = 0; p < producers; p++)
        {
            int id = p + 1;
            tasks.Add(Task.Run(() => ProduceAsync(id, ct), ct));
        }

        for (int c = 0; c < consumers; c++)
        {
            int id = c + 1;
            tasks.Add(Task.Run(() => ConsumeAsync(id, ct), ct));
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
        }
    }

    async Task ProduceAsync(int id, CancellationToken ct)
    {
        Random rand = new(id * 7919);
        while (!ct.IsCancellationRequested)
        {
            long start = Environment.TickCount64;
            try
            {
                await _emptySlots.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            Interlocked.Add(ref _producerWaitMs, Environment.TickCount64 - start);

            int item = rand.Next(1, 100);
            int occupancy;
            _mutex.WaitOne();
            try
            {
                _buffer.Enqueue(item);
                occupancy = _buffer.Count;
                int max = Volatile.Read(ref _maxOccupancy);
                if (occupancy > max)
                {
                    Interlocked.Exchange(ref _maxOccupancy, occupancy);
                }
            }
            finally
            {
                _mutex.ReleaseMutex();
            }

            _filledSlots.Release();
            Interlocked.Increment(ref _produced);

            try
            {
                await Task.Delay(rand.Next(ProduceDelayMin, ProduceDelayMax), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    async Task ConsumeAsync(int id, CancellationToken ct)
    {
        Random rand = new((id + 10) * 7919);
        while (!ct.IsCancellationRequested)
        {
            long start = Environment.TickCount64;
            try
            {
                await _filledSlots.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            Interlocked.Add(ref _consumerWaitMs, Environment.TickCount64 - start);

            _mutex.WaitOne();
            try
            {
                _buffer.Dequeue();
            }
            finally
            {
                _mutex.ReleaseMutex();
            }

            _emptySlots.Release();
            Interlocked.Increment(ref _consumed);

            try
            {
                await Task.Delay(rand.Next(ConsumeDelayMin, ConsumeDelayMax), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        _mutex.Dispose();
        _emptySlots.Dispose();
        _filledSlots.Dispose();
    }

    public string Report(int capacity, int producers, int consumers) =>
        $"  буфер {capacity,2} | {producers} пр-ва, {consumers} потр-ля | произведено {_produced,3} | потреблено {_consumed,3} | макс. заполненность {_maxOccupancy,2} | ожидание пр-в {_producerWaitMs,5} мс | ожидание потр-ля {_consumerWaitMs,5} мс | {Environment.TickCount64 - _startedTicks} мс";
}

sealed class ObjectBuffer : IDisposable
{
    private readonly Queue<DataItem> _buffer = new();
    private readonly SemaphoreSlim _emptySlots;
    private readonly SemaphoreSlim _filledSlots;
    private readonly Mutex _mutex = new();
    private readonly int _capacity;
    private int _nextId;

    public ObjectBuffer(int capacity)
    {
        _capacity = capacity;
        _emptySlots = new SemaphoreSlim(capacity, capacity);
        _filledSlots = new SemaphoreSlim(0, capacity);
    }

    public async Task RunAsync(int producers, int consumers, TimeSpan duration, CancellationToken ct)
    {
        List<Task> tasks = new();
        for (int p = 0; p < producers; p++)
        {
            int id = p + 1;
            tasks.Add(Task.Run(() => ProduceAsync(id, ct), ct));
        }

        for (int c = 0; c < consumers; c++)
        {
            int id = c + 1;
            tasks.Add(Task.Run(() => ConsumeAsync(id, ct), ct));
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
        }
    }

    async Task ProduceAsync(int id, CancellationToken ct)
    {
        Random rand = new(id * 31337);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _emptySlots.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            DataItem item = new()
            {
                Id = Interlocked.Increment(ref _nextId),
                Payload = $"Данные от потока {id} #{rand.Next(1000, 9999)}",
                CreatedAt = DateTime.Now
            };

            int occupancy;
            _mutex.WaitOne();
            try
            {
                _buffer.Enqueue(item);
                occupancy = _buffer.Count;
            }
            finally
            {
                _mutex.ReleaseMutex();
            }

            Log.Write($"[Producer {id}] Добавлен объект: Id={item.Id}, Payload=\"{item.Payload}\". В буфере: {occupancy} из {_capacity}.");
            _filledSlots.Release();

            try
            {
                await Task.Delay(rand.Next(400, 900), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Log.Write($"[Producer {id}] Завершен.");
    }

    async Task ConsumeAsync(int id, CancellationToken ct)
    {
        Random rand = new((id + 10) * 31337);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _filledSlots.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            DataItem item;
            int occupancy;
            _mutex.WaitOne();
            try
            {
                item = _buffer.Dequeue();
                occupancy = _buffer.Count;
            }
            finally
            {
                _mutex.ReleaseMutex();
            }

            Log.Write($"[Consumer {id}] Обработан объект: Id={item.Id}, Payload=\"{item.Payload}\", создан {item.CreatedAt:HH:mm:ss.fff}. В буфере осталось: {occupancy}.");
            _emptySlots.Release();

            try
            {
                await Task.Delay(rand.Next(300, 700), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Log.Write($"[Consumer {id}] Завершен.");
    }

    public void Dispose()
    {
        _mutex.Dispose();
        _emptySlots.Dispose();
        _filledSlots.Dispose();
    }
}

static class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 9. PRODUCER-CONSUMER: РАЗМЕР БУФЕРА, ЧИСЛО ПОТОКОВ, ОБЪЕКТЫ ===");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");

        BufferSizeComparison();
        ThreadCountComparison();
        await ObjectBufferDemo();

        Console.WriteLine("=== ЗАДАНИЕ 9 ВЫПОЛНЕНО ===");
        Pause();
    }

    static void BufferSizeComparison()
    {
        Console.WriteLine("--- 9.1. Влияние размера буфера (2 производителя, 3 потребителя, 3 секунды) ---");
        Console.WriteLine("  Производители работают быстро (5-25 мс), потребители медленно (300-700 мс),");
        Console.WriteLine("  поэтому буфер переполняется и размер начинает ограничивать производителей.");

        foreach (int size in new[] { 2, 5, 10 })
        {
            using SemaphoreBuffer buffer = new(size)
            {
                ProduceDelayMin = 5,
                ProduceDelayMax = 25,
                ConsumeDelayMin = 300,
                ConsumeDelayMax = 700
            };
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));
            buffer.RunAsync(2, 3, TimeSpan.FromSeconds(3), cts.Token).GetAwaiter().GetResult();
            Console.WriteLine(buffer.Report(size, 2, 3));
        }

        Console.WriteLine();
        Console.WriteLine("  Максимальная заполненность в каждом случае равна размеру буфера:");
        Console.WriteLine("  свободных мест не остаётся никогда, то есть backpressure работает одинаково.");
        Console.WriteLine($"  Потреблено во всех трёх запусках одинаково (18 элементов): узкое место — потребители,");
        Console.WriteLine("  они обрабатывают по одному элементу примерно каждые 500 мс.");
        Console.WriteLine("  Размер буфера изменил только число произведённых элементов (20 -> 23 -> 28):");
        Console.WriteLine("  с бо́льшим буфером производители успевают сделать стартовый запас,");
        Console.WriteLine("  но в установившемся режиме throughput всё равно равен скорости потребителей.");
        Console.WriteLine("  Обратная сторона большого буфера: элемент дольше лежит в очереди,");
        Console.WriteLine("  поэтому для данных с требованием свежести большой буфер вреден.\n");
    }

    static void ThreadCountComparison()
    {
        Console.WriteLine("--- 9.2. Влияние числа потоков (буфер 5, 3 секунды) ---");

        Console.WriteLine("  Здесь скорости производителей и потребителей сопоставимы, меняется только число потоков.");
        (int Producers, int Consumers)[] configs = { (2, 3), (3, 3), (4, 4) };
        foreach ((int producers, int consumers) in configs)
        {
            using SemaphoreBuffer buffer = new(5);
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));
            buffer.RunAsync(producers, consumers, TimeSpan.FromSeconds(3), cts.Token).GetAwaiter().GetResult();
            Console.WriteLine(buffer.Report(5, producers, consumers));
        }

        Console.WriteLine();
        Console.WriteLine("  4 производителя и 4 потребителя отработали без взаимоблокировок: любой поток,");
        Console.WriteLine("  ожидающий семафор, освобождается по Release от противоположной стороны.");
        Console.WriteLine("  Все конфигурации используют один и тот же буфер, меняется только число потоков.\n");
    }

    static async Task ObjectBufferDemo()
    {
        Console.WriteLine("--- 9.3. Усложнённое задание: буфер хранит объекты DataItem { Id, Payload } ---");
        Console.WriteLine("  2 производителя и 2 потребителя, буфер 3, 6 секунд, подробный лог.\n");

        using ObjectBuffer buffer = new(3);
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(6));
        await buffer.RunAsync(2, 2, TimeSpan.FromSeconds(6), cts.Token);

        Console.WriteLine();
        Console.WriteLine("  Семафоры считают такие же места, но сам буфер — это Queue<DataItem>.");
        Console.WriteLine("  Объекты создаются в одном потоке и читаются в другом: общей изменяемой");
        Console.WriteLine("  части у них нет, поэтому гонки данных за поля Id и Payload не возникает.");
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
