using Il2CppInterop.Runtime;
using UnityEngine;

namespace ChipGuide.Plugin.Runtime;

// 칩 선택 창·게임 로직은 핫업데이트 어셈블리에 있어 인터롭 바인딩이 없다.
// 그래서 IL2CPP 메타데이터를 이름으로 찾아 읽고 호출한다.
internal sealed unsafe class RuntimeObject
{
    // 래퍼가 살아 있는 동안 GC가 게임 객체를 회수하지 않도록 핸들을 잡아 둔다.
    private readonly Il2CppSystem.Object _handle;

    private RuntimeObject(IntPtr pointer) => _handle = new Il2CppSystem.Object(pointer);

    internal IntPtr Pointer => _handle.Pointer;
    private IntPtr Klass => IL2CPP.il2cpp_object_get_class(Pointer);
    internal IntPtr ClassPointer => Klass;
    internal string TypeName => Meta.ClassName(Klass);

    private static RuntimeObject? Of(IntPtr pointer) => pointer == IntPtr.Zero ? null : new RuntimeObject(pointer);

    internal static IntPtr FindClass(string ns, string name) => Meta.Class(ns, name);

    // 값 형식 필드(열거형 등)는 박싱된 객체로 돌려받는다.
    internal static RuntimeObject? StaticField(IntPtr klass, string name) =>
        Of(IL2CPP.il2cpp_field_get_value_object(Meta.Field(klass, name), IntPtr.Zero));

    internal static RuntimeObject? StaticCall(IntPtr klass, string method, params object?[] args) =>
        Of(Invoke(klass, IntPtr.Zero, method, args));

    internal static RuntimeObject New(IntPtr klass, params object?[] args)
    {
        Meta.Require(klass);
        var created = new RuntimeObject(IL2CPP.il2cpp_object_new(klass));
        created.Call(".ctor", args);
        return created;
    }

    internal RuntimeObject? Field(string name) =>
        Of(IL2CPP.il2cpp_field_get_value_object(Meta.Field(Klass, name), Pointer));

    internal T Field<T>(string name) where T : unmanaged
    {
        var field = Meta.Field(Klass, name);
        Meta.Verify<T>(IL2CPP.il2cpp_class_from_type(IL2CPP.il2cpp_field_get_type(field)));
        T value;
        IL2CPP.il2cpp_field_get_value(Pointer, field, &value);
        return value;
    }

    internal void SetField<T>(string name, T value) where T : unmanaged
    {
        var field = Meta.Field(Klass, name);
        Meta.Verify<T>(IL2CPP.il2cpp_class_from_type(IL2CPP.il2cpp_field_get_type(field)));
        IL2CPP.il2cpp_field_set_value(Pointer, field, &value);
    }

    internal void SetField(string name, string value)
    {
        var field = Meta.Field(Klass, name);
        var str = FromString(value);
        Meta.RequireAssignable(IL2CPP.il2cpp_field_get_type(field), str);
        // 참조 형식 필드는 값의 주소가 아니라 객체 포인터 자체를 넘긴다.
        IL2CPP.il2cpp_field_set_value(Pointer, field, (void*)str.Pointer);
        GC.KeepAlive(str);
    }

    internal RuntimeObject? Get(string property) => Call("get_" + property);

    internal T Get<T>(string property) where T : unmanaged
    {
        var boxed = Invoke(Klass, Self(), "get_" + property, Array.Empty<object?>());
        if (boxed == IntPtr.Zero) throw new InvalidOperationException($"게임 속성 값이 없습니다: {TypeName}.{property}");
        return Unbox<T>(boxed);
    }

    internal void Set(string property, object? value) => Call("set_" + property, value);

    internal RuntimeObject? Call(string method, params object?[] args)
    {
        var result = Invoke(Klass, Self(), method, args);
        GC.KeepAlive(this);
        return Of(result);
    }

