# Задание 2 — Реализация класса MemoryMonitor : IDisposable

**Файл:** `Lab2/Task2/Program.cs`
**Целевая платформа:** .NET 10.0 (SDK 10.0.401)

---

## Что изучаем в этом задании

Интерфейс `IDisposable` — стандартный механизм .NET для **детерминированного** освобождения ресурсов. В отличие от GC, который удаляет объекты «когда захочет», `Dispose()` позволяет программисту явно сказать: «этот объект больше не нужен, освободи ресурсы прямо сейчас».

**Зачем это нужно:**
- Управляемые ресурсы (в куче) GC освободит сам.
- Неуправляемые ресурсы (файловые дескрипторы, TCP-соединения, GDI+ кисти) — GC **не знает**, как их закрыть. Нужно вызвать `Dispose()` вручную.
- Паттерн `using` гарантирует вызов `Dispose()` даже при исключении.

В этом задании мы реализуем класс `MemoryMonitor`, который выделяет память, мониторит её состояние и корректно освобождает ресурсы через `IDisposable`.

---

## Связь с предыдущими заданиями

В Задании 1 мы увидели, **как возникают утечки** (статические коллекции, события, boxing). Теперь мы создаём инструмент, который:
- Выделяет память осознанно (чтобы потом измерить).
- Мониторит состояние кучи (поколения, счётчики сборок).
- Корректно освобождает память через `Dispose()`.

Этот класс будет переиспользован в Задании 3 для тестирования.

---

## Полный код

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;

public class MemoryMonitor : IDisposable
{
    private bool _disposed;                            // Флаг: был ли вызван Dispose()
    private List<byte[]> _allocatedMemory = new List<byte[]>(); // Хранилище выделенной памяти
    private Random _random = new Random();

    // ─── Выделение памяти в обычной куче (SOH) ───

    public void AllocateMemory(int sizeInMB)
    {
        var data = new byte[sizeInMB * 1024 * 1024]; // Создаём массив
        _allocatedMemory.Add(data);                   // Сохраняем ссылку — GC не удалит
    }

    // ─── Выделение объектов в LOH (> 85 КБ) ───

    public void AllocateLOHObjects(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var largeObject = new byte[90000 + i]; // Каждый объект > 85 КБ
        }
        // largeObject — локальная переменная. Ссылка теряется сразу после итерации.
        // Но в Debug-режиме компилятор может «удерживать» переменную до конца метода.
    }

    // ─── Демонстрация boxing ───

    public void SimulateBoxing()
    {
        var list = new List<object>();
        for (int i = 0; i < 1000000; i++)
        {
            list.Add(i); // int → object (упаковка): каждый Add создаёт объект в куче
        }
    }

    // ─── Мониторинг состояния кучи ───

    public void PrintMemoryInfo()
    {
        Console.WriteLine($"GC Generation: {GC.GetGeneration(this)}");
        Console.WriteLine($"Total Memory: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
        Console.WriteLine($"Collection Count Gen0: {GC.CollectionCount(0)}");
        Console.WriteLine($"Collection Count Gen1: {GC.CollectionCount(1)}");
        Console.WriteLine($"Collection Count Gen2: {GC.CollectionCount(2)}");
    }

    // ─── Очистка памяти ───

    public void Cleanup()
    {
        _allocatedMemory.Clear(); // Убираем ссылки из списка
        GC.Collect();             // Принудительная сборка мусора
        GC.WaitForPendingFinalizers(); // Ждём завершения финализаторов
    }

    // ─── IDisposable: детерминированное освобождение ───

    public void Dispose()
    {
        if (!_disposed)   // Защита от повторного вызова (идемпотентность)
        {
            Cleanup();
            _disposed = true;
        }
    }
}

