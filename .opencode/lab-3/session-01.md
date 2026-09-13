# Сессия 01 — Выполнение лабораторной работы №3 «Рефлексия в C#»

## Дата
13 сентября 2026

## Среда
- .NET SDK: **10.0.112** (системный), целевой фреймворк — `net10.0`.
- Прогон команд: `dotnet build` / `dotnet run` в каждом проекте `Lab3/TaskN`.

## Поставленные задачи

1. Проанализировать файл условия `.opencode/lab-3/ЛАБ_рефлексияч1_19JUL.docx`.
   Тема — механизм рефлексии (`System.Type`) и позднее связывание.
2. Реализовать Задания 1–7 в отдельных проектах `Lab3/Task1..Task7`.
3. Реализовать бонусный раздел «ДОПОЛНИТЕЛЬНО (для успевающих)» — `Lab3/Task8`
   с полным кодом системы плагинов из условия.
4. Для каждого проекта выполнить `build` + `run` и зафиксировать вывод.
5. Создать отчётность: `explanation/task-1..8.md`, `session-01.md`,
   `testing-guide.md`, `answers.md`.

## Архитектура решений

- **Task1–Task6** — консольные приложения `net10.0` (один `Program.cs` + `.csproj`).
- **Task7** — хост + отдельная библиотека плагинов `Lab3/Task7.Plugins`
  (интерфейс `IPlugin`, атрибут `PluginAttribute`, плагины `CalculatorPlugin`,
  `LoggerPlugin`). DLL плагина копируется в выходную папку `Plugins/` хоста
  MSBuild-целью `CopyPluginsToOutput`. Загрузка — через `Assembly.LoadFrom` +
  конфигурационный файл `plugins.config`.
- **Task8** — `Lab3/Task8` + `Lab3/Task8.Plugins` (из условия: `IPlugin`,
  `IParameterizedPlugin`, `PluginManager`, `CalculatorPlugin`, `LoggerPlugin`).

## Разбор заданий и результаты

### Задание 1 — `Lab3/Task1` (класс с валидацией)

Класс `Product` с 4 приватными полями разных типов (`_id` int, `_name` string,
`_price` double, `_category` string) и public-свойствами с валидацией
(Id>0, Name/Категория не пустые, Price≥0). Невалидные значения бросают
`ArgumentException`. Вывод:

```
Исключение: Price не может быть отрицательным
Исключение: Name не может быть пустым
Исключение: Id должен быть больше 0
Приватные поля класса (перечислены через рефлексию):
  Int32 _id
  String _name
  Double _price
  String _category
```

### Задание 2 — `Lab3/Task2` (инспекция методов)

`GetMethods()` с разными наборами `BindingFlags`. Показана разница наборов:
13 публичных методов против 19 при `Public|NonPublic|Instance|Static`;
отсев синтетических аксессоров `get_/set_` (8 шт.) → 11 «настоящих» методов.
Для каждого метода печатается модификатор доступа, возвращаемый тип, имя,
список параметров и признаки `static`/`virtual`.

### Задание 3 — `Lab3/Task3` (Activator.CreateInstance, позднее связывание)

Класс `DynamicCalculator`. Создание экземпляра без `new` — `Activator.CreateInstance`,
в т.ч. через конструктор с параметром (`CalcWithParam`, `ConstructorInfo.Invoke`).
Установка/чтение свойств через рефлексию (`PropertyInfo.SetValue/GetValue`),
вызов перегруженных методов `Add(int,int)`/`Add(double,double)`, приватного
`LogOperation` и статического `GetVersion`.

### Задание 4 — `Lab3/Task4` («чёрный ящик»)

Класс `BlackBox`: приватное поле `_secret`, `readonly _multiplier`, приватный
метод вычислений `Compute`; публичный метод чтения `ReadSecret()`.
Продемонстрировано: извне закрытые члены недоступны на этапе компиляции,
но видны через рефлексию (`_secret`, `_multiplier`, `Compute`).

### Задание 5 — `Lab3/Task5` (доступ к приватному полю)

