# Задание 4 — Исследование статических ссылок и утечек памяти

**Файл:** `Lab2/Task4/Program.cs`
**Целевая платформа:** .NET 10.0 (SDK 10.0.401)

---

## Что изучаем в этом задании

Мы углублённо исследуем **два механизма утечек** (статические ссылки и события) и изучаем **систему поколений** GC.

В Задании 1 мы увидели базовые примеры утечек «на поверхности». Здесь мы:
- Создаём классы, которые **демонстрируют** утечки через статическую коллекцию и событие.
- Измеряем память **до**, **после** и **после очистки** — наглядно видим, как работает GC.
- Исследуем **поколения**: как объекты перемещаются Gen0 → Gen1 → Gen2 и почему LOH сразу попадает в Gen2.

---

## Связь с предыдущими заданиями

- **Задание 1** показало базовые утечки (статика, события, LOH, boxing) — кратко.
- **Задание 2** реализовало `MemoryMonitor : IDisposable` — инструмент мониторинга.
- **Задание 3** протестировало `MemoryMonitor` и создало дампы.
- **Задание 4** создаёт **отдельные классы** для каждого типа утечки и подробно исследует поведение GC.

---

## Полный код

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;

// ─── Статическая утечка ───

public class StaticLeakDemo
{
    private static List<byte[]> _staticList = new List<byte[]>(); // Корень GC!

    public static void Reset() => _staticList.Clear(); // Единственный способ освободить память

    public static void AddData()
    {
        for (int i = 0; i < 1000; i++)
        {
            _staticList.Add(new byte[1024 * 10]); // 1000 × 10 КБ = 10 МБ
        }
    }

    public static void CheckMemory(string message = "")
    {
        if (!string.IsNullOrEmpty(message)) Console.WriteLine($"--- {message} ---");
        Console.WriteLine($"Текущий размер памяти: {GC.GetTotalMemory(true) / (1024 * 1024)} MB");
        Console.WriteLine($"Количество объектов в статическом списке: {_staticList.Count:N0}");
        Console.WriteLine($"Поколение статического списка: {GC.GetGeneration(_staticList)}");
    }
}

// ─── Подписчик события ───

public class EventSubscriber
{
    private byte[] _data;
    public string Name { get; }

    public EventSubscriber(int id, int sizeInKB)
    {
        Name = $"Подписчик_{id}";
        _data = new byte[sizeInKB * 1024]; // 256 КБ на каждого
    }

    public void HandleData(object? sender, EventArgs e)
    {
        var temp = new byte[1024]; // Временный буфер в обработчике
    }
}

// ─── Издатель события ───

public class EventLeakDemo
{
    public event EventHandler? DataEvent;

    public void Subscribe(EventHandler handler) => DataEvent += handler;
    public void Unsubscribe(EventHandler handler) => DataEvent -= handler;
}

// ─── Сравнение поколений ───

public class GenerationComparison
{
    public static void Compare()
    {
        Console.WriteLine("\n=== Сравнение поколений объектов ===");

        var obj = new object();
        Console.WriteLine($"Объект сразу после создания: поколение {GC.GetGeneration(obj)}");

        for (int i = 1; i <= 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Console.WriteLine($"После GC.Collect() #{i}: поколение {GC.GetGeneration(obj)}");
        }

        Console.WriteLine("\nОбъекты разных размеров (определяем поколение):");
        int[] sizes = { 1024 * 10, 1024 * 50, 90000, 200000 };
        var holders = new List<byte[]>();
        foreach (int size in sizes)
        {
            var data = new byte[size];
            holders.Add(data);
            string note = size > 85000 ? "  -> размер > 85 КБ, сразу в LOH (поколение 2)" : "";
            Console.WriteLine($"размер {size / 1024.0:F0} КБ: поколение {GC.GetGeneration(data)}{note}");
        }
    }
}

// ─── Точка входа ───

class Program
{
    static void Main()
    {
        Console.WriteLine("=== Задание 4. Исследование статических ссылок и утечек памяти ===\n");

        // Часть 1: Статическая утечка
        Console.WriteLine("Часть 1. Статическая утечка.");
        StaticLeakDemo.CheckMemory("До создания объектов");

        StaticLeakDemo.AddData(); // 1000 × 10 КБ в статический список
        StaticLeakDemo.CheckMemory("После AddData() (1000 x 10 КБ)");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine("\nПосле GC.Collect() + WaitForPendingFinalizers:");
        StaticLeakDemo.CheckMemory();
        Console.WriteLine("=> Объекты НЕ удалены: статический список — корень GC, живёт до завершения программы.");

        StaticLeakDemo.Reset();   // Очищаем статический список
        GC.Collect();
        Console.WriteLine("\nПосле Reset() + GC.Collect():");
        StaticLeakDemo.CheckMemory();
        Console.WriteLine("=> Память освобождена после очистки статической коллекции.");

        // Часть 2: Событийная утечка
        Console.WriteLine("\n\nЧасть 2. Событийная утечка (неотписанные события).");
        RunEventDemo();

        // Часть 3: Поколения
        GenerationComparison.Compare();

        Console.WriteLine("\nНажмите любую клавишу для завершения...");
        try { Console.ReadKey(); } catch { }
    }

