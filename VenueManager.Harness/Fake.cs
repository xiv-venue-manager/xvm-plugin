using System.Collections.Concurrent;
using System.Reflection;

namespace VenueManager.Harness;

public class Fake : DispatchProxy
{
    public static readonly ConcurrentDictionary<string, int> Calls = new();
    public static readonly Dictionary<string, Func<object?[], object?>> Overrides = new();

    private readonly Dictionary<string, Delegate?> events = new();
    private readonly Dictionary<string, object?> props = new();
    private Type iface = null!;

    public static void Reset()
    {
        Calls.Clear();
        Overrides.Clear();
    }

    public static T Make<T>() where T : class => (T)Make(typeof(T));

    public static object Make(Type type)
    {
        var proxy = Create(type, typeof(Fake));
        ((Fake)proxy).iface = type;
        return proxy;
    }

    public void Raise(string name, params object?[] args) => events.GetValueOrDefault(name)?.DynamicInvoke(args);

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var name = method!.Name;
        var key = iface.Name + "." + name;
        Calls.AddOrUpdate(key, 1, (_, count) => count + 1);
        args ??= Array.Empty<object?>();

        if (Overrides.TryGetValue(key, out var answer)) return answer(args);

        if (name.StartsWith("add_"))
        {
            var eventName = name[4..];
            events[eventName] = Delegate.Combine(events.GetValueOrDefault(eventName), (Delegate?)args[0]);
            return null;
        }

        if (name.StartsWith("remove_"))
        {
            var eventName = name[7..];
            events[eventName] = Delegate.Remove(events.GetValueOrDefault(eventName), (Delegate?)args[0]);
            return null;
        }

        if (name.StartsWith("set_"))
        {
            props[name[4..]] = args[0];
            return null;
        }

        if (name.StartsWith("get_") && props.TryGetValue(name[4..], out var stored)) return stored;

        return DefaultFor(method.ReturnType);
    }

    private static object? DefaultFor(Type type)
    {
        if (type == typeof(void)) return null;
        if (type == typeof(string)) return "";
        if (type == typeof(Task)) return Task.CompletedTask;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var inner = type.GetGenericArguments()[0];
            var value = inner.IsValueType ? Activator.CreateInstance(inner) : null;
            return typeof(Task).GetMethod("FromResult")!.MakeGenericMethod(inner).Invoke(null, new[] { value });
        }

        if (type.IsInterface) return Make(type);
        if (type.IsValueType) return Activator.CreateInstance(type);
        return null;
    }
}
