# Задание 3 — Тестирование и анализ + Memory Profiler (снапшоты кучи)

**Файлы:**
- `Lab2/Task3/Program.cs` — тестирование/анализ `MemoryMonitor`
- `Lab2/DumpTool/Program.cs` — создание снимков кучи (дампов)
- `Lab2/DumpReader/Program.cs` — чтение и сравнение снимков
**Целевая платформа:** .NET 10.0 (SDK 10.0.401)

---

## Что изучаем в этом задании

Мы изучаем **два инструмента** анализа памяти:

1. **Программный мониторинг** (Task3) — измерение памяти «на лету» через `GC.GetTotalMemory()`, `GC.GetGeneration()`, `GC.CollectionCount()`. Позволяет видеть动态 изменения, но не показывает, **какие именно объекты** занимают память.

2. **Анализ дампов** (DumpTool + DumpReader) — создание «снимка» кучи в конкретный момент времени и последующий offline-анализ: какие типы, сколько объектов, какой размер. Это стандартный подход в реальном профилировании.

**Ключевая идея:** Сравнение двух снимков (snapshot comparison) — базовый приём для обнаружения утечек. Если между снимками появился тип, которого раньше не было, или количество объектов типа выросло — это кандидат на утечку.

---

## Связь с предыдущими заданиями

В Задании 2 мы реализовали `MemoryMonitor : IDisposable` — класс, который выделяет память, мониторит и освобождает. Теперь мы:
- **Тестируем** этот класс (Task3): запускаем выделение → сборку → измерение.
- **Создаём дампы** (DumpTool): фиксируем состояние кучи в двух точках программы.
- **Анализируем дампы** (DumpReader): читаем, какие типы и сколько памяти занимают.

---

## Часть 1. Тестирование и анализ (Task3)

### Полный код

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;

public class MemoryMonitor : IDisposable
{
    private bool _disposed;
    private List<byte[]> _allocatedMemory = new List<byte[]>();

    public void AllocateMemory(int sizeInMB)
    {
        var data = new byte[sizeInMB * 1024 * 1024];
        _allocatedMemory.Add(data);
    }

    public void AllocateLOHObjects(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var largeObject = new byte[90000 + i]; // > 85 КБ → LOH
        }
    }

    public void SimulateBoxing()
    {
        var list = new List<object>();
        for (int i = 0; i < 1000000; i++)
        {
            list.Add(i); // int → object (упаковка)
        }
    }

    public void PrintMemoryInfo(string message = "")
    {
        if (!string.IsNullOrEmpty(message)) Console.WriteLine($"--- {message} ---");
        Console.WriteLine($"GC Generation (this): {GC.GetGeneration(this)}");
        Console.WriteLine($"Total Memory: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
        Console.WriteLine($"Collection Count Gen0: {GC.CollectionCount(0)}");
        Console.WriteLine($"Collection Count Gen1: {GC.CollectionCount(1)}");
        Console.WriteLine($"Collection Count Gen2: {GC.CollectionCount(2)}");
    }

    public void Cleanup()
    {
        _allocatedMemory.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public void Dispose()
    {
        if (!_disposed) { Cleanup(); _disposed = true; }
    }
}

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Задание 3. Тестирование и анализ управления памятью ===");

        using (var monitor = new MemoryMonitor())
        {
            Console.WriteLine("\n1. Базовое выделение памяти:");
            monitor.AllocateMemory(10);           // 10 МБ в SOH
            monitor.PrintMemoryInfo();

            Console.WriteLine("\n2. Создание объектов в LOH:");
            monitor.AllocateLOHObjects(100);      // 100 × ~90 КБ в LOH
            monitor.PrintMemoryInfo();

            Console.WriteLine("\n3. Тест упаковки:");
            var stopwatch = Stopwatch.StartNew();
            monitor.SimulateBoxing();             // 1M упаковок
            stopwatch.Stop();
            Console.WriteLine($"Время выполнения с упаковкой (List<object>): {stopwatch.ElapsedMilliseconds}ms");

            Console.WriteLine("\n4. После принудительной сборки:");
            monitor.Cleanup();                    // Очистка + GC.Collect()
            monitor.PrintMemoryInfo();

            Console.WriteLine("\n5. Оптимизированная версия (без упаковки):");
            stopwatch.Restart();
            var optimizedList = new List<int>();
            for (int i = 0; i < 1000000; i++)
            {
                optimizedList.Add(i);             // Без упаковки
            }
            stopwatch.Stop();
            Console.WriteLine($"Время выполнения без упаковки (List<int>):  {stopwatch.ElapsedMilliseconds}ms");
        }

        Console.WriteLine("\nАнализ: варианты с упаковкой и без упаковки; после Cleanup() память");
        Console.WriteLine("освобождается, выборки по поколениям показывают работу сборщика мусора.");

        Console.WriteLine("\nНажмите любую клавишу для завершения...");
        try { Console.ReadKey(); } catch { }
    }
}
```

### Пошаговый разбор

| Шаг | Что происходит | Что измеряем |
|-----|---------------|-------------|
| 1 | `AllocateMemory(10)` — 10 МБ в SOH | `Total Memory` растёт на ~10 МБ |
| 2 | `AllocateLOHObjects(100)` — 100 × ~90 КБ | `Total Memory` растёт ещё на ~9 МБ |
| 3 | `SimulateBoxing()` — 1M упаковок | Замер времени: ~80-120 мс |
| 4 | `Cleanup()` — `_allocatedMemory.Clear()` + `GC.Collect()` | `Total Memory` падает до 0 МБ |
| 5 | `List<int>.Add(i)` — без упаковки | Замер времени: ~5-10 мс |

### Результаты

```
1. Базовое выделение памяти:
GC Generation (this): 2
Total Memory: 10 MB

