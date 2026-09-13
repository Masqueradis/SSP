# Задание 8 (ДОПОЛНИТЕЛЬНО) — Система плагинов PluginManager

**Файлы:**
- `Lab3/Task8/Program.cs` — демонстрационное приложение
- `Lab3/Task8/Task8.csproj` — project-файл хоста
- `Lab3/Task8.Plugins/Plugins.cs` — интерфейсы и плагины
- `Lab3/Task8.Plugins/PluginManager.cs` — менеджер плагинов

**Платформа:** .NET 10.0 · **Статус:** 0 Warning(s) / 0 Error(s)

---

## 1. Что изучаем

Бонусный раздел условия «ДОПОЛНИТЕЛЬНО (для успевающих)»: **полный код**
демонстрационной системы плагинов — `IPlugin`, `IParameterizedPlugin`,
`PluginManager`, `CalculatorPlugin`, `LoggerPlugin`. В отличие от Задания 7:

- хост загружает **все DLL** из каталога `Plugins/`;
- поиск идёт **только по интерфейсу** `IPlugin` (**без атрибута**);
- есть **`IParameterizedPlugin`** — плагин с параметризованным вызовом;
- весь управляющий код вынесен в класс **`PluginManager`**.

---

## 2. Полный код интерфейсов и плагинов — `Plugins.cs`

```csharp
using System;
using System.Collections.Generic;

public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    void Execute();
}

public interface IParameterizedPlugin : IPlugin
{
    void ExecuteWithParams(Dictionary<string, object> parameters);
}

public class CalculatorPlugin : IPlugin
{
    public string Name => "Calculator";
    public string Version => "1.0.0";

    public void Execute()
    {
        Console.WriteLine("  Плагин Calculator выполнен");
    }

    public double Add(double a, double b) => a + b;
    public double Subtract(double a, double b) => a - b;
}

public class LoggerPlugin : IParameterizedPlugin
{
    public string Name => "Logger";
    public string Version => "1.0.0";

    public void Execute()
    {
        Console.WriteLine("  Плагин Logger выполнен");
    }

    public void ExecuteWithParams(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("message", out object? msg))
        {
            Console.WriteLine($"  [LOG] {msg}");
        }
    }
}
```

Важные детали:
- `CalculatorPlugin` реализует только `IPlugin` — «простой» плагин;
- `LoggerPlugin` дополнительно реализует `IParameterizedPlugin` — умеет принимать
  параметры (`Dictionary<string, object>`), что показывает расширенный контракт;
- типы объявлены в **глобальном пространстве имён** — как в исходном коде
  условия.

---

## 3. Полный код `PluginManager` — `PluginManager.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

public class PluginManager
{
    private readonly List<IPlugin> _plugins = new List<IPlugin>();
    private readonly string _pluginsPath;

    public PluginManager(string? pluginsPath = null)
    {
        _pluginsPath = pluginsPath ?? Path.Combine(AppContext.BaseDirectory, "Plugins");
        Directory.CreateDirectory(_pluginsPath);
    }

    public string PluginsPath => _pluginsPath;