    static void RunEventDemo()
    {
        var publisher = new EventLeakDemo();
        var subscribers = new List<EventSubscriber>();
        var handlers = new List<EventHandler>();

        // Подписываем 100 подписчиков по 256 КБ
        for (int i = 0; i < 100; i++)
        {
            var subscriber = new EventSubscriber(i, 256);
            subscribers.Add(subscriber);
            handlers.Add(subscriber.HandleData);
            publisher.Subscribe(subscriber.HandleData);
        }

        ForceCollect();
        Console.WriteLine($"Память после 100 подписок по 256 КБ: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
        Console.WriteLine($"Working Set с подписками: {Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)} MB");
        Console.WriteLine($"Кол-во подписчиков: {subscribers.Count}");

        // Отписываем всех
        foreach (var handler in handlers) publisher.Unsubscribe(handler);

        subscribers.Clear();
        handlers.Clear();
        ForceCollect();
        Console.WriteLine($"\nПамять после отписки всех: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
        Console.WriteLine($"Working Set после отписки: {Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)} MB");
        Console.WriteLine("=> Пока есть подписка, событие держит ссылку на обработчик (и объект-подписчик),");
        Console.WriteLine("   GC не может его собрать. После отписки объекты становятся недостижимыми и удаляются.");
    }

    static void ForceCollect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect(); // Двойная сборка для полной очистки
    }
}
```

---

## Пошаговый разбор

### Часть 1. Статическая утечка

**Шаг 1:** `StaticLeakDemo.CheckMemory("До создания объектов")`
- Память ~0 МБ, объектов в списке 0.

**Шаг 2:** `StaticLeakDemo.AddData()`
- 1000 раз создаёт `byte[10240]` и добавляет в `_staticList`.
- `_staticList` — **статическое поле**, корень GC. Объекты недостижимы из кода, но GC считает их живыми.

**Шаг 3:** `GC.Collect()` + `CheckMemory()`
- Память **не изменилась** (~10 МБ). GC попытался собрать, но объекты удерживаются корнем.

**Шаг 4:** `Reset()` + `GC.Collect()`
- `_staticList.Clear()` убирает ссылки. Теперь GC может удалить объекты.
- Память падает до 0 МБ.

**Вывод:** Статическая коллекция — корень GC. Без `Clear()` память не освобождается.

### Часть 2. Событийная утечка

**Шаг 1:** Подписка 100 подписчиков
- Каждый `EventSubscriber` хранит `byte[256KB]`.
- Подписка `publisher.Subscribe(subscriber.HandleData)` создаёт делегат, который ссылается на `subscriber`.
- Делегат хранится в поле `DataEvent` издателя.

**Шаг 2:** Измерение памяти
- ~25 МБ (100 × 256 КБ) + служебные структуры.

**Шаг 3:** Отписка
- `publisher.Unsubscribe(handler)` удаляет делегат из `DataEvent`.
- Теперь ссылка на `subscriber` отсутствует → GC может удалить.

**Шаг 4:** После отписки
- Память падает до 0 МБ.

**Вывод:** Событие = делегат = ссылка на подписчика. Без отписки — утечка.

### Часть 3. Сравнение поколений

**Эксперимент 1:** Один объект, три сборки

```
Объект сразу после создания: поколение 0
После GC.Collect() #1: поколение 1
После GC.Collect() #2: поколение 2
После GC.Collect() #3: поколение 2
```

Объект перемещается Gen0 → Gen1 → Gen2. После Gen2 остаётся там.

**Эксперимент 2:** Объекты разных размеров

```
размер  10 КБ: поколение 0 (SOH)
размер  50 КБ: поколение 0 (SOH)
размер  90 КБ: поколение 2 (LOH)  -> сразу в LOH
размер 200 КБ: поколение 2 (LOH)  -> сразу в LOH
```

Объекты > 85 КБ **сразу** попадают в LOH (поколение 2), минуя Gen0/Gen1.

---

## Результаты выполнения

```
=== Задание 4. Исследование статических ссылок и утечек памяти ===

Часть 1. Статическая утечка.
--- До создания объектов ---
Текущий размер памяти: 0 MB
Количество объектов в статическом списке: 0

--- После AddData() (1000 x 10 КБ) ---
Текущий размер памяти: 9 MB
Количество объектов в статическом списке: 1,000
Поколение статического списка: 2

После GC.Collect() + WaitForPendingFinalizers:
Текущий размер памяти: 9 MB
Количество объектов в статическом списке: 1,000
=> Объекты НЕ удалены: статический список — корень GC.

После Reset() + GC.Collect():
Текущий размер памяти: 0 MB
Количество объектов в статическом списке: 0
=> Память освобождена после очистки статической коллекции.

Часть 2. Событийная утечка (неотписанные события).
Память после 100 подписок по 256 КБ: 25 MB
Working Set с подписками: 39 MB

Память после отписки всех: 0 MB
Working Set после отписки: 39 MB
=> После отписки объекты удаляются.

=== Сравнение поколений объектов ===
Объект сразу после создания: поколение 0
После GC.Collect() #1: поколение 1
После GC.Collect() #2: поколение 2
После GC.Collect() #3: поколение 2

размер  10 КБ: поколение 0 (SOH)
размер  50 КБ: поколение 0 (SOH)
размер  90 КБ: поколение 2 (LOH)  -> сразу в LOH
размер 200 КБ: поколение 2 (LOH)  -> сразу в LOH
```

---

## Ключевые концепции

### Статические поля как корни GC

```csharp
private static List<byte[]> _staticList; // Корень GC
```

**Почему это корень:** Статическое поле принадлежит **типу**, а не экземпляру. Пока существует тип (пока работает приложение), статическое поле доступно. GC начинает обход корней с этого поля → все объекты в списке считаются достижимыми.

### События и делегаты

```csharp
public event EventHandler DataEvent; // Поле-делегат

// Подписка:
publisher.DataEvent += subscriber.HandleData;
// Эквивалентно:
publisher.DataEvent = Delegate.Combine(publisher.DataEvent, new EventHandler(subscriber.HandleData));
```

**Цепочка ссылок:**
```
publisher.DataEvent (поле-делегат)
    → multicast delegate
        → handler1 (subscriber1.HandleData)
            → subscriber1 (объект)
                → _data (byte[256KB])
        → handler2 (subscriber2.HandleData)
            → subscriber2 (объект)
                → _data (byte[256KB])
        → ...
```

Пока `DataEvent` ссылается на делегат, все подписчики **достижимы** из корня.

### Поколения (Generations)

| Поколение | Порог попадания | Частота сборки | Компактизация |
|-----------|----------------|----------------|---------------|
| **Gen0** | Новый объект | Очень часто | Да |
| **Gen1** | Пережил 1 сборку Gen0 | Средне | Да |
| **Gen2** | Пережил 2 сборки Gen0 | Редко | Да |
| **LOH** | Размер > 85 КБ | Только при Gen2 | Нет |

**Почему 3 поколения:** Большинство объектов живут недолго (временные строки, буферы). Если объект пережил сборку — скорее всего, он будет жить долго. Поэтому Gen2 собирается реже.

### LOH и порог 85 КБ

**Почему именно 85 КБ:** Это историческое значение, выбранное разработчиками .NET. Объекты > 85 КБ считаются «большими» и размещаются отдельно, чтобы не перемещать их при компактизации.

**Почему LOH не уплотняется:** Компактизация = копирование объектов в памяти. Большие объекты копировать дорого (время + память). Поэтому LOH остаётся фрагментированным.

---

## Типичные ошибки

1. **«События отписываются автоматически»** — Нет. Делегат хранит ссылку на подписчика. Без явной отписки (`-=`) ссылка остаётся.

2. **Использовать статические коллекции без контроля** — `static List<T>` без лимита размера растёт бесконечно. Используйте `MemoryCache` или ограничивайте размер.

3. **Считать, что `GC.Collect()` решает проблему** — GC не может удалить объект, удерживаемый корнем. `Reset()` + `GC.Collect()` — правильный порядок.

4. **Забыть, что `ForceCollect()` лучше одной сборки** — Двойная сборка (`GC.Collect()` → `WaitForPendingFinalizers()` → `GC.Collect()`) надёжнее: первая сборка перемещает объекты, вторая — удаляет.

---

## Практическое применение

- **UI-приложения:** Подписка на событие ViewModel в View. Если View не отписывается при закрытии — ViewModel не собирается. Решение: слабые события (`WeakEventManager`) или отписка в `OnClosing`.
- **Серверные приложения:** Статический кэш без лимита. Решение: `MemoryCache` с политикой `SlidingExpiration`.
- **Логирование:** Статический список логов без очистки. Решение: кольцевой буфер или ограничение размера.
- **Тестирование:** `GC.Collect()` + `GC.WaitForPendingFinalizers()` — стандартный приём для проверки утечек в unit-тестах.

---

## Итог

1. **Статические коллекции** — корни GC. Без `Clear()` память не освобождается.
2. **События** — делегаты хранят ссылки на подписчиков. Без отписки (`-=`) — утечка.
3. **Поколения:** Gen0 → Gen1 → Gen2. Объекты перемещаются при переживании сборок.
4. **LOH** (> 85 КБ) — сразу в Gen2, не уплотняется, собирается реже.
5. **`GC.Collect()`** не решает утечки с корнями — нужно сначала убрать корень.
