using Godot;

namespace MintySpire2.MintySpire2Code.util;

/// <summary>
///     Small helper for tracking Godot nodes through weak references.
///     Handles dead-node cleanup and duplicate prevention.
/// </summary>
internal sealed class WeakNodeRegistry<T> where T : GodotObject
{
    private readonly List<WeakReference<T>> _refs = new();

    public void Register(T node)
    {
        if (!GodotObject.IsInstanceValid(node))
            return;

        for (var i = _refs.Count - 1; i >= 0; i--)
        {
            // Cleanup
            if (!TryGetLiveNode(i, out var existing))
            {
                _refs.RemoveAt(i);
                continue;
            }

            // Skip adding duplicate
            if (ReferenceEquals(existing, node))
                return;
        }

        _refs.Add(new WeakReference<T>(node));
    }

    public void Unregister(T node)
    {
        for (var i = _refs.Count - 1; i >= 0; i--)
        {
            if (!TryGetLiveNode(i, out var existing))
            {
                // Cleanup dead references
                _refs.RemoveAt(i);
                continue;
            }

            if (ReferenceEquals(existing, node))
            {
                _refs.RemoveAt(i);
                break;
            }
        }
    }

    public void ForEachLive(Action<T> action)
    {
        for (var i = _refs.Count - 1; i >= 0; i--)
        {
            if (!TryGetLiveNode(i, out var node))
            {
                _refs.RemoveAt(i);
                continue;
            }

            if (node != null) action(node);
        }
    }

    private bool TryGetLiveNode(int index, out T? node)
    {
        node = null;
        if (!_refs[index].TryGetTarget(out var target) || !GodotObject.IsInstanceValid(target))
            return false;

        node = target;
        return true;
    }
}