    internal T Value<T>() where T : unmanaged => Unbox<T>(Pointer);

    internal string String()
    {
        if (TypeName != "String") throw new InvalidCastException($"문자열이 아닙니다: {TypeName}");
        return IL2CPP.Il2CppStringToManaged(Pointer) ?? "";
    }

    // 박싱된 구조체의 인스턴스 메서드는 this로 값의 주소를 받는다.
    private IntPtr Self() => IL2CPP.il2cpp_class_is_valuetype(Klass) ? IL2CPP.il2cpp_object_unbox(Pointer) : Pointer;

    private static T Unbox<T>(IntPtr boxed) where T : unmanaged
    {
        Meta.Verify<T>(IL2CPP.il2cpp_object_get_class(boxed));
        return *(T*)IL2CPP.il2cpp_object_unbox(boxed);
    }

    private static RuntimeObject FromString(string value) => new(IL2CPP.ManagedStringToIl2Cpp(value));

    private static IntPtr Invoke(IntPtr klass, IntPtr self, string name, object?[] args)
    {
        var method = Meta.Method(klass, name, args.Length);
        var count = Math.Max(args.Length, 1);
        // 슬롯 하나는 16바이트로, 넘기는 값 형식 중 가장 큰 Color가 들어간다.
        var slots = stackalloc ulong[count * 2];
        var argv = stackalloc void*[count];
        var strings = new List<RuntimeObject>();
        for (var i = 0; i < args.Length; i++)
            argv[i] = Marshal(IL2CPP.il2cpp_method_get_param(method, (uint)i), args[i], slots + i * 2, strings);

        var error = IntPtr.Zero;
        var result = IL2CPP.il2cpp_runtime_invoke(method, self, argv, ref error);
        GC.KeepAlive(strings);
        GC.KeepAlive(args);
        if (error != IntPtr.Zero) throw new InvalidOperationException($"게임 메서드 호출 실패: {Meta.ClassName(klass)}.{name}");
        return result;
    }

    private static void* Marshal(IntPtr type, object? arg, ulong* slot, List<RuntimeObject> strings)
    {
        if (type == IntPtr.Zero || IL2CPP.il2cpp_type_is_byref(type)) throw new InvalidCastException("ref/out 매개변수는 지원하지 않습니다");
        var klass = IL2CPP.il2cpp_class_from_type(type);
        switch (arg)
        {
            case int v: return Store(klass, slot, v);
            case float v: return Store(klass, slot, v);
            case bool v: return Store(klass, slot, v);
            case Color v: return Store(klass, slot, v);
            case Vector2 v: return Store(klass, slot, v);
            case string s:
                var str = FromString(s);
                strings.Add(str);
                Meta.RequireAssignable(type, str);
                return (void*)str.Pointer;
            case RuntimeObject obj:
                Meta.RequireAssignable(type, obj);
                return (void*)obj.Pointer;
            case null:
                Meta.RequireReference(type, "null");
                return null;
            default:
                throw new ArgumentException($"지원하지 않는 인자 형식입니다: {arg.GetType().Name}");
        }
    }

    private static void* Store<T>(IntPtr klass, ulong* slot, T value) where T : unmanaged
    {
        Meta.Verify<T>(klass);
        *(T*)slot = value;
        return slot;
    }

    // 메타데이터만 캐시한다. 게임 객체는 담지 않는다.
    private static class Meta
    {
        private static readonly Dictionary<(string, string), IntPtr> Classes = new();
        private static readonly Dictionary<IntPtr, string> Names = new();
        private static readonly Dictionary<(IntPtr, string), IntPtr> Fields = new();
        private static readonly Dictionary<(IntPtr, string, int), IntPtr> Methods = new();
        private static readonly HashSet<(IntPtr, Type)> Verified = new();

