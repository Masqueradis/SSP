# Testing Guide — Лабораторная работа 3

**Тема:** Рефлексия в C# (механизм `System.Type`) и позднее связывание

Пошаговая инструкция по запуску, демонстрации и сдаче преподавателю.

Выполнено на **.NET 10.0** (SDK, дающий `dotnet --version` → 10.x). Все проекты —
в `Lab3/Task1` … `Lab3/Task8` + библиотеки плагинов `Lab3/Task7.Plugins`,
`Lab3/Task8.Plugins`.

> Примечание: если на машине преподавателя установлен только SDK 8.0, нужно либо
> поставить .NET 10 SDK, либо в каждом `.csproj` поменять `<TargetFramework>`
> на `net8.0` (код совместим).

---

## 0. Окружение

- Проверить версию: `dotnet --version` → должно быть 10.x.
- Собирать и запускать из каталога проекта: `dotnet build`, затем `dotnet run`.

---

## 1. Задание 1 — Класс с приватными полями и валидацией

**Команда:** `cd Lab3/Task1 && dotnet run`

**Что должно быть на экране (ключевые блоки):**
```
=== ЗАДАНИЕ 1: КЛАСС С ПРИВАТНЫМИ ПОЛЯМИ И ВАЛИДАЦИЕЙ ===
Product: Laptop, Price: 1000, Category: Electronics
  Цена со скидкой 15%: 850.00
Исключение: Price не может быть отрицательным
Исключение: Name не может быть пустым
Исключение: Id должен быть больше 0
Приватные поля класса (перечислены через рефлексию):
  Int32 _id
  String _name
  Double _price
  String _category
```

**Демонстрация:** класс содержит ≥3 приватных поля разных типов, каждое покрыто
публичным свойством с валидацией; невалидные значения бросают `ArgumentException`.

---

## 2. Задание 2 — Инспекция методов класса

**Команда:** `cd Lab3/Task2 && dotnet run`

**Ожидаемый результат (ключевое):**
```
2.1 Публичные методы (BindingFlags.Public | Instance): (13 методов)
2.2 Все методы (Public | NonPublic | Instance | Static): (19 методов)
   private Void ValidatePrice()
   protected Void UpdateCategory(String newCategory)
   public Int32 GetTotalCount() static
2.3 Методы без аксессоров свойств (фильтр get_/set_): (11 методов)
2.4 Синтетический аксессор свойства: get_Id ... set_Category (8 шт.)
2.5 BindingFlags.Public | Instance: 13 | Public|NonPublic|Instance|Static: 19
```

**Демонстрация:** выводится имя/возвращаемый тип/модификаторы/параметры для
каждого метода; видна разница наборов флагов и фильтрация `get_`/`set_`.

---

## 3. Задание 3 — Activator.CreateInstance (позднее связывание)

**Команда:** `cd Lab3/Task3 && dotnet run`

**Ожидаемый результат (ключевое):**
```
Тип найден: DynamicCalculator, FullName: DynamicCalculator
[Конструктор DynamicCalculator без параметров]
Экземпляр создан: DynamicCalculator
Label установлен через SetValue: CalcFromReflection
Результат Add(10, 20): 30
Результат Add(5.5, 3.2): 8.7
Result: 0;  Label:  CalcFromReflection   <- подтверждение чтения через рефлексию
Приватный метод LogOperation вызван успешно
Версия: DynamicCalculator v1.0
Result нового экземпляра: 42            <- конструктор с параметром
Через ConstructorInfo.Invoke: Result = 100
```

**Демонстрация:** объект создаётся без `new` (`Activator.CreateInstance`), значения
свойств устанавливаются и считываются через рефлексию (два способа создания:
конструктор без параметров и с параметрами).

---

## 4. Задание 4 — «Чёрный ящик»

**Команда:** `cd Lab3/Task4 && dotnet run`

**Ожидаемый результат (ключевое):**
```
Публичное чтение: ReadSecret() = 31
Извне недоступны (прямое обращение не компилируется):
  поле _secret   (private int)
  поле _multiplier (private readonly int)
  метод Compute  (private int)
Приватные поля: Int32 _secret; readonly Int32 _multiplier
Приватные методы: Int32 Compute(Int32 value)
```

**Демонстрация:** закрытые члены видны только через рефлексию, публичный интерфейс —
через `ReadSecret()`.

---

## 5. Задание 5 — Доступ к приватному полю

**Команда:** `cd Lab3/Task5 && dotnet run`

**Ожидаемый результат (ключевое):**
```
Начальное значение (публичное чтение): 31
GetField("_secret") без флагов -> null
Чтение через рефлексию: _secret = 31
После SetValue(777):      _secret = 777
Проверка публичным методом ReadSecret(): 777
Поле _multiplier с помощью SetValue теперь = 9
```

