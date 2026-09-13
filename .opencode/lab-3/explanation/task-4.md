# Задание 4 — «Чёрный ящик»: закрытые члены класса

**Файл:** `Lab3/Task4/Program.cs` · **Платформа:** .NET 10.0
**Статус сборки/запуска:** 0 Warning(s) / 0 Error(s)

---

## 1. Что изучаем

Класс устроен как **«чёрный ящик»**: наружу виден лишь небольшой публичный
интерфейс, а внутреннее состояние (`private`/`private readonly` поля) и
алгоритмы (`private` метод) спрятаны. Требование условия:

> класс с **приватным полем**, **публичным методом чтения** этого поля и
> **приватным методом**, реализующим логику вычислений.

Задача программы — подтвердить средствами рефлексии, что закрытые члены
реально существуют, назвать их типы и сигнатуры, и показать, что обычный код
к ним доступа не имеет.

---

## 2. Полный код программы

```csharp
using System;
using System.Reflection;

public class BlackBox
{
    private int _secret;               // приватное поле для чтения (цель)
    private readonly int _multiplier;  // приватное readonly-поле (константа)

    public BlackBox(int seed)
    {
        _multiplier = 3;
        _secret = Compute(seed);       // заполнение — «внутренняя» логика
    }

    // Единственный публичный метод чтения:
    public int ReadSecret()
    {
        return _secret;
    }

    // Приватный метод вычисления:
    private int Compute(int value)
    {
        return (value * _multiplier + 5) % 100;
    }
}

class Program
{
    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 4: «ЧЁРНЫЙ ЯЩИК» — ЗАКРЫТЫЕ ЧЛЕНЫ КЛАССА ===\n");

        var box = new BlackBox(42);
        Console.WriteLine($"Публичное чтение: ReadSecret() = {box.ReadSecret()}");
        Console.WriteLine();

        Console.WriteLine("Извне недоступны (прямое обращение не компилируется):");
        Console.WriteLine("  поле _secret   (private int)");
        Console.WriteLine("  поле _multiplier (private readonly int)");
        Console.WriteLine("  метод Compute  (private int)");

        Type type = typeof(BlackBox);
        Console.WriteLine("\nСуществование закрытых членов подтверждается рефлексией:");
        Console.WriteLine("  Приватные поля:");
        foreach (FieldInfo f in type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            string access = f.IsInitOnly ? "readonly " : "";
            Console.WriteLine($"    {access}{f.FieldType.Name} {f.Name}");
        }

        Console.WriteLine("  Приватные методы:");
        foreach (MethodInfo m in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var parameters = string.Join(", ", Array.ConvertAll(
                m.GetParameters(), p => $"{p.ParameterType.Name} {p.Name}"));
            Console.WriteLine($"    {m.ReturnType.Name} {m.Name}({parameters})");
        }

        Console.WriteLine("\nОткрытый интерфейс «чёрного ящика» доступен штатно:");
        Console.WriteLine($"  public int ReadSecret() -> {box.ReadSecret()}");
    }
}
```

---

## 3. Устройство «чёрного ящика»

```
                    ┌─────────────────────────────────────┐
   снаружи          │   BlackBox                           │
   (Program)        │                                     │
                    │  private int _secret;                │
   box.ReadSecret() │  private readonly int _multiplier;   │
   ───────────────► │                                     │
   публичный метод  │  public int ReadSecret() { _secret }│
                    │  private int Compute(int) { ... }    │
                    └─────────────────────────────────────┘
```

- `BlackBox(42)` вызывает конструктор: `_multiplier = 3`, затем
  `_secret = Compute(42)` → `(42 * 3 + 5) % 100 = 131 % 100 = 31`.
- Снаружи доступен только `ReadSecret()`. Попытки тронуть `box._secret`
  или `box.Compute(...)` **не скомпилируются** — компилятор сообщит о нарушении
  модификатора доступа `private`. Именно об этом говорят три строки
  «Извне недоступны (прямое обращение не компилируется)».

---

## 4. Что показывает рефлексия

### 4.1 Приватные поля

```csharp
type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
```

Без `NonPublic` вернулся бы пустой список (в классе публичных полей нет).
С флагом приходят оба поля:

```
Int32 _secret            <- private int
readonly Int32 _multiplier   <- private readonly int
```

Свойство `FieldInfo.IsInitOnly` равно `true` для `readonly`-полей — поэтому в
строку подставляется слово `readonly`. Это метаданные: CLR знает, что поле
защищено от изменений *компилятором*, но (как покажет Задание 5) —
не от рефлексии.

### 4.2 Приватные методы

```csharp
type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
```

Вывод: собственный метод `Compute(Int32 value)`; и два служебных члена, которые
CLR добавляет каждому типу —
`Finalize()` и `MemberwiseClone()` (наследуются от `object` и являются
`protected`, поэтому попадают в NonPublic-список).

### 4.3 Confirmation публичного интерфейса

В конце программа «честно» получает значение через легальный путь:
`box.ReadSecret()` → 31. Так демонстрируется сосуществование двух миров:
внешнего (public API) и «тёмного» (то, что видно в метаданных).

---

## 5. Полный вывод программы

```
=== ЗАДАНИЕ 4: «ЧЁРНЫЙ ЯЩИК» — ЗАКРЫТЫЕ ЧЛЕНЫ КЛАССА ===

Публичное чтение: ReadSecret() = 31

Извне недоступны (прямое обращение не компилируется):
  поле _secret   (private int)
  поле _multiplier (private readonly int)
  метод Compute  (private int)

Существование закрытых членов подтверждается рефлексией:
  Приватные поля:
    Int32 _secret
    readonly Int32 _multiplier
  Приватные методы:
    Int32 Compute(Int32 value)
    Object MemberwiseClone()
    Void Finalize()

Открытый интерфейс «чёрного ящика» доступен штатно:
  public int ReadSecret() -> 31
```

---

## 6. Наблюдения и выводы

- **Инкапсуляция — compile-time иллюзия для метаданных.** Модификатор `private`
  не удаляет член из сборки: CLR сохраняет его сигнатуру и тело в метаданных,
  поэтому рефлексия всё «видит».
- **`readonly`** ограничивает присваивания на уровне компилятора/конструктора.
  В рантайме, как показывает Задание 5, это тоже обходится.
- **Финализатор и MemberwiseClone** попадают в NonPublic-список любого класса —
  удобный «маячок» того, что мы смотрим на полный набор методов.
- Программа ничего не «ломает», но полностью подтверждает тезис Заданий 5–6:
  скрытые члены существуют и ждут, когда их вызовут через рефлексию.

---

## 7. Вопросы для защиты

1. **Почему `box._secret` не скомпилируется, а рефлексия его видит?**
   — Модификатор доступа проверяется компилятором C# при написании кода.
   Метаданные же содержат приватный член, и рефлексия читает их напрямую, не
   проходя через проверку доступа.

2. **Что такое `IsInitOnly`?**
   — Флаг `FieldInfo`, истинный для `readonly`-полей. Указывает, что инициализация
   разрешена только в конструкторе (на уровне CLR — до конца конструктора).

3. **Почему в списке методов оказались `Finalize` и `MemberwiseClone`?**
   — Это `protected`-члены класса `object`, поэтому они видны с флагом
   `NonPublic`; `Finalize` к тому же виртуальный (в C# — основа деструктора).

4. **Зачем нужен публичный `ReadSecret()`, если рефлексия и так всё достаёт?**
   — Это «легальный» канал: честный API для чтения. Задание 4 про то, чтобы
   показать его существование вместе со скрытой частью; Задание 5 — обход.