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
        _pluginsPath = pluginsPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
        Directory.CreateDirectory(_pluginsPath);
    }

    public string PluginsPath => _pluginsPath;

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
        return _plugins.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

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