2. Создание объектов в LOH:
Total Memory: 19 MB

3. Тест упаковки:
Время выполнения с упаковкой (List<object>): 84ms

4. После принудительной сборки:
Total Memory: 0 MB

5. Оптимизированная версия (без упаковки):
Время выполнения без упаковки (List<int>): 7ms
```

**Ключевые наблюдения:**
- `Total Memory: 0 MB` после `Cleanup()` — все ссылки удалены, GC собрал объекты.
- `84ms vs 7ms` — упаковка замедляет в ~12 раз.
- `GC Generation (this): 2` — объект `MemoryMonitor` пережил несколько сборок.

---

## Часть 2. Memory Profiler (DumpTool + DumpReader)

### Зачем нужны дампы

Программный мониторинг (Task3) показывает **общий объём** памяти, но не говорит, **какие типы** её занимают. Для этого нужны **снимки кучи** (heap dumps) — «фотография» кучи в конкретный момент.

**Алгоритм:**
1. Создаём снимок **до** выделения (DumpTool).
2. Выделяем память.
3. Создаём снимок **после** выделения (DumpTool).
4. Анализируем оба снимка (DumpReader): какие типы выросли, сколько нового памяти.

### DumpTool — создание снимков

```csharp
using Microsoft.Diagnostics.NETCore.Client;

// Получаем ID текущего процесса
int processId = System.Diagnostics.Process.GetCurrentProcess().Id;
var client = new DiagnosticsClient(processId);

// Снимок 1: ДО выделения
var stage1 = new List<byte[]>();
for (int i = 0; i < 200; i++) stage1.Add(new byte[1024 * 10]); // 200 × 10 КБ
client.WriteDump(DumpType.WithHeap, dumpPath1, logDumpGeneration: true);

// Снимок 2: ПОСЛЕ выделения
var bigArray = new byte[10 * 1024 * 1024]; // 10 МБ
var lohObjects = new List<byte[]>();
for (int i = 0; i < 50; i++) lohObjects.Add(new byte[100000 + i]); // 50 × ~100 КБ
client.WriteDump(DumpType.WithHeap, dumpPath2, logDumpGeneration: true);
```

**Ключевые моменты:**
- `DiagnosticsClient` — клиент API для диагностики .NET-процессов.
- `DumpType.WithHeap` — создаёт дамп **с кучей** (включает все управляемые объекты).
- `WriteDump` — записывает дамп в файл (формат, понятный ClrMD).
- Дампы сохраняются в `bin/Debug/net10.0/Dumps/`.

### DumpReader — анализ одного дампа

```csharp
using Microsoft.Diagnostics.Runtime;

// Загружаем дамп
using var dt = DataTarget.LoadDump(dumpPath);
var runtime = dt.ClrVersions.First().CreateRuntime();
var heap = runtime.Heap;

// Перебираем все объекты кучи
foreach (var obj in heap.EnumerateObjects())
{
    if (!obj.IsValid || obj.IsFree) continue;
    string typeName = obj.Type?.Name ?? "Unknown";
    ulong size = obj.Size;
    // ... собираем статистику
}
```

**Ключевые моменты:**
- `DataTarget.LoadDump()` — загружает дамп из файла (аналог `LoadCrashDump` в старых версиях ClrMD).
- `ClrVersions.First().CreateRuntime()` — создаёт runtime для работы с кучей.
- `heap.EnumerateObjects()` — перебирает **все объекты** в куче.
- `obj.Type.Name` — имя типа (например, `System.Byte[]`, `System.String`).
- `obj.Size` — размер объекта в байтах.

**Вывод:** ТОП-20 типов по занимаемой памяти:

```
Тип                                              Кол-во      Размер
---------------------------------------------------------------------------
System.Byte[]                                       150     16.20 MB
System.String                                       320      0.45 MB
System.Object[]                                      85      0.12 MB
...
```

### DumpReader — сравнение двух дампов

```csharp
var s1 = CollectStats(first);  // Статистика дампа 1
var s2 = CollectStats(second); // Статистика дампа 2

