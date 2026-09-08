# Сессия 01 — Выполнение лабораторной работы №2 «Управление памятью в C# и анализ утечек»

## Дата
09 сентября 2026

## Среда
- .NET SDK: **10.0.401** (установлен в `~/.dotnet`, системный 8.0.130 не изменён).
- Прогон команд: `~/.dotnet/dotnet build` / `~/.dotnet/dotnet run` в каждом проекте.

## Поставленные задачи

1. Проанализировать оба файла условия (`.docx`) и проект-референс `damp_heap`.
2. Установить .NET 10 SDK (условие требует net10.0).
3. Реализовать Задания 1–7 в отдельных проектах `Lab2/Task1..Task7`.
4. Реализовать инструменты анализа кучи: `Lab2/DumpTool` (создание снимков)
   и `Lab2/DumpReader` (чтение/сравнение снимков) на NuGet-пакетах
   `Microsoft.Diagnostics.NETCore.Client` и `Microsoft.Diagnostics.Runtime`.
5. Для каждого проекта выполнить `build` + `run` и зафиксировать вывод.
6. Создать отчётность: `explanation/task-1..7.md`, `session-01.md`,
   `testing-guide.md`, `answers.md`.

## Разбор заданий и результаты

### Задание 1 — `Lab2/Task1` (базовые утечки)

Написан полный код: `StaticReferenceDemo`, `StaticFieldDemo`,
`MemoryLeakExample`, `EventLeakExample`, `MemoryMonitor` + 5-этапный тест.

Ключевой вывод (фрагмент):
```
Total Memory: 18 MB (после выделения) → 28 MB (после утечек) → 4 MB (после очистки)
Время с упаковкой: 112ms, без упаковки: 7ms
Поколение объекта: 0 → после GC.Collect(): 1
```
**Вывод:** статическая коллекция и подписка на события удерживают объекты
(корневые ссылки); `GC.Collect()` без очистки корней не освобождает память.

---

### Задание 2 — `Lab2/Task2` (MemoryMonitor : IDisposable)

Полный класс `MemoryMonitor : IDisposable` (паттерн Dispose, флаг `_disposed`,
идемпотентный `Dispose()`). Демонстрация через `using`.
```
GC Generation: 2, Total Memory: 34 MB
Collection Count Gen0: 6, Gen1: 4, Gen2: 2
Повторный Dispose() выполнен без исключений.
```

---

### Задание 3 — `Lab2/Task3` (тестирование и анализ)

Main по скелету условия; `using`-блок, 5 этапов.
```
С упаковкой (List<object>): 80ms
Без упаковки (List<int>):    7ms
После принудительной сборки: Total Memory: 0 MB
```

---

### Инструменты анализа — `Lab2/DumpTool`, `Lab2/DumpReader`

**DumpTool** (`Microsoft.Diagnostics.NETCore.Client 0.2.731102`):
`DiagnosticsClient.WriteDump(DumpType.WithHeap, ...)` в двух точках программы —
до и после выделения массива 10 МБ + 50 LOH-объектов:

```
heap_dump_stage1_20260909_004112.dump: 103372.00 KB
heap_dump_stage2_20260909_004113.dump: 127428.00 KB
```

**DumpReader** (`Microsoft.Diagnostics.Runtime 3.1.512801`):
`DataTarget.LoadDump` → `ClrRuntime` → `ClrHeap.EnumerateObjects()` (ClrObject).
Выводит живую кучу, корни GC и ТОП-20 типов; второй аргумент включает сравнение.

Одиночный анализ:
```
Живая куча: 2.19 MB, Корни GC: 93, Всего типов: 265
System.Byte[]  244  шт.  1.99 MB
System.String  938  шт.  0.11 MB
```

Сравнение двух snapshots:
```
Живая куча: дамп1 = 2.19 MB, дамп2 = 16.97 MB, разница = +14 MB
System.Byte[]: 244 -> 291 (1 -> 16 MB)   <- добавленные массив и LOH-объекты
Типов появилось: 6, исчезло: 9
```

