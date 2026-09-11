# Задание 7 — Поиск и исправление утечек в коде (BuggyCode)

**Файл:** `Lab2/Task7/Program.cs`
**Целевая платформа:** .NET 10.0 (SDK 10.0.401)

---

## Что изучаем в этом задании

Это **практическое задание** — найти и исправить утечки памяти в presented коде. Мы применяем знания из Заданий 1–6:
- Статические коллекции (Задание 1, 4)
- События и делегаты (Задание 1, 4)
- `IDisposable` (Задание 2)
- Поколения и LOH (Задание 4, 5)

**Ключевая идея:** В реальном коде утечки редко бывают «одна строка». Обычно они возникают из-за **комбинации** факторов: статическое поле + событие + неосвобождённый ресурс.

---

## Связь с предыдущими заданиями

- **Задание 1** показало базовые утечки (статика, события, LOH, boxing) — по отдельности.
- **Задание 4** исследовало статические ссылки и события — подробно.
- **Задание 7** объединяет **все механизмы** в одном классе и требует **найти и исправить** все утечки.

---

## Исходный код (BuggyCode)

```csharp
public class BuggyCode
{
    private static List<IDisposable> _cache = new List<IDisposable>(); // Утечка 1
    private static event EventHandler? _globalEvent;                    // Утечка 2
    private byte[] _data;                                                // Утечка 3

    public BuggyCode(int dataSizeKB)
    {
        _data = new byte[dataSizeKB * 1024]; // Удерживается, пока жив объект
        _globalEvent += OnEvent;              // Подписка в конструкторе (Утечка 2)
        _cache.Add(new MemoryStream(1024));   // Добавление в кэш (Утечка 1)
    }

    private void OnEvent(object? sender, EventArgs e)
    {
        var temp = new byte[1024 * 100]; // 100 КБ «зависает»
    }

    public static int CacheCount => _cache.Count;

    public static void CreateInstances(int count, int sizeKB)
    {
        for (int i = 0; i < count; i++)
        {
            new BuggyCode(sizeKB); // Утечка 4: ссылки не сохраняются
        }
    }

    public static void FireEvent() => _globalEvent?.Invoke(null, EventArgs.Empty);
}
```

---

## Найденные утечки (4 причины)

### Утечка 1: `static List<IDisposable> _cache`

```csharp
private static List<IDisposable> _cache = new List<IDisposable>();
// ...
_cache.Add(new MemoryStream(1024));
```

**Проблема:** Кэш **растёт без ограничений** и **никогда не очищается**. Каждый вызов конструктора добавляет `MemoryStream` в список. `_cache` — статическое поле (корень GC). Все `MemoryStream` удерживаются до конца работы приложения.

**Почему это утечка:** Даже если удалить все явные ссылки на `BuggyCode`, `_cache` всё ещё ссылается на `MemoryStream`. А если `MemoryStream` реализует `IDisposable`, его нужно **освободить** (вызвать `Dispose()`), иначе утекают неуправляемые ресурсы (native-буферы).

### Утечка 2: `static event _globalEvent`

```csharp
private static event EventHandler? _globalEvent;
// ...
_globalEvent += OnEvent; // Подписка в конструкторе
```

**Проблема:** Конструктор подписывает `OnEvent` на статическое событие. Делегат хранит ссылку на экземпляр `BuggyCode`. Пока подписка существует, объект **недостижим** для GC.

**Цепочка ссылок:**
```
_globalEvent (статическое поле → корень GC)
  → делегат EventHandler
    → OnEvent (метод экземпляра)
      → this (объект BuggyCode)
        → _data (byte[1MB])
        → _cache (List<IDisposable>)
          → MemoryStream (1KB)
```

### Утечка 3: `byte[] _data` (1 МБ)

```csharp
private byte[] _data;
public BuggyCode(int dataSizeKB)
{
    _data = new byte[dataSizeKB * 1024]; // 1 МБ на объект
}
```

**Проблема:** Каждый `BuggyCode` выделяет 1 МБ. Если создано 1000 объектов — 1 ГБ памяти. Объекты удерживаются через `_globalEvent` (Утечка 2) и `_cache` (Утечка 1), поэтому GC не может их удалить.

### Утечка 4: `CreateInstances()` без сохранения ссылок

```csharp
public static void CreateInstances(int count, int sizeKB)
{
    for (int i = 0; i < count; i++)
    {
        new BuggyCode(sizeKB); // Ссылки не сохраняются
    }
}
```

**Проблема:** Ссылки на созданные объекты **не сохраняются**. Казалось бы, объекты должны быть недостижимы и удалены GC. Но:
- Каждый объект подписан на `_globalEvent` (Утечка 2) → удерживается.
- Каждый объект добавляет `MemoryStream` в `_cache` (Утечка 1) → удерживается.

---

## Демонстрация утечки (оригинальный код)

```
=== ОРИГИНАЛЬНЫЙ код (с утечками) ===
Память до создания: 0 MB
Создали 400 экземпляров (без сохранения ссылок).
Элементов в статическом кэше: 400
Память после создания: 100 MB
Прирост: 100 MB
=> Память НЕ освободилась: экземпляры удерживаются статическим событием и кэшем.
```

**Что произошло:**
- 400 объектов × 256 КБ = ~100 МБ.
- `GC.Collect()` вызван, но память **не освободилась** — объекты удерживаются корнями.

---

## Исправления (FixedBuggyCode)