    // Загрузка ВСЕХ *.dll из каталога плагинов
    public void LoadPlugins()
    {
        var dllFiles = Directory.GetFiles(_pluginsPath, "*.dll");

        foreach (var dllPath in dllFiles)
        {
            try
            {
                LoadPluginFromAssembly(dllPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Ошибка загрузки плагина {dllPath}: {ex.Message}");
            }
        }
    }

    private void LoadPluginFromAssembly(string assemblyPath)
    {
        Assembly assembly = Assembly.LoadFrom(assemblyPath);
        Type[] types = assembly.GetTypes();

        foreach (var type in types)
        {
            if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
            {
                IPlugin plugin = (IPlugin)Activator.CreateInstance(type)!;
                _plugins.Add(plugin);
                Console.WriteLine($"  Загружен плагин: {plugin.Name} v{plugin.Version}");
            }
        }
    }

    public IEnumerable<IPlugin> GetPlugins() => _plugins;

    public IPlugin? GetPlugin(string name)
    {
        return _plugins.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    // Обычный вызов метода через рефлексию
    public void ExecutePluginMethod(IPlugin plugin, string methodName, object[]? parameters)
    {
        Type pluginType = plugin.GetType();
        MethodInfo? method = pluginType.GetMethod(methodName);

        if (method != null)
        {
            object? result = method.Invoke(plugin, parameters);
            Console.WriteLine($"  Метод {methodName} выполнен. Результат: {result ?? "null"}");
        }
        else
        {
            Console.WriteLine($"  Метод {methodName} не найден в плагине {plugin.Name}");
        }
    }

    // Обобщённый вызов с типизированным результатом
    public T? ExecutePluginMethod<T>(IPlugin plugin, string methodName, object[]? parameters)
    {
        Type pluginType = plugin.GetType();
        MethodInfo? method = pluginType.GetMethod(methodName);

        if (method != null && method.ReturnType == typeof(T))
        {
            return (T?)method.Invoke(plugin, parameters);
        }

        return default;
    }
}
```

Разбор ключевых методов:

### `LoadPlugins()`
- `Directory.GetFiles(..., "*.dll")` — все DLL в каталоге (Task7 подключал одну);
- `Assembly.LoadFrom` + `GetTypes()` — перебор типов;
- фильтр `IsAssignableFrom(IPlugin) && !IsInterface && !IsAbstract`;
- `Activator.CreateInstance(type)` → экземпляр плагина;
- каждый экземпляр добавляется в `_plugins` — список записей.

### `GetPlugin(name)`
- линейный поиск `FirstOrDefault` с **регистронезависимым** сравнением
  (`OrdinalIgnoreCase`) → поэтому `GetPlugin("calculator")` находит `Calculator`.

### `ExecutePluginMethod` (обычный)
- `GetMethod(methodName)` — поиск публичного метода по имени;
- `method.Invoke(plugin, parameters)` — вызов с параметрами;
- отсутствие метода → «не найден», ошибка не бросается.

### `ExecutePluginMethod<T>` (обобщённый)
- дополнительная проверка `method.ReturnType == typeof(T)` — только в этом
  случае выполняется типизированный каст `(T?)method.Invoke(...)`.
- несовпадение → `default`.

---

## 4. Демонстрация — `Lab3/Task8/Program.cs`

```csharp
using System;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 8 (ДОПОЛНИТЕЛЬНО): СИСТЕМА ПЛАГИНОВ PluginManager ===\n");

        var manager = new PluginManager();
        Console.WriteLine($"Каталог плагинов: {manager.PluginsPath}");

        Console.WriteLine("\n1. Загрузка всех плагинов из каталога Plugins:");
        manager.LoadPlugins();

        Console.WriteLine("\n2. Список загруженных плагинов:");
        foreach (var plugin in manager.GetPlugins())
        {
            Console.WriteLine($"  {plugin.Name} v{plugin.Version}");
        }

        Console.WriteLine("\n3. Получение плагина по имени (GetPlugin):");
        IPlugin? calc = manager.GetPlugin("calculator");          // регистронезависимый поиск
        if (calc != null)
        {
            Console.WriteLine($"  Найден: {calc.Name} v{calc.Version}");
            calc.Execute();
        }

        IPlugin? logger = manager.GetPlugin("Logger");
        if (logger != null)
        {
            Console.WriteLine($"  Найден: {logger.Name} v{logger.Version}");
            logger.Execute();
            if (logger is IParameterizedPlugin parameterized)
            {
                parameterized.ExecuteWithParams(new Dictionary<string, object> { ["message"] = "Привет из LoggerPlugin" });
            }
        }

        Console.WriteLine("\n4. Вызов метода плагина через рефлексию (ExecutePluginMethod):");
        if (calc != null)
        {
            manager.ExecutePluginMethod(calc, "Add", new object[] { 5.5, 4.5 });
            double sum = manager.ExecutePluginMethod<double>(calc, "Add", new object[] { 10.0, 20.0 });
            Console.WriteLine($"  Generic ExecutePluginMethod<double> -> {sum}");

            manager.ExecutePluginMethod(calc, "Divide", new object[] { 10.0, 2.0 });   // метод не существует
        }

        Console.WriteLine("\n5. Гибкость: новый плагин добавляется отдельной DLL в каталог Plugins,");
        Console.WriteLine("   основной код (PluginManager и Program) не изменяется.");
    }
}
```

Порядок демонстрации повторяет структуру условия:
1. **Загрузка** — `LoadPlugins()` подхватывает `Calculator` и `Logger` из DLL;
2. **список** — `GetPlugins()`;
3. **поиск** — `GetPlugin("calculator")` (регистр не важен) и `GetPlugin("Logger")`,
   вызов `Execute()`, а для логгера — `IParameterizedPlugin.ExecuteWithParams`;
4. **рефлексивный вызов** — обычный и обобщённый `ExecutePluginMethod`, плюс
   попытка вызвать **несуществующий** метод `Divide` (мягкая обработка).

---

## 5. Полный вывод программы

```
=== ЗАДАНИЕ 8 (ДОПОЛНИТЕЛЬНО): СИСТЕМА ПЛАГИНОВ PluginManager ===

