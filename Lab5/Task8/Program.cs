namespace Lab5Task8;

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

    public static void Section(string title)
    {
        lock (Sync)
        {
            Console.WriteLine();
            Console.WriteLine($"--- {title} ---");
        }
    }
}

sealed record PcStats(
    string Name,
    long Produced,
    long Consumed,
    int MaxOccupancy,
    long ProducerWaitMs,
    long ConsumerWaitMs,
    long ElapsedMs,
    int Remaining)
{
    public bool Balanced => Produced == Consumed;
}

interface IProducerConsumer
{
    string Name { get; }

    bool Verbose { get; set; }

    Task RunAsync(int producers, int consumers, TimeSpan duration, CancellationToken ct);

    PcStats GetStats();

    int Remaining { get; }
}

sealed class SemaphoreBuffer : IProducerConsumer, IDisposable
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

    public SemaphoreBuffer(int capacity)
    {
        _capacity = capacity;
        _emptySlots = new SemaphoreSlim(capacity, capacity);
        _filledSlots = new SemaphoreSlim(0, capacity);
    }

    public string Name => "SemaphoreSlim (empty/filled) + Mutex";

    public bool Verbose { get; set; }

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
            long startWait = Environment.TickCount64;
            try
            {
                await _emptySlots.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            Interlocked.Add(ref _producerWaitMs, Environment.TickCount64 - startWait);

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

                if (Verbose)
                {
                    Log.Write($"[Producer {id}] Добавлен элемент: {item}. В буфере: {occupancy} элементов.");
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
                await Task.Delay(rand.Next(200, 600), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (Verbose)
        {
            Log.Write($"[Producer {id}] Завершен.");
        }
    }

    async Task ConsumeAsync(int id, CancellationToken ct)
    {
        Random rand = new((id + 10) * 7919);
        while (!ct.IsCancellationRequested)
        {
            long startWait = Environment.TickCount64;
            try
            {
                await _filledSlots.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            Interlocked.Add(ref _consumerWaitMs, Environment.TickCount64 - startWait);

            int item;
            int occupancy;
            _mutex.WaitOne();
            try
            {
                item = _buffer.Dequeue();
                occupancy = _buffer.Count;
                if (Verbose)
                {
                    Log.Write($"[Consumer {id}] Извлечен элемент: {item}. В буфере: {occupancy} элементов.");
                }
            }
            finally
            {
                _mutex.ReleaseMutex();
            }

            _emptySlots.Release();
            Interlocked.Increment(ref _consumed);

            try
            {
                await Task.Delay(rand.Next(300, 800), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (Verbose)
        {
            Log.Write($"[Consumer {id}] Завершен.");
        }
    }

    public int Remaining
    {
        get
        {
            _mutex.WaitOne();
            try
            {
                return _buffer.Count;
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }
    }

    public PcStats GetStats() => new(
        Name,
        Interlocked.Read(ref _produced),
        Interlocked.Read(ref _consumed),
        Volatile.Read(ref _maxOccupancy),
        Interlocked.Read(ref _producerWaitMs),
        Interlocked.Read(ref _consumerWaitMs),
        Environment.TickCount64 - _startedTicks,
        Remaining);

    public void Dispose()
    {
        _mutex.Dispose();
        _emptySlots.Dispose();
        _filledSlots.Dispose();
    }
}

sealed class MonitorBuffer : IProducerConsumer
{
    private readonly object _lock = new();
    private readonly Queue<int> _buffer = new();
    private readonly int _capacity;

    private bool _notFull = true;
    private bool _notEmpty;

    private long _produced;
    private long _consumed;
    private int _maxOccupancy;
    private long _producerWaitMs;
    private long _consumerWaitMs;
    private long _startedTicks;

    public MonitorBuffer(int capacity) => _capacity = capacity;

    public string Name => "Monitor.Wait / Monitor.Pulse";

    public bool Verbose { get; set; }

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
        Random rand = new(id * 104729);
        while (!ct.IsCancellationRequested)
        {
            long startWait = Environment.TickCount64;
            try
            {
                Put(rand.Next(1, 100), id, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            Interlocked.Add(ref _producerWaitMs, Environment.TickCount64 - startWait);
            Interlocked.Increment(ref _produced);

            try
            {
                await Task.Delay(rand.Next(200, 600), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (Verbose)
        {
            Log.Write($"[Producer {id}] Завершен.");
        }
    }

    void Put(int item, int id, CancellationToken ct)
    {
        lock (_lock)
        {
            while (!_notFull)
            {
                ct.ThrowIfCancellationRequested();
                Monitor.Wait(_lock, 100);
                ct.ThrowIfCancellationRequested();
            }

            _buffer.Enqueue(item);
            int occupancy = _buffer.Count;
            int max = _maxOccupancy;
            if (occupancy > max)
            {
                _maxOccupancy = occupancy;
            }

            if (Verbose)
            {
                Log.Write($"[Producer {id}] Добавлен элемент: {item}. В буфере: {occupancy} элементов.");
            }

            _notEmpty = true;
            _notFull = _buffer.Count < _capacity;
            Monitor.PulseAll(_lock);
        }
    }

    async Task ConsumeAsync(int id, CancellationToken ct)
    {
        Random rand = new((id + 10) * 104729);
        while (!ct.IsCancellationRequested)
        {
            long startWait = Environment.TickCount64;
            bool taken = TryTake(out int item, out int occupancy, ct);
            if (!taken)
            {
                break;
            }

            Interlocked.Add(ref _consumerWaitMs, Environment.TickCount64 - startWait);

            if (Verbose)
            {
                Log.Write($"[Consumer {id}] Извлечен элемент: {item}. В буфере: {occupancy} элементов.");
            }

            Interlocked.Increment(ref _consumed);

            try
            {
                await Task.Delay(rand.Next(300, 800), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (Verbose)
        {
            Log.Write($"[Consumer {id}] Завершен.");
        }
    }

    bool TryTake(out int item, out int occupancy, CancellationToken ct)
    {
        lock (_lock)
        {
            while (!_notEmpty)
            {
                if (ct.IsCancellationRequested)
                {
                    item = 0;
                    occupancy = 0;
                    return false;
                }

                Monitor.Wait(_lock, 100);
            }

            item = _buffer.Dequeue();
            occupancy = _buffer.Count;
            _notEmpty = _buffer.Count > 0;
            _notFull = true;
            Monitor.PulseAll(_lock);
            return true;
        }
    }

    public int Remaining
    {
        get
        {
            lock (_lock)
            {
                return _buffer.Count;
            }
        }
    }

    public PcStats GetStats() => new(
        Name,
        Interlocked.Read(ref _produced),
        Interlocked.Read(ref _consumed),
        _maxOccupancy,
        Interlocked.Read(ref _producerWaitMs),
        Interlocked.Read(ref _consumerWaitMs),
        Environment.TickCount64 - _startedTicks,
        Remaining);
}

sealed class ConcurrentQueueBuffer : IProducerConsumer
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<int> _queue = new();
    private readonly SemaphoreSlim _emptySlots;
    private readonly SemaphoreSlim _filledSlots;

    private long _produced;
    private long _consumed;
    private int _maxOccupancy;
    private long _producerWaitMs;
    private long _consumerWaitMs;
    private long _startedTicks;

    public ConcurrentQueueBuffer(int capacity)
    {
        _emptySlots = new SemaphoreSlim(capacity, capacity);
        _filledSlots = new SemaphoreSlim(0, capacity);
    }

    public string Name => "ConcurrentQueue + SemaphoreSlim (счётчики)";

    public bool Verbose { get; set; }

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
        Random rand = new(id * 15485863);
        while (!ct.IsCancellationRequested)
        {
            long startWait = Environment.TickCount64;
            try
            {
                await _emptySlots.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            Interlocked.Add(ref _producerWaitMs, Environment.TickCount64 - startWait);

            int item = rand.Next(1, 100);
            _queue.Enqueue(item);
            int occupancy = _queue.Count;
            int max = Volatile.Read(ref _maxOccupancy);
            if (occupancy > max)
            {
                Interlocked.Exchange(ref _maxOccupancy, occupancy);
            }

            if (Verbose)
            {
                Log.Write($"[Producer {id}] Добавлен элемент: {item}. В буфере: {occupancy} элементов.");
            }

            _filledSlots.Release();
            Interlocked.Increment(ref _produced);

            try
            {
                await Task.Delay(rand.Next(200, 600), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (Verbose)
        {
            Log.Write($"[Producer {id}] Завершен.");
        }
    }

    async Task ConsumeAsync(int id, CancellationToken ct)
    {
        Random rand = new((id + 10) * 15485863);
        while (!ct.IsCancellationRequested)
        {
            long startWait = Environment.TickCount64;
            try
            {
                await _filledSlots.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            Interlocked.Add(ref _consumerWaitMs, Environment.TickCount64 - startWait);

            if (_queue.TryDequeue(out int item))
            {
                if (Verbose)
                {
                    Log.Write($"[Consumer {id}] Извлечен элемент: {item}. В буфере: {_queue.Count} элементов.");
                }
            }

            _emptySlots.Release();
            Interlocked.Increment(ref _consumed);

            try
            {
                await Task.Delay(rand.Next(300, 800), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (Verbose)
        {
            Log.Write($"[Consumer {id}] Завершен.");
        }
    }

    public int Remaining => _queue.Count;

    public PcStats GetStats() => new(
        Name,
        Interlocked.Read(ref _produced),
        Interlocked.Read(ref _consumed),
        Volatile.Read(ref _maxOccupancy),
        Interlocked.Read(ref _producerWaitMs),
        Interlocked.Read(ref _consumerWaitMs),
        Environment.TickCount64 - _startedTicks,
        Remaining);
}

static class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 8. PATTERN PRODUCER-CONSUMER ===");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}");
        Console.WriteLine("Все три реализации используют CancellationTokenSource: отмена разблокирует");
        Console.WriteLine("потоки, ожидающие семафор, поэтому программа всегда завершается.\n");

        Log.Section("8.1. SemaphoreSlim (empty/filled) + Mutex: 2 производителя и 3 потребителя, буфер 5");
        PcStats semaphoreStats = await RunAsync(new SemaphoreBuffer(5), 2, 3, TimeSpan.FromSeconds(5), verbose: true);

        Log.Section("8.2. Тот же сценарий без подробного лога");
        PcStats monitorStats = await RunAsync(new MonitorBuffer(5), 2, 3, TimeSpan.FromSeconds(5), verbose: false);
        PcStats queueStats = await RunAsync(new ConcurrentQueueBuffer(5), 2, 3, TimeSpan.FromSeconds(5), verbose: false);

        Log.Section("8.3. Сравнение реализаций");
        Console.WriteLine($"  {"Реализация",-46} {"Произв.",8} {"Потреб.",8} {"В буфере",9} {"Ожидание пр-в",14} {"Ожидание потр.",14}");
        foreach (PcStats s in new[] { semaphoreStats, monitorStats, queueStats })
        {
            Console.WriteLine($"  {s.Name,-46} {s.Produced,8} {s.Consumed,8} {s.Remaining,9} {s.ProducerWaitMs,11} мс {s.ConsumerWaitMs,11} мс");
        }

        Console.WriteLine();
        Console.WriteLine("  Расхождение «произведено/потреблено» на 1-2 элемента нормально: работа останавливается");
        Console.WriteLine("  по таймеру, и в момент отмены элемент может остаться в буфере или в пути к потребителю.");

        Console.WriteLine();
        Console.WriteLine("  Смысл показателей:");
        Console.WriteLine("  empty-семафор — свободные места, производитель ждёт при переполнении;");
        Console.WriteLine("  filled-семафор — занятые места, потребитель ждёт при пустом буфере;");
        Console.WriteLine("  Mutex защищает саму очередь: без него два потока повреждают внутренний массив Queue<T>");
        Console.WriteLine("  и элементы теряются либо дублируются.");
        Console.WriteLine("  Время ожидания потребителей больше, потому что трое читают из буфера, где двое наполняют.");

        Log.Section("8.4. Итог по сценарию 8.1");
        Console.WriteLine($"  Произведено {semaphoreStats.Produced}, потреблено {semaphoreStats.Consumed}, в буфере осталось {semaphoreStats.Remaining}, сведено: {semaphoreStats.Balanced}");
        Console.WriteLine($"  Максимальная заполненность буфера: {semaphoreStats.MaxOccupancy} из 5");
        Console.WriteLine($"  Общее время: {semaphoreStats.ElapsedMs} мс");

        Console.WriteLine("\n=== ЗАДАНИЕ 8 ВЫПОЛНЕНО ===");
        Pause();
    }

    static async Task<PcStats> RunAsync(IProducerConsumer model, int producers, int consumers, TimeSpan duration, bool verbose)
    {
        model.Verbose = verbose;
        using CancellationTokenSource cts = new(duration);
        await model.RunAsync(producers, consumers, duration, cts.Token);
        return model.GetStats();
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