`FieldInfo.GetField("_secret", NonPublic|Instance)` + `SetValue`. Результат:
`_secret = 31 → 777`, значение подтверждено публичным методом `ReadSecret() = 777`.
Показана обработка ошибок: без `NonPublic` поле «не найдено»; несуществующее
поле → null. Также изменено `readonly`-поле `_multiplier` (SetValue → 9) —
демонстрация того, что рефлексия «ломает» инкапсуляцию.

### Задание 6 — `Lab3/Task6` (вызов приватных методов)

Три способа вызова приватных методов `HiddenService`:
1. `MethodInfo.Invoke` (2 параметра, 1 параметр, статический);
2. создание делегата `Delegate.CreateDelegate` → `Func<int,int,int>`,
   `Action<string>`, `Func<int,int>` (статический);
3. компоновка делегата через `Expression` (`Expression.Lambda(...).Compile()`).
Все способы успешно вызывают `private`-методы.

### Задание 7 — `Lab3/Task7` (динамическая загрузка плагинов)

Хост сканирует сборку `Task7.Plugins.dll` из каталога `Plugins/`
(`Assembly.LoadFrom` + `GetTypes` + `GetCustomAttribute<PluginAttribute>` +
проверка `IPlugin`), создаёт экземпляры через `Activator.CreateInstance` и
загружает плагины по именам из `plugins.config` (`Type.GetType` с резолвером).
Вывод: `Всего типов в сборке: 4`, оба плагина `Calculator` и `Logger` созданы и
выполнены; метод `Add(5.0, 3.0) = 8` вызван через рефлексию. Гибкость:
новый плагин = новая DLL в `Plugins/` + строка в конфиге, код хоста без правок.

### Задание 8 — `Lab3/Task8` (бонус, полный код из условия)

`PluginManager`: `LoadPlugins()` сканирует `Plugins/*.dll` и загружает типы,
реализующие `IPlugin` и не абстрактные; `GetPlugins()`, `GetPlugin(name)`,
`ExecutePluginMethod` (непрямой и обобщённый). Плагины `Calculator v1.0.0`,
`Logger v1.0.0`; `LoggerPlugin` реализует `IParameterizedPlugin` —
`ExecuteWithParams` write dictionary "message". Вызов `Add` через рефлексию
возвращает 10 и 30; несуществующий метод `Divide` обработан.

## Выполненные команды

```bash
# Для каждого проекта Lab3/Task1..Task8, Lab3/Task7.Plugins, Lab3/Task8.Plugins
#   cd Lab3/TaskN && dotnet build && dotnet run
# Общая проверка сборки:
for p in Task1 Task2 Task3 Task4 Task5 Task6 Task7 Task8 Task7.Plugins Task8.Plugins; do
  dotnet build "Lab3/$p" -nologo | grep -E "Warning\(s\)|Error\(s\)"
done
```

Итог по сборке (все проекты): **0 Warning(s), 0 Error(s)**.

Полные выводы программ (8 шт.) сохранены в разделе «Запуск проектов» —
см. тексты выполнения в `session-01.md` выше (выводы программ продублированы
в `testing-guide.md` и `explanation/task-*.md`).

## Примечания по API

- В условии код использует `Console.ReadKey()` в конце демонстраций — в наших
  программах он убран, чтобы `dotnet run` завершался без блокировки ввода.
- Для соблюдения требования «вывод на русском» строки плагинов из бонусного
  раздела условия (например, `"Calculator plugin loaded"`) переведены на русский
  (методы и логика сохранены).
- В `Task2` методы свойств отфильтрованы по префиксам `get_`/`set_`, унаследованные
  методы `object` помечены как есть (компилятор всегда добавляет их в `GetMethods()`).

## Выводы

- Реализованы все 7 заданий + бонусное задание 8, полный набор демонстраций
  рефлексии: получение `Type`, инспекция методов/полей/свойств, позднее
  связывание, работа с приватными членами, динамическая загрузка плагинов.
- Все проекты собираются без предупреждений и ошибок; программы выводят
  структурированный результат на русском языке.
- Демонстрация плагинов (Task7/Task8) выполнена на отдельных DLL: хост
  загружает их из каталога `Plugins/` в рантайме и не зависит от конкретных
  классов плагинов.
- Следующий шаг: файлы `answers.md` и `testing-guide.md` готовы для сдачи.