# Задание 1 — Базовые примеры утечек памяти

**Файл:** `Lab2/Task1/Program.cs`
**Целевая платформа:** .NET 10.0 (SDK 10.0.401)

---

## Что изучаем в этом задании

В .NET память управляется сборщиком мусора (Garbage Collector, GC). Программист не вызывает `malloc`/`free` напрямую — GC сам определяет, какие объекты больше не используются, и освобождает их. Но это не значит, что утечек памяти в .NET не бывает.

**Утечка памяти в .NET** — это ситуация, когда объект больше не нужен программе, но GC не может его удалить, потому что на него ссылается какой-то **корень** (root). Корни — это статические поля, локальные переменные в активных методах, регистры процессора, подписки на события. Пока хотя бы один корень ссылается на объект, GC считает его «живым».

В этом задании мы изучаем четыре базовых механизма, которые приводят к утечкам:

| Механизм | Суть проблемы |
|----------|---------------|
| Статические коллекции | Статическое поле — корень GC. Объекты в статической коллекции живут всё время работы программы. |
| События | Делегат события хранит ссылку на обработчик. Если не отписаться — объект недостижим для GC. |
| LOH-объекты | Объекты > 85 КБ попадают в Large Object Heap, который не уплотняется и собирается реже. |
| Boxing | Упаковка значимого типа в `object` создаёт объект в куче, нагружая GC. |

---

## Связь с предыдущими заданиями

Это первое задание второй лабораторной работы. Оно закладывает фундамент: все последующие задания строятся на понимании того, **почему** возникают утечки и **как** их обнаружить. Задание 2 реализует инструмент мониторинга, Задание 3 — анализ дампов, Задания 4–7 — углублённое исследование каждого механизма.

---

## Полный код

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;

// ─── Демонстрация: статическое vs экземплярное поле ───

public class StaticReferenceDemo
{
    private static List<int> _staticList = new List<int>(); // ОДИН список на ВСЕ экземпляры
    private List<int> _instanceList = new List<int>();       // Свой список у каждого

    public void AddToStatic(int value) => _staticList.Add(value);
    public void AddToInstance(int value) => _instanceList.Add(value);

    public void PrintLists(string objectName)
    {
        Console.WriteLine($"{objectName}:");
        Console.WriteLine($"  Статический список (общий): [{string.Join(", ", _staticList)}]");
        Console.WriteLine($"  Экземплярный список (свой): [{string.Join(", ", _instanceList)}]");
        Console.WriteLine();
    }
}

public class StaticFieldDemo
{
    private static int _staticCounter = 0; // ОДИН счётчик на все объекты
    private int _instanceCounter = 0;       // Свой счётчик у каждого

    public void Increment()
    {
        _staticCounter++;
        _instanceCounter++;
    }

    public void ShowCounters(string objectName)
    {
        Console.WriteLine($"{objectName}:");
        Console.WriteLine($"  Статический счетчик (общий): {_staticCounter}");
        Console.WriteLine($"  Экземплярный счетчик (свой): {_instanceCounter}");
    }

    public static void ShowStaticFieldDemo()
    {
        var objA = new StaticFieldDemo();
        var objB = new StaticFieldDemo();

        objA.Increment();  // staticCounter = 1, instanceCounter(objA) = 1
        objA.Increment();  // staticCounter = 2, instanceCounter(objA) = 2
        objB.Increment();  // staticCounter = 3, instanceCounter(objB) = 1

        Console.WriteLine("После операций:");
        objA.ShowCounters("Объект A");
        objB.ShowCounters("Объект B");
        Console.WriteLine("=> Статический счетчик ОДИН для обоих объектов, экземплярные РАЗНЫЕ.");
    }
}

// ─── Утечка через статическую коллекцию ───

public class MemoryLeakExample
{
    private static List<byte[]> _staticList = new List<byte[]>(); // Корень GC

    public static void CreateMemoryLeak()
    {
        for (int i = 0; i < 1000; i++)
        {
            _staticList.Add(new byte[1024 * 10]); // 10 КБ × 1000 = 10 МБ
        }
        // Ссылки на byte[] есть только в _staticList, но _staticList — статическое поле.
        // Даже если вызвать GC.Collect(), объекты НЕ будут удалены.
    }

    public static void ClearMemoryLeak()
    {
        _staticList.Clear(); // Убираем ссылки из корня
        GC.Collect();         // Теперь GC может удалить byte[]
        GC.WaitForPendingFinalizers();
    }
}

// ─── Утечка через событие ───

public class EventLeakExample
{
    public event EventHandler BigEvent;

