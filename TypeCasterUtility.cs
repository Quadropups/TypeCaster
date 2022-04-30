using System;
using System.Collections.Generic;
using System.Reflection;

public static class TypeCasterUtility {
    public static T Cast<T>(object data) => (T)Cast(data, typeof(T));

    public static object Cast(object data, Type type) {
        if (data == null) {
            return typeof(TypeCasterUtility).GetMethod(nameof(GetDefaultValue), BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(type).Invoke(null, null);
        }
        try {
            Type caster = typeof(TypeCaster<,>).MakeGenericType(new Type[] { data.GetType(), type });
            return caster.GetMethod("Cast", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { data });
        }
        catch {
            return null;
        }
    }
    private static T GetDefaultValue<T>() => default;


    /// <summary>Determines whether an instance of a specified type c can be casted to a variable of the current type using TypeCaster.</summary>
    public static bool IsCastableFrom(this Type t, Type c) {
        //Certain types can't be used as generic arguments of a generic type (pointers for example). Rather than overly complicating the code we just use try-catch
        Type caster;
        try {
            caster = typeof(TypeCaster<,>).MakeGenericType(new Type[] { c, t });
        }
        catch {
            return false;
        }
        try {
            return (bool)caster.GetProperty("IsValid", BindingFlags.Static | BindingFlags.Public).GetValue(null);
        }
        catch {
            return false;
        }
    }

    /// <summary>Determines whether an instance of a specified type c can be casted to a variable of the current type using TypeCaster.</summary>
    /// <param name="allowTypeTesting">If true, typetesting (casting from base to derived class or from/to an interface) is allowed</param>
    public static bool IsCastableFrom(this Type t, Type c, bool allowTypeTesting) {
        if (IsCastableFrom(t, c)) return true;

        if (allowTypeTesting) {
            if (c.IsAssignableFrom(t)) return true;
            if (t.IsInterface && !c.IsSealed) return true;
            if (!t.IsSealed && c.IsInterface) return true;
        }

        return false;
    }

    /// <summary>Get all custom casters and casting operator for specified type. This method only returns casters defined in this type and types it inherits from.</summary>
    public static IEnumerable<MethodInfo> GetCasters(Type type) {
        Type caster = typeof(TypeCaster<,>).MakeGenericType(type, typeof(object));
        foreach (MethodInfo method in (IEnumerable<MethodInfo>)caster.GetMethod("GetCasterMethods", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null)) {
            if (type.IsAssignableFrom(method.ReturnType)) continue;
            yield return method;
        }
        foreach (MethodInfo method in (IEnumerable<MethodInfo>)caster.GetMethod("GetOperatorCasters", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null)) {
            if (type.IsAssignableFrom(method.ReturnType)) continue;
            yield return method;
        }
    }

}