Каталог плагинов: /home/masqueradis/ssp/Lab3/Task8/bin/Debug/net10.0/Plugins

1. Загрузка всех плагинов из каталога Plugins:
  Загружен плагин: Calculator v1.0.0
  Загружен плагин: Logger v1.0.0

2. Список загруженных плагинов:
  Calculator v1.0.0
  Logger v1.0.0

3. Получение плагина по имени (GetPlugin):
  Найден: Calculator v1.0.0
  Плагин Calculator выполнен
  Найден: Logger v1.0.0
  Плагин Logger выполнен
  [LOG] Привет из LoggerPlugin

4. Вызов метода плагина через рефлексию (ExecutePluginMethod):
  Метод Add выполнен. Результат: 10
  Generic ExecutePluginMethod<double> -> 30
  Метод Divide не найден в плагине Calculator

5. Гибкость: новый плагин добавляется отдельной DLL в каталог Plugins,
   основной код (PluginManager и Program) не изменяется.
```

---

## 6. Сравнение Task8 и Task7

| Аспект | Task7 | Task8 |
|--------|-------|-------|
| Каталог DLL | одна сборка `Plugins/Task7.Plugins.dll` | **все** `*.dll` в `Plugins/` |
| Поиск плагина | атрибут `[Plugin]` + `IPlugin` | **только** интерфейс `IPlugin` |
| Загрузка по имени | `plugins.config` + `Type.GetType(name)` | поиск в `_plugins` по `Name` |
| Управляющий класс | логика в `Program.Main` | вынесен `PluginManager` |
| Доп. контракт | нет | `IParameterizedPlugin` |
| Вызов метода | `MethodInfo.Invoke` «напрямую» | `ExecutePluginMethod` (+ generic) |
| Версия плагина | нет в интерфейсе | `Version` |

---

## 7. Вопросы для защиты

1. **Почему `LoadPlugins` обёрнут в `try/catch`?**
   — `LoadFrom(путь)` и `GetTypes` могут упасть на «битой» DLL; гибкая система
   пропускает проблемыый плагин и продолжает грузить остальные.

2. **Зачем проверка `!type.IsInterface && !type.IsAbstract`?**
   — Интерфейсный или абстрактный тип нельзя создать `Activator.CreateInstance` —
   это упало бы с `MemberAccessException`.

3. **Что вернёт `ExecutePluginMethod<T>` при несовпадении типов?**
   — `default(T)`. В примере generic-версия дополнительно проверяет
   `method.ReturnType == typeof(T)`.

4. **Почему `GetPlugin` регистронезависимый?**
   — `StringComparison.OrdinalIgnoreCase` в `Equals` — плагин можно запросить
   как `"calculator"`, `"Calculator"` и т.д.

5. **Как добавить третий плагин без изменения кода?**
   — Собрать DLL с классом `IPlugin`, положить её в `Plugins/`, перезапустить —
   `LoadPlugins` подхватит её автоматически.