    public void SubscribeLeak()
    {
        // Каждая подписка создаёт лямбду, которая захватывает контекст.
        // Делегат BigEvent хранит ссылку на лямбду → ссылку на экземпляр.
        BigEvent += (sender, e) =>
        {
            var data = new byte[1024 * 100]; // 100 КБ «зависает» в куче
        };
    }
}

// ─── Мониторинг памяти ───

public class MemoryMonitor
{
    private List<byte[]> _allocatedMemory = new List<byte[]>();

    public void AllocateMemory(int sizeInMB)
    {
        var data = new byte[sizeInMB * 1024 * 1024];
        _allocatedMemory.Add(data); // Сохраняем ссылку — GC не удалит
    }

    public void AllocateLOHObjects(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var largeObject = new byte[90000 + i]; // > 85 КБ → LOH
        }
        // largeObject — локальная переменная, ссылка сразу теряется.
        // Но в Debug-режиме компилятор может «удерживать» переменную до конца метода.
    }

    public void SimulateBoxing(int count = 1000000)
    {
        var list = new List<object>();
        for (int i = 0; i < count; i++)
        {
            list.Add(i); // int → object (упаковка): каждый Add создаёт объект в куче
        }
    }

    public void PrintMemoryInfo(string message = "")
    {
        if (!string.IsNullOrEmpty(message)) Console.WriteLine($"\n=== {message} ===");
        Console.WriteLine($"Total Memory: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
        Console.WriteLine($"Gen0: {GC.CollectionCount(0)}, Gen1: {GC.CollectionCount(1)}, Gen2: {GC.CollectionCount(2)}");
        using (Process process = Process.GetCurrentProcess())
        {
            Console.WriteLine($"Working Set: {process.WorkingSet64 / (1024 * 1024)} MB");
        }
    }

    public void Cleanup()
    {
        _allocatedMemory.Clear(); // Убираем ссылки
        GC.Collect();             // Принудительная сборка
        GC.WaitForPendingFinalizers();
    }
}

