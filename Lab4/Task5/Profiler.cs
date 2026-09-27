using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Lab4Task5;

sealed class ProfileNode
{
    public ProfileNode(string name, ProfileNode? parent)
    {
        Name = name;
        Parent = parent;
        Children = new Dictionary<string, ProfileNode>(StringComparer.Ordinal);
    }

    public string Name { get; }
    public ProfileNode? Parent { get; }
    public Dictionary<string, ProfileNode> Children { get; }

    public long TotalTicks { get; set; }
    public long SelfTicks { get; set; }
    public int Calls { get; set; }
    public long AllocatedBytes { get; set; }

    public double TotalMilliseconds => TotalTicks * 1000.0 / Stopwatch.Frequency;
    public double SelfMilliseconds => SelfTicks * 1000.0 / Stopwatch.Frequency;
    public double ShareOfParent => Parent is null || Parent.TotalTicks == 0
        ? 0
        : 100.0 * TotalTicks / Parent.TotalTicks;
}

sealed class Profiler : IDisposable
{
    private sealed class Frame
    {
        public required ProfileNode Node { get; init; }
        public required long StartTicks { get; init; }
        public long ChildTicks { get; set; }
        public long AllocatedAtEntry { get; init; }
    }

    private readonly ProfileNode _root = new("Main", null);
    private readonly Stack<Frame> _stack = new();
    private bool _allocationTrackingEnabled;

    public Profiler()
    {
        _stack.Push(new Frame
        {
            Node = _root,
            StartTicks = Stopwatch.GetTimestamp(),
            AllocatedAtEntry = 0
        });
    }

    public ProfileNode Root => _root;

    public void EnableAllocationTracking() => _allocationTrackingEnabled = true;

    public void Complete()
    {
        while (_stack.Count > 1)
        {
            Exit();
        }

        if (_stack.Count == 1)
        {
            Exit();
        }
    }

    public void Dispose() => Complete();

    public Scope Enter(string name)
    {
        ProfileNode parent = _stack.Peek().Node;
        if (!parent.Children.TryGetValue(name, out ProfileNode? child))
        {
            child = new ProfileNode(name, parent);
            parent.Children[name] = child;
        }

        _stack.Push(new Frame
        {
            Node = child,
            StartTicks = Stopwatch.GetTimestamp(),
            AllocatedAtEntry = _allocationTrackingEnabled ? GC.GetTotalAllocatedBytes(precise: false) : 0
        });

        return new Scope(this);
    }

    private void Exit()
    {
        Frame frame = _stack.Pop();
        long elapsed = Stopwatch.GetTimestamp() - frame.StartTicks;

        frame.Node.TotalTicks += elapsed;
        frame.Node.Calls++;

        long ownTicks = elapsed - frame.ChildTicks;
        if (ownTicks < 0)
        {
            ownTicks = 0;
        }
        frame.Node.SelfTicks += ownTicks;

        if (_allocationTrackingEnabled)
        {
            frame.Node.AllocatedBytes += GC.GetTotalAllocatedBytes(precise: false) - frame.AllocatedAtEntry;
        }

        if (_stack.Count > 0)
        {
            _stack.Peek().ChildTicks += elapsed;
        }
    }

    public readonly struct Scope : IDisposable
    {
        private readonly Profiler? _profiler;

        internal Scope(Profiler profiler)
        {
            _profiler = profiler;
        }

        public void Dispose()
        {
            _profiler?.Exit();
        }
    }

    public IEnumerable<ProfileNode> AllNodes()
    {
        List<ProfileNode> result = new();
        Collect(_root, result);
        return result;
    }

    private static void Collect(ProfileNode node, List<ProfileNode> result)
    {
        result.Add(node);
        foreach (ProfileNode child in node.Children.Values)
        {
            Collect(child, result);
        }
    }

    public IEnumerable<ProfileNode> TopBySelfTime(int count)
    {
        return AllNodes()
            .Where(n => n.Parent is not null && n.SelfTicks > 0)
            .OrderByDescending(n => n.SelfTicks)
            .Take(count);
    }
}

static class TreePrinter
{
    public static void Print(Profiler profiler, string title)
    {
        ProfileNode root = profiler.Root;
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(title);
        Console.WriteLine($"Общее время: {root.TotalMilliseconds:F2} мс   Вызовов: {root.Calls}");
        Console.WriteLine(new string('=', 78));
        Console.WriteLine();
        Console.WriteLine("Дерево вызовов (в скобках — Own Time, т.е. время САМОГО метода):");
        Console.WriteLine();

        PrintNode(root, 0, root.TotalTicks);

        Console.WriteLine();
    }

    private static void PrintNode(ProfileNode node, int depth, long parentTicks)
    {
        string indent = new(' ', depth * 4);
        string connector = depth == 0 ? string.Empty : depth == 1 ? "├── " : "│   ├── ";
        double share = parentTicks == 0 ? 0 : 100.0 * node.TotalTicks / parentTicks;
        double selfShare = node.TotalTicks == 0 ? 0 : 100.0 * node.SelfTicks / node.TotalTicks;

        string bar = BuildBar(selfShare);
        string hot = node.SelfTicks > 0 && selfShare >= 25 ? "  <-- горячий" : string.Empty;

        if (depth == 0)
        {
            Console.WriteLine($"{node.Name}  всего {node.TotalMilliseconds:F2} мс");
        }
        else
        {
            Console.WriteLine($"{indent}{connector}{node.Name}");
            Console.WriteLine($"{indent}│     всего: {node.TotalMilliseconds,8:F2} мс ({share,5:F1} % родителя)" +
                              $"  вызовов: {node.Calls}");
            Console.WriteLine($"{indent}│     OWN TIME: {node.SelfMilliseconds,8:F2} мс ({selfShare,5:F1} % от метода)" +
                              $"  {bar}{hot}");
        }

        List<ProfileNode> children = node.Children.Values
            .OrderByDescending(c => c.TotalTicks)
            .ToList();

        foreach (ProfileNode child in children)
        {
            PrintNode(child, depth + 1, node.TotalTicks);
        }
    }

    private static string BuildBar(double percent)
    {
        int length = (int)(percent / 5);
        if (length <= 0)
        {
            return string.Empty;
        }

        return new string('#', Math.Min(length, 20));
    }
}