// Вычисляем разницу для каждого типа
foreach (var t in allTypes)
{
    s1.TryGetValue(t, out var a);
    s2.TryGetValue(t, out var b);
    long sizeDelta = (long)(b.Size - a.Size);
    // Если sizeDelta > 0 — тип вырос (кандидат на утечку)
}
```

**Результат сравнения:**

```
Живая куча: дамп1 = 2.19 MB, дамп2 = 16.97 MB, разница = +14 MB
ТОП-10 типов с наибольшим ростом размера:
Тип                                              Кол-во (1 -> 2)     Размер (1 -> 2)
---------------------------------------------------------------------------
System.Byte[]                                         1 -> 51          0 -> 16 MB
```

**Интерпретация:**
- `System.Byte[]` вырос с 1 до 51 объекта, размер с 0 до 16 МБ.
- Причина: мы выделили `byte[10 МБ]` + 50 × `byte[~100 КБ]`.
- Если бы этот тип рос без нашей инициативы — это была бы утечка.

---

## Ключевые концепции

### Снимки кучи (Heap Dumps)

**Снимок** — это «фотография» состояния кучи в конкретный момент. Содержит:
- Все управляемые объекты (тип, размер, поколение)
- Корни GC (откуда идут ссылки)
- Сегменты кучи (SOH, LOH)

**Когда делать снимки:**
- До и после операции, которая может вызвать утечку.
- До и после `GC.Collect()` для проверки эффективности сборки.
- В «нормальном» состоянии и при «утечке» для сравнения.

### Сравнение снимков (Diff Analysis)

Это **базовый приём** профилирования:

| Тип | Дамп 1 | Дамп 2 | Дельта | Вывод |
|-----|--------|--------|--------|-------|
| `System.Byte[]` | 1 объект, 0 МБ | 51 объект, 16 МБ | +16 МБ | Мы выделили массивы |
| `System.String` | 100, 0.1 МБ | 100, 0.1 МБ | 0 | Стабильно, утечки нет |
| `MyClass` | 0, 0 МБ | 50, 5 МБ | +5 МБ | Кандидат на утечку |

### API ClrMD (Microsoft.Diagnostics.Runtime)

| Метод/Свойство | Назначение |
|---------------|-----------|
| `DataTarget.LoadDump(path)` | Загрузить дамп из файла |
| `runtime.Heap` | Получить объект кучи |
| `heap.EnumerateObjects()` | Перебрать все объекты |
| `obj.Type.Name` | Имя типа объекта |
| `obj.Size` | Размер объекта |
| `heap.EnumerateRoots()` | Перебрать корни GC |

> **Примечание по API:** В условии лабораторной работы используется `DataTarget.LoadCrashDump` (ClrMD 2.x). В актуальной версии ClrMD 3.1 метод называется `DataTarget.LoadDump`. Также `heap.TotalHeapSize` заменён на ручной подсчёт через `EnumerateObjects()`, а `ClrType.GetSize()` — на `ClrObject.Size`.

---

## Типичные ошибки

1. **Считать снимки «на лету»** — `GC.GetTotalMemory()` не показывает, **какие типы** занимают память. Нужен DumpReader.

2. **Сравнивать снимки без привязки к коду** — Если вы не знаете, что происходило между снимками, рост `System.Byte[]` может быть и нормальным явлением, и утечкой. Всегда фиксируйте контекст.

3. **Использовать дампы из другого окружения** — Дамп, созданный на Windows, может не читаться ClrMD на Linux (CLR-версии различаются). Используйте дампы, созданные на той же платформе.

4. **Забыть `DumpType.WithHeap`** — Если создать дамп без кучи (`DumpType.Normal`), в нём не будет управляемых объектов — анализ бесполезен.

---

## Практическое применение

- **Production-профилирование:** В реальных серверах делают дампы через `dotnet-dump` или `procdump` при росте памяти, затем анализируют через `dotnet-dump analyze` или ClrMD.
- **Обнаружение утечек:** Сравнивают дамп «нормального» состояния и дамп «после нескольких часов работы». Типы, которые растут — кандидаты на утечки.
- **Анализ LOH-фрагментации:** DumpReader показывает размеры сегментов LOH и распределение объектов.
- **Автоматизация:** ClrMD позволяет писать скрипты анализа дампов (например, «найти все строки > 10 КБ» или «показать объекты, удерживаемые статическими полями»).

---

## Итог

1. **Программный мониторинг** (`GC.GetTotalMemory`, `GetGeneration`) — показывает динамику, но не детали.
2. **Дампы кучи** (DumpTool) — «фотография» кучи в конкретный момент.
3. **Анализ дампов** (DumpReader) — ТОП-20 типов, размеры, количество объектов.
4. **Сравнение двух снимков** — базовый приём обнаружения утечек: рост типа = кандидат.
5. **ClrMD 3.1** отличается от условия: `LoadCrashDump` → `LoadDump`, ручной подсчёт размера.