// ─── Точка входа ───

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Задание 1. Базовые примеры утечки памяти ===");

        // Часть 1: статическое vs экземплярное поле
        Console.WriteLine("\nЧасть 1. Наглядная демонстрация: статическая ссылка — ОДНА на все объекты.\n");
        var obj1 = new StaticReferenceDemo();
        var obj2 = new StaticReferenceDemo();
        var obj3 = new StaticReferenceDemo();

        obj1.AddToStatic(100); obj1.AddToInstance(1);
        obj2.AddToStatic(200); obj2.AddToInstance(2);
        obj3.AddToStatic(300); obj3.AddToInstance(3);

        obj1.PrintLists("Объект 1");
        obj2.PrintLists("Объект 2");
        obj3.PrintLists("Объект 3");

        Console.WriteLine("=> Статический список у всех объектов ОДИН, экземплярные списки РАЗНЫЕ.\n");
        StaticFieldDemo.ShowStaticFieldDemo();

        // Часть 2: утечки и замеры
        Console.WriteLine("\nЧасть 2. Проверка утечек: статическая коллекция, событие, LOH, boxing.\n");
        var monitor = new MemoryMonitor();

        Console.WriteLine("1. Выделение памяти (10 МБ + 100 объектов LOH):");
        monitor.AllocateMemory(10);
        monitor.AllocateLOHObjects(100);
        monitor.PrintMemoryInfo("После выделения");

        Console.WriteLine("\n2. Тест утечек:");
        MemoryLeakExample.CreateMemoryLeak();
        var eventExample = new EventLeakExample();
        for (int i = 0; i < 100; i++) eventExample.SubscribeLeak();
        monitor.PrintMemoryInfo("После утечек");

        Console.WriteLine("\n3. Тест упаковки:");
        var sw = Stopwatch.StartNew();
        monitor.SimulateBoxing(1000000);
        sw.Stop();
        Console.WriteLine($"Время с упаковкой (List<object>): {sw.ElapsedMilliseconds}ms");

        sw.Restart();
        var optimizedList = new List<int>();
        for (int i = 0; i < 1000000; i++) optimizedList.Add(i);
        sw.Stop();
        Console.WriteLine($"Время без упаковки (List<int>):  {sw.ElapsedMilliseconds}ms");

        // Часть 3: очистка и поколения
        Console.WriteLine("\n4. После очистки:");
        MemoryLeakExample.ClearMemoryLeak();
        monitor.Cleanup();
        monitor.PrintMemoryInfo("Очищено");

        Console.WriteLine("\n5. Поколения объектов:");
        var obj = new object();
        Console.WriteLine($"Начальное поколение объекта: {GC.GetGeneration(obj)}");
        GC.Collect();
        Console.WriteLine($"После GC.Collect():          {GC.GetGeneration(obj)}");

        Console.WriteLine("\nНажмите любую клавишу для завершения...");
        try { Console.ReadKey(); } catch { }
    }
}
```

---

## Пошаговый разбор

### Часть 1. Статическое vs экземплярное поле

1. Создаём три объекта `StaticReferenceDemo` и добавляем в них значения:
   - `obj1.AddToStatic(100)` — добавляет 100 в `_staticList` (общий для всех)
   - `obj1.AddToInstance(1)` — добавляет 1 в `_instanceList` (только obj1)
   - `obj2.AddToStatic(200)` — добавляет 200 в тот же `_staticList`
   - `obj2.AddToInstance(2)` — добавляет 2 в `_instanceList` obj2

2. Выводим содержимое: видим, что статический список `[100, 200, 300]` у всех трёх объектов один, а экземплярные списки разные.

3. `StaticFieldDemo` показывает то же самое на счётчиках: `staticCounter` растёт через любой объект, `instanceCounter` — только у того, через который вызвали.

**Почему это важно:** Статическое поле — это корень GC. Если положить в статическую коллекцию million объектов по 1 МБ — GC никогда их не удалит, пока коллекция не очищена вручную.

### Часть 2. Создание утечек

1. `monitor.AllocateMemory(10)` — выделяет 10 МБ и сохраняет ссылку в `_allocatedMemory`. Память занята, GC не может удалить.

2. `monitor.AllocateLOHObjects(100)` — создаёт 100 объектов по ~90 КБ. Ссылки сразу теряются (локальная переменная `largeObject` выходит из области видимости). Но в Debug-режиме компилятор может «удерживать» переменную до конца метода, поэтому 일부 объекты могут не собираться.

3. `MemoryLeakExample.CreateMemoryLeak()` — 1000 × 10 КБ = 10 МБ в статической коллекции. Объекты **недостижимы** из кода (нет переменных, ссылающихся на них), но `_staticList` — корень GC, поэтому GC считает их живыми.

4. `EventLeakExample.SubscribeLeak()` × 100 — каждая подписка создаёт лямбду, которая захватывает контекст. Делегат `BigEvent` хранит ссылку на лямбду → ссылку на экземпляр `EventLeakExample`. Даже если удалить все явные ссылки на `eventExample`, GC не сможет его собрать.

5. `SimulateBoxing(1000000)` — `list.Add(i)` упаковывает `int` в `object`. Каждая упаковка создаёт объект в куче (~24 байта: заголовок + значение). 1 000 000 упаковок = ~24 МБ «мусора».

### Часть 3. Очистка и поколения

1. `MemoryLeakExample.ClearMemoryLeak()` — `_staticList.Clear()` убирает ссылки из корня. Теперь GC может удалить все 1000 объектов. `GC.Collect()` запускает сборку.

2. `monitor.Cleanup()` — `_allocatedMemory.Clear()` + `GC.Collect()`. Память освобождается.

3. **Поколения:** новый объект живёт в Gen0. Если он пережил сборку GC — перемещается в Gen1. Если пережил ещё одну — в Gen2. Объекты в Gen2 собираются реже всего.

---

## Результаты выполнения

```
=== Задание 1. Базовые примеры утечки памяти ===

Часть 1. Наглядная демонстрация: статическая ссылка — ОДНА на все объекты.

Объект 1:
  Статический список (общий): [100, 200, 300]
  Экземплярный список (свой): [1]

Объект 2:
  Статический список (общий): [100, 200, 300]
  Экземплярный список (свой): [2]

Объект 3:
  Статический список (общий): [100, 200, 300]
  Экземплярный список (свой): [3]

=> Статический список у всех объектов ОДИН, экземплярные списки РАЗНЫЕ.

После операций:
Объект A:
  Статический счетчик (общий): 3
  Экземплярный счетчик (свой): 2
Объект B:
  Статический счетчик (общий): 3
  Экземплярный счетчик (свой): 1
=> Статический счетчик ОДИН для обоих объектов, экземплярные РАЗНЫЕ.

=== Часть 2 ===
Total Memory: 18 MB (после выделения 10 МБ + LOH)
Total Memory: 28 MB (после утечек: +10 МБ статика + 100 × 100 КБ события)
Время с упаковкой (List<object>): ~112ms
Время без упаковки (List<int>):    ~7ms

=== После очистки ===
Total Memory: 4 MB (всё освобождено)

