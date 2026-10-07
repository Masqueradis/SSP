using System.Collections.Concurrent;
using System.Diagnostics;

namespace Lab5Task5;

sealed class CacheEntry
{
    public string Value { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}

sealed class TtlCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _store = new();
    private readonly TimeSpan _ttl;
    private long _hits;
    private long _misses;

    public TtlCache(TimeSpan ttl) => _ttl = ttl;

    public long Hits => Interlocked.Read(ref _hits);

    public long Misses => Interlocked.Read(ref _misses);

    public int Count => _store.Count;

    public void Add(string key, string value) =>
        _store[key] = new CacheEntry { Value = value, CreatedAt = DateTime.UtcNow };

    public bool TryGet(string key, out string value)
    {
        if (_store.TryGetValue(key, out CacheEntry? entry))
        {
            if (DateTime.UtcNow - entry.CreatedAt < _ttl)
            {
                Interlocked.Increment(ref _hits);
                value = entry.Value;
                return true;
            }

            _store.TryRemove(key, out _);
        }

        Interlocked.Increment(ref _misses);
        value = string.Empty;
        return false;
    }

    public bool Remove(string key) => _store.TryRemove(key, out _);

    public void PurgeExpired()
    {
        DateTime now = DateTime.UtcNow;
        foreach (KeyValuePair<string, CacheEntry> pair in _store)
        {
            if (now - pair.Value.CreatedAt >= _ttl)
            {
                _store.TryRemove(pair.Key, out _);
            }
        }
    }
}

readonly record struct Order(int Id, decimal Amount, string Customer);