class Program
{
    static void Main()
    {
        Console.WriteLine("=== Задание 2. MemoryMonitor : IDisposable ===");

        // using-блок гарантирует вызов Dispose() даже при исключении
        using (var monitor = new MemoryMonitor())
        {
            Console.WriteLine("\nШаг 1. Выделяем 4 МБ и 10 объектов LOH (> 85 KБ).");
            monitor.AllocateMemory(4);      // 4 МБ в SOH
            monitor.AllocateLOHObjects(10); // 10 объектов > 85 КБ в LOH

            Console.WriteLine("Шаг 2. Упаковка: 1 000 000 значений int в List<object>.");
            monitor.SimulateBoxing();       // 1M упаковок → нагрузка на GC

            Console.WriteLine("\nШаг 3. Информация о памяти до очистки:");
            monitor.PrintMemoryInfo();      // Выводим Generation, TotalMemory, CollectionCount

            Console.WriteLine("\nШаг 4. Выходим из using — будет вызван Dispose(), память очищена.");
        } // ← Здесь вызывается monitor.Dispose()

        // Проверка идемпотентности: повторный Dispose() не должен падать
        Console.WriteLine("\nПроверка повторного вызова Dispose() (должен быть безопасным):");
        var second = new MemoryMonitor();
        second.Dispose();  // Первый вызов — очищает
        second.Dispose();  // Второй вызов — ничего не делает (флаг _disposed = true)
        Console.WriteLine("Повторный Dispose() выполнен без исключений.");

        Process process = Process.GetCurrentProcess();
        Console.WriteLine($"Working Set: {process.WorkingSet64 / (1024 * 1024)} MB");
    }
}
```

---

## Пошаговый разбор

### Создание объекта

```csharp
using (var monitor = new MemoryMonitor())
```

`using`-блок — это «синтаксический сахар». Компилятор преобразует его в:

```csharp
var monitor = new MemoryMonitor();
try
{
    // тело using
}
finally
{
    if (monitor != null) ((IDisposable)monitor).Dispose();
}
```

**Гарантия:** `Dispose()` вызовется **всегда** — даже если внутри `using` произойдёт исключение.

### Шаг 1. Выделение памяти

```csharp
monitor.AllocateMemory(4);
```

Создаётся `byte[4 * 1024 * 1024]` (4 МБ) и сохраняется в `_allocatedMemory`. Ссылка на массив есть — GC не может его удалить. Память физически занята.

```csharp
monitor.AllocateLOHObjects(10);
```

10 объектов по ~90 КБ каждый. Все > 85 КБ → попадают в LOH. Ссылки на них **не сохраняются** (локальная переменная `largeObject` выходит из области видимости). Но в Debug-режиме компилятор может «удерживать» переменную до конца метода.

### Шаг 2. Boxing

```csharp
monitor.SimulateBoxing();
```

1 000 000 раз `list.Add(i)` упаковывает `int` в `object`. Каждая упаковка:
- Выделяет объект в куче (~24 байта: заголовок + 4 байта значения)
- Нагружает GC (нужно потом удалить 1M объектов)
- Замедляет работу (аллокация + последующая сборка)

### Шаг 3. Мониторинг

```csharp
monitor.PrintMemoryInfo();
```

Выводит:
- `GC.GetGeneration(this)` — поколение объекта `MemoryMonitor` (обычно Gen0 или Gen1)
- `GC.GetTotalMemory(false)` — текущий размер управляемой кучи (без принудительной сборки)
- `GC.CollectionCount(0/1/2)` — сколько раз GC собирал каждое поколение

### Шаг 4. Очистка

```csharp
} // ← Вызов Dispose()
```

При выходе из `using`-блока вызывается `Dispose()`:
1. Проверяется флаг `_disposed` (первый вызов → `false`)
2. Вызывается `Cleanup()`: `_allocatedMemory.Clear()` + `GC.Collect()`
3. Флаг `_disposed = true`

При **повторном** вызове `Dispose()`: флаг `_disposed = true` → `Cleanup()` не вызывается → исключений нет.

---

## Результаты выполнения

```
=== Задание 2. MemoryMonitor : IDisposable ===

Шаг 1. Выделяем 4 МБ и 10 объектов LOH (> 85 KБ).
Шаг 2. Упаковка: 1 000 000 значений int в List<object>.

Шаг 3. Информация о памяти до очистки:
GC Generation: 2
Total Memory: 34 MB
Collection Count Gen0: 6
Collection Count Gen1: 4
Collection Count Gen2: 2

Шаг 4. Выходим из using — будет вызван Dispose(), память очищена.

