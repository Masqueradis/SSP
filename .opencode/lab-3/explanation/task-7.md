# Задание 7 — Система динамической загрузки плагинов

**Файлы:**
- `Lab3/Task7/Program.cs` — хост (консольное приложение)
- `Lab3/Task7/Task7.csproj` — project-файл хоста
- `Lab3/Task7/plugins.config` — конфигурация подключаемых плагинов
- `Lab3/Task7.Plugins/Plugins.cs` — библиотека плагинов (отдельная DLL)

**Платформа:** .NET 10.0 · **Статус:** 0 Warning(s) / 0 Error(s)

---

## 1. Что изучаем

Полноценную модель **подключаемых модулей (plug-in)**. Основное приложение —
хост — **не знает заранее**, какие классы-плагины существуют. Оно получает их
только в рантайме: загружает сборку `Task7.Plugins.dll`, находит классы по
интерфейсу `IPlugin` и атрибуту `PluginAttribute`, создаёт экземпляры через
`Activator.CreateInstance` и вызывает их методы через рефлексию. Новый плагин
добавляется без перекомпиляции хоста.

---

## 2. Библиотека плагинов `Lab3/Task7.Plugins/Plugins.cs`

```csharp
using System;

namespace Task7.Plugins
{
    // Атрибут-«метка» для маркировки плагинов
    [AttributeUsage(AttributeTargets.Class)]
    public class PluginAttribute : Attribute
    {
        public string Name { get; }
        public PluginAttribute(string name) => Name = name;
    }

    // Контракт плагина
    public interface IPlugin
    {
        string Name { get; }
        void Execute();
    }

    [Plugin("Калькулятор")]
    public class CalculatorPlugin : IPlugin
    {
        public string Name => "Calculator";
        public void Execute()
        {
            Console.WriteLine("  Плагин Calculator выполнен");
        }

        public double Add(double a, double b) => a + b;       // вызывается рефлексией
        public double Subtract(double a, double b) => a - b;
    }

    [Plugin("Логгер")]
    public class LoggerPlugin : IPlugin
    {
        public string Name => "Logger";
        public void Execute()
        {
            Console.WriteLine("  Плагин Logger выполнен");
        }

        public void Log(string message)
        {
            Console.WriteLine($"  [LOG] {message}");
        }
    }
}
```

Ключевые решения:
- **`IPlugin`** — минимальный контракт (имя + метод `Execute`), по которому хост
  узнаёт плагин;
- **`PluginAttribute`** — человекочитаемое описание («Калькулятор», «Логгер»);
  считывается `GetCustomAttribute<PluginAttribute>()`;
- `Add`/`Subtract`/`Log` — «обычные» методы, которые будут вызваны через рефлексию
  в шаге 4.

---

## 3. Конфигурация `Lab3/Task7/plugins.config`

```
# Плагины, загружаемые хостом (имя типа, разделённое от сборки запятой)
Task7.Plugins.CalculatorPlugin, Task7.Plugins
Task7.Plugins.LoggerPlugin, Task7.Plugins
```

Формат строки — **полное имя типа**, за которым через запятую идёт **лёгкое имя
сборки**. Именно такой формат понимает `Type.GetType(string)`. Строки «#» хост
отфильтровывает как комментарии.

---

## 4. Project-файл хоста `Task7.csproj`

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
</PropertyGroup>

<ItemGroup>
  <ProjectReference Include="..\Task7.Plugins\Task7.Plugins.csproj" />
  <None Include="plugins.config" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>

<Target Name="CopyPluginsToOutput" AfterTargets="Build">
  <Copy SourceFiles="$(OutputPath)Task7.Plugins.dll"
        DestinationFolder="$(OutputPath)Plugins" />
</Target>
```

- `CopyToOutputDirectory="PreserveNewest"` → `plugins.config` попадает в вывод;
- цель `CopyPluginsToOutput` копирует `Task7.Plugins.dll` в подкаталог `Plugins/`
  рядом с exe. Так формируется «инсталляция плагина» — то, что хост загрузит
  в рантайме как настоящую внешнюю DLL.

> Хозяином это демонстрирует принцип: плагин лежит **отдельно** от хоста,
> хост ничего о нём не знает до запуска.

---

## 5. Хост — `Lab3/Task7/Program.cs`

### 5.1 Загрузка сборки и сканирование типов

```csharp
string pluginsDir = Path.Combine(AppContext.BaseDirectory, "Plugins");
string pluginDll = Path.Combine(pluginsDir, "Task7.Plugins.dll");

Assembly assembly = Assembly.LoadFrom(pluginDll);
Type[] types = assembly.GetTypes();
Type pluginInterface = typeof(IPlugin);