Поколение объекта: 0 → 1 (после GC.Collect())
```

**Ключевые наблюдения:**
- Память выросла с 18 до 28 МБ после утечек (статическая коллекция + события).
- После `ClearMemoryLeak()` + `Cleanup()` память упала до 4 МБ — утечки устранены.
- Boxing: 112ms vs 7ms — упаковка замедляет работу в ~16 раз.
- Поколение объекта: 0 → 1 после `GC.Collect()`.

---

## Ключевые концепции

### Корни GC (GC Roots)

**Корень** — это любая ссылка, через которую GC может «добраться» до объекта:
- Статические поля классов
- Локальные переменные в активных методах (на стеке вызовов)
- Аргументы методов
- Регистры процессора
- Подписки на события (делегаты)

Объект считается **достижимым** (live), пока существует путь от хотя бы одного корня. Недостижимые объекты удаляются GC.

### Поколения (Generations)

GC разделяет объекты на три поколения для оптимизации:

| Поколение | Что хранится | Как часто собирается |
|-----------|-------------|---------------------|
| **Gen0** | Новые объекты (молодые) | Очень часто (при нехватке памяти) |
| **Gen1** | Объекты, пережившие одну сборку Gen0 | Реже (промежуточное буферизирование) |
| **Gen2** | Объекты, пережившие две сборки | Очень реже (дорогая полная сборка) |

**Логика:**大多数 объекты живут недолго (например,temporary strings, temp buffers). Если объект пережил сборку — скорее всего, он будет жить долго. Поэтому Gen2 собирается реже.

### Large Object Heap (LOH)

Объекты **> 85 000 байт** (~83 КБ) автоматически попадают в LOH — отдельную область кучи. LOH:
- **Не уплотняется** (компактизация не происходит, чтобы не перемещать большие блоки памяти)
- Собирается **только при полной сборке Gen2**
- Может **фрагментироваться** (дыры после удалённых объектов не закрываются)

### Boxing (упаковка)

**Значимые типы** (`int`, `float`, `struct`) хранятся на стеке или inline в объекте. При приведении к `object` (или передаче в обобщённую коллекцию без параметра типа) происходит **упаковка**:

```csharp
int x = 42;
object boxed = x;       // Boxing: создаётся объект в куче (заголовок + значение)
int unboxed = (int)boxed; // Unboxing: извлечение значения из кучи на стек
```

**Почему это проблема:** Каждая упаковка — аллокация в куче. 1 000 000 упаковок = 1 000 000 объектов для GC. `List<int>` хранит значения напрямую, `List<object>` — через упаковку.

---

## Типичные ошибки

1. **«GC сам всё почистит»** — Нет. Если объект удерживается корнем (статическое поле, событие), GC **не может** его удалить, даже если программист больше не использует этот объект.

2. **Забыть отписаться от события** — Самая частая ошибка. Делегат события хранит ссылку на подписчика. Если подписчик — объект, а не статический метод, GC не сможет его собрать.

3. **Хранить большие объекты в `List<object>`** — Каждое добавление значимого типа вызывает упаковку. Используйте обобщённые коллекции (`List<int>`, `Dictionary<string, Value>`).

4. **Считать, что `GC.Collect()` решает проблему** — `GC.Collect()` принудительно запускает сборку, но если объект удерживается корнем, он всё равно не будет удалён.

---

## Практическое применение

- **Кэши:** `static Dictionary<string, HeavyObject>` — если не ограничить размер и не удалять старые записи, память растёт бесконечно. Используйте `MemoryCache` с политикой истечения.
- **События в UI-приложениях:** Подписка на событие от ViewModel в View, но View не отписывается → ViewModel не собирается → утечка памяти. Используйте弱ые события (weak references) или отписывайтесь в `OnClosing`.
- **Boxing в циклах:** `for (int i = 0; i < N; i++) list.Add(i)` на `ArrayList` — классический пример. Всегда используйте `List<T>`.
- **LOH-фрагментация:** В высоконагруженных серверах большие объекты (> 85 КБ) могут фрагментировать LOH, что приводит к `OutOfMemoryException` при наличии достаточного общего объёма памяти.

---

## Итог

1. **Статические коллекции** — корни GC. Объекты в них живут до очистки коллекции.
2. **События** — делегаты хранят ссылки на подписчиков. Без отписки — утечка.
3. **LOH** (> 85 КБ) — не уплотняется, собирается реже. Фрагментация — проблема.
4. **Boxing** — создаёт объекты в куче. Используйте обобщённые коллекции.
5. **`GC.Collect()`** не решает утечки — он только ускоряет сборку уже недостижимых объектов.