Проверка повторного вызова Dispose() (должен быть безопасным):
Повторный Dispose() выполнен без исключений.
Working Set: 34 MB
```

**Ключевые наблюдения:**
- `GC Generation: 2` — объект `MemoryMonitor` пережил достаточно сборок, чтобы попасть в Gen2.
- `Total Memory: 34 MB` — 4 МБ (SOH) + ~900 КБ (LOH) + ~24 МБ (boxing) + служебные структуры.
- `Collection Count Gen0: 6, Gen1: 4, Gen2: 2` — GC работал активно из-за большого количества аллокаций.
- Повторный `Dispose()` не вызвал исключение — идемпотентность работает.

---

## Ключевые концепции

### Паттерн Dispose

Стандартный паттерн в .NET:

```csharp
public class MyClass : IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)        // 1. Защита от повторного вызова
        {
            CleanupResources(); // 2. Освобождение ресурсов
            _disposed = true;   // 3. Помечаем как освобождённый
        }
    }
}
```

**Почему идемпотентность важна:** Если `Dispose()` вызовется дважды (а `using` + явный вызов — частый сценарий), повторная очистка не должна падать или освобождать уже освобождённые ресурсы.

### using vs IDisposable

| Способ | Когда использовать |
|--------|-------------------|
| `using (var x = ...) { }` | Краткосрочное использование. Гарантия `Dispose()` при выходе. |
| `var x = ...; try { } finally { x.Dispose(); }` | Когда нужно контролировать время жизни. |
| `var x = ...; x.Dispose();` | Ручное управление. Забыть вызвать — утечка. |

**Рекомендация:** Всегда используйте `using`, если класс реализует `IDisposable`.

### Три типа ресурсов

1. **Управляемые** (в куче) — GC освободит сам. Пример: `byte[]`, `List<T>`.
2. **Неуправляемые** (native) — GC не знает, как закрыть. Пример: файловые дескрипторы, TCP-сокеты.
3. **Управляемые с неуправляемыми** — требуют `Dispose()`. Пример: `StreamReader` (держит файловый дескриптор).

### GC.GetTotalMemory vs Working Set

| Метрика | Что измеряет |
|---------|-------------|
| `GC.GetTotalMemory()` | Размер **управляемой кучи** (объекты .NET) |
| `Process.WorkingSet64` | **Общий объём памяти** процесса в ОС (управляемая + неуправляемая + служебная) |

---

## Типичные ошибки

1. **Забыть `using` или `Dispose()`** — Если класс реализует `IDisposable`, но вы не вызываете `Dispose()`, ресурсы освободятся только при финализации (когда GC «дойдёт» до объекта). Для неуправляемых ресурсов это может означать утечку файловых дескрипторов или соединений.

2. **Вызывать `Dispose()` дважды без идемпотентности** — Если `Dispose()` не проверяет флаг `_disposed`, повторный вызов может освободить уже освобождённые ресурсы → краш.

3. **Вызывать `Dispose()` на объекте, который ещё используется** — После `Dispose()` объект «мёртв». Любая операция с ним может выбросить `ObjectDisposedException`.

4. **Считать, что `Dispose()` = удаление из памяти** — `Dispose()` освобождает **ресурсы** (файлы, соединения), но сам объект остаётся в куче до тех пор, пока GC не решит его удалить.

---

## Практическое применение

- **Потоки и соединения:** `StreamReader`, `StreamWriter`, `SqlConnection`, `HttpClient` — все требуют `Dispose()` для закрытия дескрипторов.
- **Временные ресурсы:** `Stopwatch`, `SemaphoreSlim`, `ManualResetEventSlim` — используют `Dispose()` для освобождения системных ресурсов.
- **Паттерн « sở hữu ресурс»:** Класс, который открывает файл/соединение в конструкторе и закрывает в `Dispose()`. `using` гарантирует закрытие.
- **Оптимизация GC:** Если вы знаете, что объект больше не нужен, вызовите `Dispose()` немедленно, не дожидаясь GC.

---

## Итог

1. `IDisposable` — стандартный механизм детерминированного освобождения ресурсов в .NET.
2. `using`-блок гарантирует вызов `Dispose()` даже при исключении.
3. Паттерн Dispose: проверка флага `_disposed` → очистка → установка флага. Идемпотентность обязательна.
4. `Dispose()` освобождает **ресурсы**, но не удаляет объект из памяти (это делает GC).
5. Всегда используйте `using` для классов, реализующих `IDisposable`.
