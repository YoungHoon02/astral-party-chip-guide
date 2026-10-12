namespace ChipGuide.Plugin.Runtime;

internal static class GameUi
{
    private static IntPtr _rootClass;

    internal static IntPtr Require(string ns, string name)
    {
        var klass = RuntimeObject.FindClass(ns, name);
        return klass != IntPtr.Zero ? klass : throw new TypeLoadException($"게임 클래스가 없습니다: {ns}.{name}");
    }

    internal static RuntimeObject? Root()
    {
        if (_rootClass == IntPtr.Zero) _rootClass = RuntimeObject.FindClass("FairyGUI", "GRoot");
        return _rootClass == IntPtr.Zero ? null : RuntimeObject.StaticField(_rootClass, "_inst");
    }

    internal static bool Alive(RuntimeObject? obj) => obj != null && !obj.Get<bool>("isDisposed");

    internal static bool Visible(RuntimeObject? obj)
    {
        if (!Alive(obj) || !obj!.Get<bool>("onStage")) return false;
        for (var current = obj; current != null; current = current.Get("parent"))
            if (!current.Get<bool>("internalVisible") || !current.Get<bool>("internalVisible2")) return false;
        return true;
    }

    internal static RuntimeObject? FindWindow(RuntimeObject root, string typeName)
    {
        var count = root.Get<int>("numChildren");
        for (var i = 0; i < count; i++)
        {
            var child = root.Call("GetChildAt", i);
            if (child != null && child.TypeName == typeName && Visible(child)) return child;
        }
        return null;
    }

    internal static bool IsUnderPointer(RuntimeObject root, RuntimeObject target)
    {
        var hit = root.Get("touchTarget");
        for (var depth = 0; hit != null && depth < 32; depth++, hit = hit.Get("parent"))
            if (hit.Pointer == target.Pointer) return true;
        return false;
    }

    internal static void Dispose(RuntimeObject? obj)
    {
        if (Alive(obj)) obj!.Call("Dispose");
    }
}