**Замечание по API:** в условии `DataTarget.LoadCrashDump` / `heap.TotalHeapSize`
(ClrMD 2.x). В ClrMD 3.1 эти члены называются `DataTarget.LoadDump` и
`ClrObject.Size`. Логика анализа сохранена. Эталонные дампы из `damp_heap`
(Windows) под Linux ClrMD не распознаёт — для надёжности DumpReader выдаёт
сообщение «CLR не обнаружена»; демонстрация идёт на собственных дампах DumpTool.

---

### Задание 4 — `Lab2/Task4` (статические ссылки и утечки)

- Статическая утечка: после `AddData()` (1000×10 КБ) память 9 МБ и `GC.Collect()`
  **не освобождает** её; после `Reset()` — 0 МБ.
- Событийная утечка: 100 подписок по 256 КБ → 25 МБ; после отписки → 0 МБ.
- Поколения: объект 0 → 1 → 2 → 2; >85 КБ сразу в LOH (поколение 2).

---

### Задание 5 — `Lab2/Task5` (LOH)

100 массивов по 100 КБ: память 0 → 9 МБ → после `GC.Collect()` 9 МБ (LOH не
сжимается). 90 КБ → Gen2/LOH, 80 КБ → Gen1. Мониторинг: 10/50 КБ мигрируют по
поколениям, 90/200 КБ остаются в Gen2 (LOH).

---

### Задание 6 — `Lab2/Task6` (boxing elimination)

| Вариант (1 000 000 операций) | Время, мс | Куча, МБ |
|---|---|---|
| List\<object\> (упаковка) | 82.4 | 30.9 |
| List\<int\> (без упаковки) | 5.0 | 4.0 |

Ускорение ~16x. `ArrayList → List<int>`: добавление 9.0x быстрее, GetSum 2.2x
быстрее (все переменные интерпретаций при повторных замеров находятся в диапазоне
8–15x / 2–3x). Причины: упаковка/распаковка создают объекты кучи и нагружают GC.

---

### Задание 7 — `Lab2/Task7` (поиск утечек в BuggyCode)

Найдены и объяснены 4 утечки: статический кэш, статическое событие (нет отписки),
неосвобождённые `MemoryStream`, «гранитные» экземпляры из `CreateInstances`.
Исправления: отписка в `Dispose()`, `Dispose()` всех элементов кэша, очистка кэша.

Демонстрация (400×256 КБ из-за ограничений памяти; по условию 1000×1 МБ):
```
Оригинал: создали 400 → память 100 MB, GC.Collect() не освобождает.
Исправление: after Dispose + ClearCache + GC.Collect → освобождено 99 MB,
             живых подписчиков: 0.
```

---

## Выполненные команды

```bash
# Установка .NET 10 SDK
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/opencode/dotnet-install.sh
bash /tmp/opencode/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
"$HOME/.dotnet/dotnet" --list-sdks          # → 10.0.401

# По каждому проекту:
#   cd Lab2/TaskN && ~/.dotnet/dotnet build && ~/.dotnet/dotnet run
# DumpTool дополнительно: пакеты
#   dotnet add package Microsoft.Diagnostics.NETCore.Client 0.2.731102
# DumpReader:
#   dotnet add package Microsoft.Diagnostics.Runtime 3.1.512801
# DumpReader работает с дампами:
#   ~/.dotnet/dotnet run -- <дамп1> [<дамп2>]
```

Все проекты собираются с `0 Warning(s)`, `0 Error(s)`.

## Результаты и выводы

- Доступные в локальной среде SDK 8.0 не поддерживают net10.0 — установлен
  изолированный SDK 10.0.401; системный SDK не изменён.
- Реализованы все 7 заданий + 2 инструмента профилирования. Все демонстрации
  подтверждают теорию: утечки = корневые ссылки (статика, события); LOH не
  компактизируется; упаковка сильно замедляет работу и «раздувает» кучу.
- Сравнение двух snapshots кучи (DumpTool → DumpReader) наглядно показывает
  рост `System.Byte[]` между точками программы.
- Следующий шаг: подготовка `testing-guide.md` и `answers.md` для сдачи.