```csharp
public class FixedBuggyCode : IDisposable
{
    private static List<IDisposable> _cache = new List<IDisposable>();
    private static event EventHandler? _globalEvent;
    private byte[] _data;
    private bool _disposed;                        // Флаг идемпотентности

    public static int AliveSubscribers { get; private set; }

    public FixedBuggyCode(int dataSizeKB)
    {
        _data = new byte[dataSizeKB * 1024];
        _globalEvent += OnEvent;
        AliveSubscribers++;
    }

    private void OnEvent(object? sender, EventArgs e)
    {
        var temp = new byte[1024 * 100];
    }

    public static void CreateInstances(int count, int sizeKB)
    {
        for (int i = 0; i < count; i++)
        {
            new FixedBuggyCode(sizeKB);
        }
    }

    public static int CacheCount => _cache.Count;

    public static void FireEvent() => _globalEvent?.Invoke(null, EventArgs.Empty);

    public static void OpenCachedStream()
    {
        _cache.Add(new MemoryStream(1024));
    }

    public static void ClearCache()
    {
        foreach (var item in _cache) item.Dispose(); // Освобождаем каждый MemoryStream
        _cache.Clear();                               // Очищаем список
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _globalEvent -= OnEvent;  // ОТПИСКА от события!
        _disposed = true;
        AliveSubscribers--;
    }
}
```

### Что исправлено

| Утечка | Исправление | Как работает |
|--------|-------------|-------------|
| `_cache` растёт без ограничений | `ClearCache()` вызывает `Dispose()` у каждого элемента и очищает список | Неуправляемые ресурсы освобождаются, ссылки убираются |
| `_globalEvent` удерживает объекты | `Dispose()` отписывает `_globalEvent -= OnEvent` | Ссылка на экземпляр разрывается, GC может удалить |
| `MemoryStream` без `Dispose()` | `ClearCache()` вызывает `item.Dispose()` для каждого элемента | Неуправляемые ресурсы освобождаются |
| `CreateInstances()` без ссылок | Добавлен `IDisposable`, экземпляры сохраняются и освобождаются вручную | Контролируемое время жизни |

---

## Результат исправленной версии

```
=== ИСПРАВЛЕННЫЙ код ===
Память до создания: 0 MB
Память после создания: 200 MB (живых подписчиков: 400)
Память после Dispose всех + ClearCache + GC.Collect: 100 MB
Освобождено: 100 MB, живых подписчиков: 0
=> Исправления: отписка в Dispose(), Dispose() всех элементов кэша и очистка кэша.
```

**Что изменилось:**
- После `Dispose()` всех экземпляров + `ClearCache()` + `GC.Collect()`: **100 МБ освобождены**.
- Живых подписчиков: 0 (все отписались через `Dispose()`).
- Память упала с 200 до 100 МБ.

---

## Ключевые концепции

### Паттерн «отписка в Dispose»

```csharp
public void Dispose()
{
    if (_disposed) return;
    _globalEvent -= OnEvent;  // Ключевая строка
    _disposed = true;
}
```

**Почему это важно:** Событие — корень GC. Если объект подписан на статическое событие и не отписывается при уничтожении — он «бессмертен» (GC не может удалить).

### Идемпотентность `Dispose()`

```csharp
if (_disposed) return; // Защита от повторного вызова
```

**Почему нужна:** `Dispose()` может вызваться несколько раз (явно + через `using`). Повторная отписка от события не должна падать.

### Двойная сборка GC

```csharp
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
```

**Почему дважды:**
- Первая сборка: помечает недостижимые объекты, запускает финализаторы.
- `WaitForPendingFinalizers`: ждёт завершения финализаторов (которые могут создать новые ссылки).
- Вторая сборка: окончательно удаляет объекты.

---

## Типичные ошибки

1. **Подписка в конструкторе без отписки в `Dispose()`** — Классическая утечка. Если объект подписывается на статическое событие в конструкторе, он **обязан** отписаться в `Dispose()`.

2. **Кэш без лимита** — `static List<T>` без ограничения размера растёт бесконечно. Используйте `MemoryCache` с политикой истечения или ограничивайте размер.

3. **Не вызывать `Dispose()` у элементов кэша** — Даже если очистить список, `MemoryStream` не освободит native-ресурсы без `Dispose()`.

4. **Забыть про идемпотентность** — Если `Dispose()` не проверяет флаг `_disposed`, повторный вызов может отписаться от уже несуществующего события.

---

## Практическое применение

- **Репозитории/кэши:** Любой `static Dictionary` или `static List` — потенциальная утечка. Ограничивайте размер, используйте `MemoryCache`.
- **Подписки на глобальные события:** `Logger.OnLog += handler` — если не отписаться, handler живёт вечно. Используйте слабые события (`WeakEventManager`).
- **`IDisposable` + статические события:** Если класс реализует `IDisposable` и подписан на статическое событие — `Dispose()` **обязан** отписаться.
- **Мониторинг:** Регулярно проверяйте `GC.GetTotalMemory()` в long-running приложениях. Рост без причины — сигнал на проверку.

---

## Итог

1. **4 утечки** в `BuggyCode`: статический кэш, статическое событие, неосвобождённые `MemoryStream`, отсутствие `IDisposable`.
2. **Исправление:** `Dispose()` отписывает от события, `ClearCache()` освобождает элементы и очищает список.
3. **Результат:** 100 МБ освобождены, живых подписчиков 0.
4. **Паттерн:** Если подписались на статическое событие в конструкторе — отпишитесь в `Dispose()`.
5. **Идемпотентность:** `Dispose()` должен проверять флаг `_disposed` для безопасного повторного вызова.