var candidates = types
    .Where(t => pluginInterface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
    .ToArray();
```

- `Assembly.LoadFrom(путь)` — создание объекта сборки из файла;
- `GetTypes()` — список типов;
- `typeof(IPlugin).IsAssignableFrom(t)` — t реализует `IPlugin` (или наследует);
- фильтр `!IsInterface && !IsAbstract` — фактическим плагином может быть только
  конкретный класс;
- цикл печатает каждый тип с атрибутом и пометкой «-> плагин» — визуальный итог
  сканирования.

### 5.2 Создание плагинов без `new`

```csharp
foreach (Type t in candidates)
{
    IPlugin plugin = (IPlugin)Activator.CreateInstance(t)!;
    plugin.Execute();
}
```

Те же приёмы, что в Задании 3, но уже для **неизвестной заранее** сборки:
тип из сканирования, фабрика `Activator.CreateInstance`, приведение к `IPlugin`.

### 5.3 Загрузка по имени из конфигурации

```csharp
string[] configLines = File.ReadAllLines(configPath)
    .Select(l => l.Trim())
    .Where(l => l.Length > 0 && !l.StartsWith('#'))
    .ToArray();

foreach (string line in configLines)
{
    Type? pluginType = Type.GetType(line, a => assembly, null);
    if (pluginType == null) { Console.WriteLine($"    {line} -> тип не найден"); continue; }
    if (!pluginInterface.IsAssignableFrom(pluginType) || pluginType.IsInterface || pluginType.IsAbstract)
    {
        Console.WriteLine($"    {line} -> не является плагином");
        continue;
    }
    IPlugin plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
    plugin.Execute();
}
```

Здесь используется **перегрузка `Type.GetType` с резолверами**:

```csharp
Type.GetType(
    fullName,          // "Task7.Plugins.CalculatorPlugin, Task7.Plugins"
    assemblyResolver,  // лямбда, возвращающая загруженную сборку
    typeResolver       // лямбда, возвращающая тип            (null — по умолчанию)
)
```

- `a => assembly` — резолвер сборки: по имени сборки вернём уже загруженную
  `assembly` (не перезагружая с диска);
- третий аргумент `null` — использовать стандартное поведение поиска типа.
- Некорректные строки конфига обрабатываются: «тип не найден» / «не является
  плагином».

### 5.4 Вызов метода плагина через рефлексию

```csharp
Type calculatorType = assembly.GetType("Task7.Plugins.CalculatorPlugin")!;
IPlugin calculator = (IPlugin)Activator.CreateInstance(calculatorType)!;
MethodInfo addMethod = calculator.GetType().GetMethod("Add")!;
object sum = addMethod.Invoke(calculator, new object[] { 5.0, 3.0 })!;
Console.WriteLine($"    Add(5.0, 3.0) = {sum}");
```

Достаём конкретный тип по полному имени, создаём экземпляр, ищем `Add`
(публичный метод, `GetMethod(name)` без флагов), вызываем с `object[] { 5.0, 3.0 }`.
Результат 8.0 подтверждает, что «чужой» метод реально выполнился.

### 5.5 Гибкость

В шаге 5 печатается рецепт расширения системы без изменения кода:
1. собрать новую DLL с классом, реализующим `IPlugin`;
2. скопировать её в каталог `Plugins/`;
3. добавить строку с именем типа в `plugins.config`.
4. перезапустить хост.

---

## 6. Полный вывод программы

```
=== ЗАДАНИЕ 7: СИСТЕМА ДИНАМИЧЕСКОЙ ЗАГРУЗКИ ПЛАГИНОВ ===

Каталог плагинов: /home/masqueradis/ssp/Lab3/Task7/bin/Debug/net10.0/Plugins
DLL плагинов:     Task7.Plugins.dll

1. Загрузка сборки и сканирование типов (Assembly.GetTypes + GetCustomAttribute):
  Всего типов в сборке: 4
    Task7.Plugins.PluginAttribute [атрибут: нет] 
    Task7.Plugins.IPlugin [атрибут: нет] 
    Task7.Plugins.CalculatorPlugin [атрибут: Калькулятор] -> плагин (реализует IPlugin)
    Task7.Plugins.LoggerPlugin [атрибут: Логгер] -> плагин (реализует IPlugin)

2. Создание экземпляров плагинов через Activator.CreateInstance:
    Создан плагин: Calculator
  Плагин Calculator выполнен
    Создан плагин: Logger
  Плагин Logger выполнен

3. Загрузка плагинов по имени из конфигурационного файла (plugins.config):
  Конфигурация: plugins.config
    Task7.Plugins.CalculatorPlugin, Task7.Plugins -> загружен: Calculator
  Плагин Calculator выполнен
    Task7.Plugins.LoggerPlugin, Task7.Plugins -> загружен: Logger
  Плагин Logger выполнен

4. Вызов метода плагина через рефлексию (DynamicMethod lookup):
    Add(5.0, 3.0) = 8

5. Гибкость системы:
  Добавление нового плагина НЕ требует изменения основного кода:
    1) собрать новую DLL с классом, реализующим IPlugin;
    2) скопировать её в каталог Plugins;
    3) добавить строку с именем типа в plugins.config.
  Программа пересобирается без правки Program.cs.
```

---

## 7. Вопросы для защиты

1. **Зачем `IsAssignableFrom` + `!IsInterface && !IsAbstract`?**
   — Это фильтр «конкретный класс, реализующий интерфейс». Без него в плагины
   попали бы сам интерфейс и абстрактные типы.

2. **Зачем в `Type.GetType` резолверы?**
   — Резолвер сборки заменяет «загрузку из глобального кэша сборок» на уже
   загруженную `assembly`; резолвер типа может задать нестандартную логику
   поиска (например, регистронезависимый поиск или поиск в нескольких сборках).

3. **Чем загрузка по `plugins.config` отличается от сканирования?**
   — Сканирование «знает» все типы в сборке и отбирает плагины по интерфейсу;
   конфиг задаёт список конкретных типов (управление составом плагинов без
   изменений кода). В реальных системах стыкуют оба подхода: сканирование +
   чёрный/белый список в конфиге.

4. **Что будет, если в конфиге указать несуществующий тип?**
   — `Type.GetType` вернёт `null`, ветка «тип не найден» печатает сообщение;
   программа продолжит работу — плагин просто не загрузится.

5. **Можно ли загрузить несколько разных DLL?**
   — Да: цикл по `Directory.GetFiles(pluginsDir, "*.dll")` — это сделано в
   Задании 8; здесь одна сборка для наглядности.