        internal static IntPtr Class(string ns, string name)
        {
            if (Classes.TryGetValue((ns, name), out var found)) return found;
            uint count = 0;
            var assemblies = IL2CPP.il2cpp_domain_get_assemblies(IL2CPP.il2cpp_domain_get(), ref count);
            for (var i = 0; i < count; i++)
            {
                found = IL2CPP.il2cpp_class_from_name(IL2CPP.il2cpp_assembly_get_image(assemblies[i]), ns, name);
                if (found != IntPtr.Zero) return Classes[(ns, name)] = found;
            }
            // 핫업데이트 어셈블리는 늦게 로드되므로 못 찾은 결과는 캐시하지 않는다.
            return IntPtr.Zero;
        }

        internal static string ClassName(IntPtr klass)
        {
            if (!Names.TryGetValue(klass, out var name)) Names[klass] = name = IL2CPP.il2cpp_class_get_name_(klass) ?? "";
            return name;
        }

        internal static void Require(IntPtr klass)
        {
            if (klass == IntPtr.Zero) throw new TypeLoadException("게임 클래스를 찾지 못했습니다");
        }

        internal static IntPtr Field(IntPtr klass, string name)
        {
            Require(klass);
            if (Fields.TryGetValue((klass, name), out var field)) return field;
            for (var c = klass; c != IntPtr.Zero && field == IntPtr.Zero; c = IL2CPP.il2cpp_class_get_parent(c))
                field = IL2CPP.il2cpp_class_get_field_from_name(c, name);
            if (field == IntPtr.Zero) throw new MissingFieldException(ClassName(klass), name);
            return Fields[(klass, name)] = field;
        }

        internal static IntPtr Method(IntPtr klass, string name, int argCount)
        {
            Require(klass);
            if (Methods.TryGetValue((klass, name, argCount), out var method)) return method;
            for (var c = klass; c != IntPtr.Zero && method == IntPtr.Zero; c = IL2CPP.il2cpp_class_get_parent(c))
                method = IL2CPP.il2cpp_class_get_method_from_name(c, name, argCount);
            if (method == IntPtr.Zero) throw new MissingMethodException(ClassName(klass), name);
            return Methods[(klass, name, argCount)] = method;
        }

        // 게임이 업데이트돼 형식이 바뀌면 메모리를 잘못 읽기 전에 멈춘다. 열거형은 바탕 정수 형식으로 비교한다.
        internal static void Verify<T>(IntPtr klass) where T : unmanaged
        {
            if (Verified.Contains((klass, typeof(T)))) return;
            if (klass == IntPtr.Zero || !IL2CPP.il2cpp_class_is_valuetype(klass))
                throw new InvalidCastException($"값 형식이 아닙니다: {typeof(T).Name}");
            var type = IL2CPP.il2cpp_class_is_enum(klass) ? IL2CPP.il2cpp_class_enum_basetype(klass) : IL2CPP.il2cpp_class_get_type(klass);
            var actual = IL2CPP.il2cpp_type_get_name_(type);
            uint align = 0;
            if (actual != typeof(T).FullName || IL2CPP.il2cpp_class_value_size(klass, ref align) != sizeof(T))
                throw new InvalidCastException($"형식이 다릅니다: {actual} / {typeof(T).FullName}");
            Verified.Add((klass, typeof(T)));
        }

        internal static void RequireReference(IntPtr type, string what)
        {
            if (IL2CPP.il2cpp_class_is_valuetype(IL2CPP.il2cpp_class_from_type(type)))
                throw new InvalidCastException($"참조 형식이 아닙니다: {what}");
        }

        internal static void RequireAssignable(IntPtr type, RuntimeObject value)
        {
            var target = IL2CPP.il2cpp_class_from_type(type);
            if (IL2CPP.il2cpp_class_is_valuetype(target) || !IL2CPP.il2cpp_class_is_assignable_from(target, value.Klass))
                throw new InvalidCastException($"{value.TypeName}을(를) {ClassName(target)}에 넘길 수 없습니다");
        }
    }
}