**Демонстрация:** `FieldInfo.GetField` + `SetValue` с `BindingFlags.NonPublic |
Instance` меняет приватное поле; проверка подтверждает нарушение инкапсуляции;
обработаны случаи «поле не найдено» и «без флагов».

---

## 6. Задание 6 — Вызов приватных методов

**Команда:** `cd Lab3/Task6 && dotnet run`

**Ожидаемый результат (ключевое):**
```
HiddenAdd(5, 7) = 12 (2 параметра)
LogIt("...") вызван (1 параметр)
Triple(10) = 30 (статический)
Делегат d1: d1(3, 4) = 7          <- CreateDelegate
Делегат d3 (статический): d3(5) = 15
Делегат d4 (Expression): d4(11, 22) = 33   <- Expression.Lambda
```

**Демонстрация:** приватные методы найдены через `BindingFlags.NonPublic` и вызваны
тремя способами: `MethodInfo.Invoke`, делегат (`Delegate.CreateDelegate`) и
`Expression`.

---

## 7. Задание 7 — Система динамической загрузки плагинов (отдельные DLL)

**Шаг 1. Собрать и запустить**
```bash
cd Lab3/Task7
dotnet build    # DLL плагина автоматически копируется в bin/.../Plugins/
dotnet run
```

**Ожидаемый результат (ключевое):**
```
Каталог плагинов: .../bin/Debug/net10.0/Plugins
Всего типов в сборке: 4
  Task7.Plugins.CalculatorPlugin [атрибут: Калькулятор] -> плагин (реализует IPlugin)
  Task7.Plugins.LoggerPlugin [атрибут: Логгер] -> плагин (реализует IPlugin)
Создан плагин: Calculator / Плагин Calculator выполнен
  ... Logger ...
Загрузка плагинов по имени из plugins.config:
  Task7.Plugins.CalculatorPlugin, Task7.Plugins -> загружен: Calculator
  Task7.Plugins.LoggerPlugin, Task7.Plugins -> загружен: Logger
Add(5.0, 3.0) = 8
```

**Демонстрация гибкости:** в `plugins.config` добавить/удалить строку с именем типа
плагина (или положить новую DLL в каталог `Plugins/`) — код `Program.cs` менять
не нужно.

---

## 8. Задание 8 (ДОПОЛНИТЕЛЬНО) — PluginManager

**Команда:**
```bash
cd Lab3/Task8
dotnet build
dotnet run
```

**Ожидаемый результат (ключевое):**
```
1. Загрузка всех плагинов из каталога Plugins:
  Загружен плагин: Calculator v1.0.0
  Загружен плагин: Logger v1.0.0
2. Calculator v1.0.0 / Logger v1.0.0
3. GetPlugin("calculator") -> Calculator v1.0.0
  [LOG] Привет из LoggerPlugin   <- IParameterizedPlugin.ExecuteWithParams
4. Метод Add выполнен. Результат: 10
  Generic ExecutePluginMethod<double> -> 30
  Метод Divide не найден в плагине Calculator
```

**Демонстрация:** `PluginManager` из условия собран полностью: сканирование
`Plugins/*.dll`, `Assembly.LoadFrom`, фильтр по `IPlugin`, поиск по имени,
вызов методов через рефлексию (обычный и обобщённый).

---

## 9. Чек-лист сдачи

- [ ] `dotnet --version` → 10.x.
- [ ] Все проекты `Lab3/Task1..8` (+ 2 библиотеки плагинов) собираются: `0 Warning(s), 0 Error(s)`.
- [ ] Задание 1 — класс с ≥3 приватными полями + валидация свойств.
- [ ] Задание 2 — список методов с модификаторами/параметрами, сравнение BindingFlags, фильтр `get_`/`set_`.
- [ ] Задание 3 — создание объекта через `Activator.CreateInstance` (2 способа), чтение/запись свойств через рефлексию.
- [ ] Задание 4 — «чёрный ящик»: приватные поле/метод, публичный метод чтения.
- [ ] Задание 5 — изменение приватного поля через `SetValue`, проверка публичным методом.
- [ ] Задание 6 — вызов приватных методов: `Invoke`, `CreateDelegate`, `Expression`.
- [ ] Задание 7 — загрузка плагинов из отдельной DLL + `plugins.config` по именам.
- [ ] Задание 8 — `PluginManager` (+`IParameterizedPlugin`) из бонусного раздела.
- [ ] Ответы на контрольные вопросы (файл `.opencode/lab-3/answers.md`).
- [ ] Объяснения по каждому заданию (файлы `.opencode/lab-3/explanation/task-N.md`).