# Задание 2 — Реализация класса MemoryMonitor : IDisposable

**Файл:** `Lab2/Task2/Program.cs`
**Целевая платформа:** .NET 10.0 (SDK 10.0.401)

## Суть задания

По данному в условии тексту написать полный код класса `MemoryMonitor`,
реализующего `IDisposable`, и продемонстрировать его работу.

## Код (основная часть)

```csharp
public class MemoryMonitor : IDisposable
{
    private bool _disposed;
    private List<byte[]> _allocatedMemory = new List<byte[]>();

    public void AllocateMemory(int sizeInMB) { ... }
    public void AllocateLOHObjects(int count) { ... }
    public void SimulateBoxing() { /* List<object>.Add(i) — упаковка */ }
    public void PrintMemoryInfo() { /* GetGeneration, GetTotalMemory, CollectionCount */ }
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
```

## Паттерн Dispose

- `_disposed` — флаг, защищающий от повторной очистки.
- `Dispose()` освобождает управляемую память (`_allocatedMemory.Clear()`) и
  принудительно собирает мусор.
- В Main класс используется через `using`, гарантирующий вызов `Dispose()`.

## Результаты вывода

```
GC Generation: 2
Total Memory: 34 MB
Collection Count Gen0: 6  Gen1: 4  Gen2: 2
Повторный Dispose() выполнен без исключений.
```

## Вывод

Качественная реализация `IDisposable` обязана быть *идемпотентной*: повторный
`Dispose()` не должен вызывать исключения и повторную работу. Флаг `_disposed`
обеспечивает это.