static class Program
{
    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 5. ПАРАЛЛЕЛЬНЫЕ КОЛЛЕКЦИИ ===");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");

        ConcurrentDictionaryBasics();
        TtlCacheDemo();
        OrderQueue();
        ConcurrentBagScraper();

        Console.WriteLine("=== ЗАДАНИЕ 5 ВЫПОЛНЕНО ===");
        Pause();
    }

    static void ConcurrentDictionaryBasics()
    {
        Console.WriteLine("--- 5.1. ConcurrentDictionary: параллельное добавление и обновление ---");

        ConcurrentDictionary<int, string> dict = new();

        Parallel.For(0, 100, i => dict.TryAdd(i, $"Value_{i}"));
        Console.WriteLine($"  После параллельного TryAdd: элементов {dict.Count} (ожидалось 100)");

        int updated = 0;
        Parallel.ForEach(dict, kvp =>
        {
            if (dict.TryUpdate(kvp.Key, $"Updated_{kvp.Value}", kvp.Value))
            {
                Interlocked.Increment(ref updated);
            }
        });

        Console.WriteLine($"  Параллельно обновлено записей: {updated}");
        Console.WriteLine($"  Пример: ключ 7 -> {dict[7]}");
        bool duplicate = dict.TryAdd(7, "дубль");
        Console.WriteLine($"  Повторный TryAdd ключа 7: {duplicate} (false — ключ уже существует), значение осталось {dict[7]}");
        Console.WriteLine("  Вывод: все операции ConcurrentDictionary атомарны, внешняя блокировка не нужна.\n");
    }

    static void TtlCacheDemo()
    {
        Console.WriteLine("--- 5.2. Потокобезопасный кэш на ConcurrentDictionary со сроком жизни 10 секунд ---");
        Console.WriteLine("  Ключи добавляются по одному каждые 4 секунды, затем проверяются на протухание.");

        TtlCache cache = new(TimeSpan.FromSeconds(10));
        var sw = Stopwatch.StartNew();

        for (int i = 1; i <= 3; i++)
        {
            cache.Add($"ключ-{i}", $"значение-{i}");
            Console.WriteLine($"  [{sw.Elapsed.TotalSeconds:F0} с] добавлен ключ-{i}, всего элементов {cache.Count}");
            if (i < 3)
            {
                Thread.Sleep(4000);
            }
        }

        Thread.Sleep(5000);

        Parallel.For(0, 100, i => _ = cache.TryGet($"ключ-{(i % 3) + 1}", out _));

        Console.WriteLine($"  [{sw.Elapsed.TotalSeconds:F0} с] Проверка после {sw.Elapsed.TotalSeconds:F0} секунд:");
        for (int i = 1; i <= 3; i++)
        {
            bool hit = cache.TryGet($"ключ-{i}", out string value);
            string age = $"добавлен {(int)(sw.Elapsed.TotalSeconds) - (i - 1) * 4} с назад";
            Console.WriteLine(hit
                ? $"    попадание: ключ-{i} = {value} ({age}, TTL ещё не истёк)"
                : $"    промах:   ключ-{i} ({age}, TTL истёк, элемент удалён)");
        }

        cache.PurgeExpired();
        Console.WriteLine($"  После PurgeExpired элементов в кэше: {cache.Count} (ключ-1 уже удалён при обращении)");
        Console.WriteLine($"  Статистика: попаданий {cache.Hits}, промахов {cache.Misses}");
        Console.WriteLine("  Вывод: ConcurrentDictionary сам по себе не хранит время жизни, TTL проверяется вручную.");
        Console.WriteLine("  Все обращения идут из 100 параллельных потоков, блокировка не требуется.\n");
    }

    static void OrderQueue()
    {
        Console.WriteLine("--- 5.3. ConcurrentQueue: 2 производителя заказов и 3 потребителя ---");

        const int ordersPerProducer = 50;
        ConcurrentQueue<Order> queue = new();
        ConcurrentBag<string> log = new();
        int processed = 0;
        int rejected = 0;

        Task[] producers = new Task[2];
        for (int p = 0; p < producers.Length; p++)
        {
            int pid = p;
            producers[p] = Task.Run(() =>
            {
                Random rand = new(1000 + pid);
                for (int i = 0; i < ordersPerProducer; i++)
                {
                    Order order = new(pid * 1000 + i, Math.Round((decimal)rand.Next(100, 10000) / 100, 2), $"Клиент {rand.Next(1, 50)}");
                    queue.Enqueue(order);
                }
            });
        }

        Task[] consumers = new Task[3];
        for (int c = 0; c < consumers.Length; c++)
        {
            int cid = c;
            consumers[c] = Task.Run(() =>
            {
                while (queue.TryDequeue(out Order order))
                {
                    if (order.Amount > 50m)
                    {
                        log.Add($"Потребитель {cid}: заказ {order.Id} на {order.Amount} принят");
                        Interlocked.Increment(ref processed);
                    }
                    else
                    {
                        log.Add($"Потребитель {cid}: заказ {order.Id} отклонён (сумма {order.Amount})");
                        Interlocked.Increment(ref rejected);
                    }
                }
            });
        }

        Task.WaitAll(producers);
        Task.WaitAll(consumers);

        Console.WriteLine($"  Создано заказов: {ordersPerProducer * 2}, осталось в очереди: {queue.Count}");
        Console.WriteLine($"  Обработано: {processed}, отклонено: {rejected}");
        foreach (string line in log.OrderBy(x => x).Take(6))
        {
            Console.WriteLine($"    {line}");
        }

        Console.WriteLine($"    ... всего записей в ConcurrentBag: {log.Count}");
        Console.WriteLine("  Вывод: ConcurrentQueue реализует FIFO без блокировки, TryDequeue потокобезопасен.\n");
    }

    static void ConcurrentBagScraper()
    {
        Console.WriteLine("--- 5.4. ConcurrentBag: параллельная «загрузка» данных с URL ---");

        string[] urls =
        {
            "https://example.com/a", "https://example.com/b", "https://example.com/c",
            "https://example.com/d", "https://example.com/e", "https://example.com/f",
            "https://example.com/g", "https://example.com/h"
        };

        ConcurrentBag<(string Url, int Bytes)> results = new();

        var sw = Stopwatch.StartNew();
        Parallel.ForEach(urls, url =>
        {
            Random rand = new(url.GetHashCode(StringComparison.Ordinal) & 0x7FFFFFFF);
            Thread.Sleep(rand.Next(50, 400));
            results.Add((url, rand.Next(1024, 512 * 1024)));
        });
        sw.Stop();

        Console.WriteLine($"  Загружено URL: {urls.Length}, результатов в ConcurrentBag: {results.Count}");
        Console.WriteLine($"  Суммарный объём: {results.Sum(r => r.Bytes) / 1024.0:F1} КБ, время {sw.ElapsedMilliseconds} мс");
        foreach ((string url, int bytes) in results.OrderBy(r => r.Url))
        {
            Console.WriteLine($"    {url} -> {bytes / 1024.0:F1} КБ");
        }

        Console.WriteLine("  Примечание: ConcurrentBag не сохраняет порядок и хранит элементы по локальным очередям потоков,");
        Console.WriteLine("  поэтому он быстрее ConcurrentQueue при добавлении без требования FIFO.\n");
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
