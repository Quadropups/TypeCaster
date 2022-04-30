using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Type caster that allows fast runtime casting any type to another type including casting enums to enums, to/from interfaces and generic types
/// </summary>
public static class TypeCaster<T, TResult> {
    #region Fields

    private static Func<T, TResult> CastFunc;

    #endregion

    static TypeCaster() {

        //If target type is object we simply return value as System.Object
        if (TrySetObjectCast()) {
            return;
        }

        //Then we try to return T as TResult (if T inherits from TResult)
        if (TrySetInheritCast()) {
            return;
        }

        //Then we try to use one of the custom casters
        if (TrySetCustomCaster()) {
            return;
        }

        //Then we try to find a casting operator on T and TResult
        if (TrySetOperatorMethod()) {
            return;
        }

        //Then we try to use one of the static custom casters
        if (TrySetStaticCaster()) {
            return;
        }

        //Then we try to simply return casted type as desired type. This will work with exact matches, derived types and if we cast enum to/from it's underlying type (int, long, etc)
        //This method won't work if desired type is System.Object and casted type is a ValueType because this method will fail to box casted data
        if (TrySetDefaultConverter()) {
            return;
        }

        //If we want to cast enum to another enum then we must first cast to it's underlying type (integer in most cases) and only then we can cast it to desired enum type
        if (TrySetEnumConversionMethod()) {
            return;
        }

        //If this type has a default caster we try to see if it's viable as intermediate cast
        if (TryDefaultTypeCast()) {
            return;
        }

        //Finally if we failed to find acceptible cast method then type "T" is not castable to type "TResult" so we do safe cast (returns default value if cast fails)
        CastFunc = SafeCast;
    }

    public static MethodInfo CasterMethod => CastFunc?.Method;

    public static bool IsValid => CastFunc.Method != ((Func<T, TResult>)SafeCast).Method;

    public static TResult Cast(T value) => CastFunc(value);

    private static IEnumerable<MethodInfo> GetOperatorCasters() {
        foreach (Type type in GetTypes(false)) {
            foreach (MethodInfo m in type.GetMethods(BindingFlags.Static | BindingFlags.Public)) {
                if (!m.IsSpecialName) continue;
                if (m.Name != "op_Implicit" && m.Name != "op_Explicit") continue;
                yield return m;
            }
        }
    }

   

    private static TResult CastEnumToEnumByte(byte value) => TypeCaster<byte, TResult>.CastFunc(value);

    private static TResult CastEnumToEnumInt(int value) => TypeCaster<int, TResult>.CastFunc(value);

    private static TResult CastEnumToEnumLong(long value) => TypeCaster<long, TResult>.CastFunc(value);

    private static TResult CastEnumToEnumSByte(sbyte value) => TypeCaster<sbyte, TResult>.CastFunc(value);

    private static TResult CastEnumToEnumShort(short value) => TypeCaster<short, TResult>.CastFunc(value);

    private static TResult CastEnumToEnumUInt(uint value) => TypeCaster<uint, TResult>.CastFunc(value);

    private static TResult CastEnumToEnumULong(ulong value) => TypeCaster<ulong, TResult>.CastFunc(value);

    private static TResult CastEnumToEnumUShort(ushort value) => TypeCaster<ushort, TResult>.CastFunc(value);

    private static object CastToSystemObject(T value) => value;

    private static Func<T, TResult> CreateDelegate(MethodInfo method) => (Func<T, TResult>)Delegate.CreateDelegate(typeof(Func<T, TResult>), method);

    private static TResult DefaultCast(TResult value) => value;

    private static TResult DefaultTypeCast<TDefault>(T value) {
        TDefault temp = TypeCaster<T, TDefault>.Cast(value);
        return TypeCaster<TDefault, TResult>.Cast(temp);
    }

    private static IEnumerable<DefaultTypeCasterAttribute> GetDefaultCasterAttributes() {
        foreach (Type type in GetTypes(typeof(T), true)) {
            IEnumerable<DefaultTypeCasterAttribute> attributes = type.GetCustomAttributes<DefaultTypeCasterAttribute>();
            foreach (DefaultTypeCasterAttribute attribute in attributes) yield return attribute;
        }
    }

    private static MethodInfo GetTestedMethod(MethodInfo method) {
        if (!method.IsGenericMethod) {
            if (InstanceMethodIsCompatible(method)) {
                return method;
            }
        }
        else {
            bool returnTypeContainsGenericParameters = method.ReturnType.ContainsGenericParameters;

            foreach (var t in GetTypes(typeof(TResult), true)) {
                MethodInfo specific = null;
                Type[] typeArguments;

                if (returnTypeContainsGenericParameters) {
                    typeArguments = t.GetGenericArguments();
                }
                else {
                    typeArguments = new Type[] { t };

                }

                try {
                    specific = method.MakeGenericMethod(typeArguments);
                }
                catch {
                    continue;
                }

                if (InstanceMethodIsCompatible(specific)) return specific;
            }
        }

        return null;
    }

    private static bool InstanceMethodIsCompatible(MethodInfo method) {
        if (!typeof(TResult).IsAssignableFrom(method.ReturnType)) return false;
        return true;
    }

    private static IEnumerable<Type> GetTypes(bool getInterfaces) {
        HashSet<Type> types = new HashSet<Type>();
        foreach (Type t in GetTypes(typeof(T), getInterfaces)) {
            if (types.Contains(t)) continue;
            types.Add(t);
            yield return t;
        }

        foreach (Type t in GetTypes(typeof(TResult), getInterfaces)) {
            if (types.Contains(t)) continue;
            types.Add(t);
            yield return t;
        }
    }

    private static IEnumerable<Type> GetTypes(Type type, bool getInterfaces) {
        Type current = type;
        do {
            yield return current;
            current = current.BaseType;
        }
        while (current != null);

        if (getInterfaces) {
            foreach (var t in type.GetInterfaces()) yield return t;
        }
    }


    private static TResult SafeCast(T value) {
        if (value is TResult cast) return cast;
        return default;
    }

    private static MethodInfo SelectMethod(MethodInfo current, MethodInfo other) {
        if (other == null) return current;
        if (current == null) return other;
        if (current.ReflectedType.IsAssignableFrom(other.ReflectedType)) return other;
        //not sure if needed
        if (other.ReturnType.IsAssignableFrom(current.ReturnType)) return other;
        return current;
    }

    private static bool TryDefaultTypeCast() {

        foreach (DefaultTypeCasterAttribute attribute in GetDefaultCasterAttributes()) {
            if (typeof(T) == attribute.CastType) continue;
            if (typeof(TResult) == attribute.CastType) continue;

            if (attribute.DesiredType != null && attribute.DesiredType != typeof(TResult)) continue;

            MethodInfo method = typeof(TypeCaster<T, TResult>).GetMethod(nameof(TryGetDefaultCastTypeCaster), BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo specific = method.MakeGenericMethod(attribute.CastType);

            object func = specific.Invoke(null, null);

            if (func == null) continue;

            CastFunc = (Func<T, TResult>)func;
            return true;
        }

        return false;
    }

    private static Func<T, TResult> TryGetDefaultCastTypeCaster<TDefault>() {
        //if both "T >> TDefault" and "TDefaul >> TResult" casts are invalid then this cast is invalid as well
        if (!TypeCaster<T, TDefault>.IsValid && !TypeCaster<TDefault, TResult>.IsValid) return null;

        return DefaultTypeCast<TDefault>;
    }

    private static bool TrySetCustomCaster() {
        MethodInfo selected = null;

        foreach (var m in GetCasterMethods()) {
            selected = SelectMethod(selected, GetTestedMethod(m));
        }
        if (selected != null) {
            CastFunc = CreateDelegate(selected);
            return true;
        }
        return false;
    }

    private static IEnumerable<MethodInfo> GetCasterMethods() {
        foreach (Type type in GetTypes(typeof(T), true)) {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (MethodInfo method in methods) {
                TypeCasterAttribute attribute = method.GetCustomAttribute<TypeCasterAttribute>();
                if (attribute == null) continue;

                if (method.ReturnType == typeof(void)) {
                    continue;
                }

                if (method.GetParameters().Length > 0) {
                    continue;
                }

                yield return method;
            }
        }
    }

    private static bool TrySetDefaultConverter() {
        MethodInfo method = typeof(TypeCaster<T, TResult>).GetMethod(nameof(DefaultCast), BindingFlags.Static | BindingFlags.NonPublic);
        Func<T, TResult> func;
        try {
            func = CreateDelegate(method);
        }
        catch (ArgumentException) {
            return false;
        }

        CastFunc = func;
        return true;
    }

    private static bool TrySetEnumConversionMethod() {
        //T must be an enum
        if (!typeof(T).IsEnum) return false;
        //TResult must also be an enum
        if (!typeof(TResult).IsEnum) return false;
        //underlying type of both T and TResult must be the same
        if (typeof(T).GetEnumUnderlyingType() != typeof(TResult).GetEnumUnderlyingType()) return false;

        string methodName = null;
        Type underlyingType = typeof(T).GetEnumUnderlyingType();
        if (underlyingType == typeof(int)) methodName = nameof(CastEnumToEnumInt);
        else if (underlyingType == typeof(sbyte)) methodName = nameof(CastEnumToEnumSByte);
        else if (underlyingType == typeof(byte)) methodName = nameof(CastEnumToEnumByte);
        else if (underlyingType == typeof(short)) methodName = nameof(CastEnumToEnumShort);
        else if (underlyingType == typeof(ushort)) methodName = nameof(CastEnumToEnumUShort);
        else if (underlyingType == typeof(uint)) methodName = nameof(CastEnumToEnumUInt);
        else if (underlyingType == typeof(long)) methodName = nameof(CastEnumToEnumLong);
        else if (underlyingType == typeof(ulong)) methodName = nameof(CastEnumToEnumULong);

        MethodInfo method = typeof(TypeCaster<T, TResult>).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        CastFunc = CreateDelegate(method);
        return true;
    }

    private static bool TrySetInheritCast() {
        if (!typeof(TResult).IsAssignableFrom(typeof(T))) return false;
        return TrySetDefaultConverter();
    }

    private static bool TrySetObjectCast() {
        if (typeof(TResult) != typeof(object)) return false;

        CastFunc = (Func<T, TResult>)(Delegate)(Func<T, object>)CastToSystemObject;

        return true;
    }

    private static bool TrySetOperatorMethod() {
        foreach (MethodInfo method in GetOperatorCasters()) {
            if (!StaticMethodIsCompatible(method)) continue;
            CastFunc = CreateDelegate(method);
            return true;
        }
        return false;
    }

    private static bool TrySetStaticCaster() {
        foreach (var m in GetStaticCasterMethods()) {
            if (!StaticMethodIsCompatible(m)) continue;
            CastFunc = CreateDelegate(m);
            return true;
        }
        return false;
    }

    private static IEnumerable<MethodInfo> GetStaticCasterMethods() {
        MethodInfo[] methods = typeof(GlobalTypeCasters).GetMethods(BindingFlags.Static | BindingFlags.Public);

        foreach (MethodInfo method in methods) {
            if (method.ReturnType == typeof(void)) {
                continue;
            }

            if (method.GetParameters().Length != 1) {
                continue;
            }

            if (method.IsGenericMethod) {
                Type tTGeneric = method.ReturnType;
                Type tResultGeneric = method.GetParameters()[0].ParameterType;

                char tTChar = GetGenericParameterType(tTGeneric);
                char tResultChar = GetGenericParameterType(tResultGeneric);

                foreach (var t in GetTypes(true)) {
                    MethodInfo specific = null;

                    if (tTChar == 'T' || tResultChar == 'T') {
                        try {
                            specific = method.MakeGenericMethod(t);
                        }
                        catch {
                        }
                        if (specific != null) yield return specific;
                    }

                    if (tTChar == 't' || tResultChar == 't') {
                        try {
                            specific = method.MakeGenericMethod(t.GetGenericArguments());
                        }
                        catch {
                        }
                        if (specific != null) yield return specific;
                    }
                }

                if (tTGeneric != tResultGeneric && tTChar != 's' && tResultChar != 's') {
                    foreach (var tT in GetTypes(typeof(T), true)) {

                        foreach (var tResult in GetTypes(typeof(TResult), true)) {

                            List<Type> typeArgumentList = new List<Type>();

                            if (tTChar == 'T') {
                                typeArgumentList.Add(tT);
                            }
                            else if (tTChar == 't') {
                                typeArgumentList.AddRange(tT.GetGenericArguments());
                            }
                            if (tResultChar == 'T') {
                                typeArgumentList.Add(tResult);
                            }
                            else if (tResultChar == 't') {
                                typeArgumentList.AddRange(tResult.GetGenericArguments());
                            }

                            MethodInfo specific = null;

                            Type[] typeArguments = new Type[typeArgumentList.Count]; 

                            for (int i = 0; i < typeArguments.Length; i++) {
                                typeArguments[i] = typeArgumentList[i];
                            }

                            try {
                                specific = method.MakeGenericMethod(typeArguments);
                            }
                            catch {
                            }
                            if (specific != null) yield return specific;
                        }
                    }
                }
            }
            else {
                yield return method;
            }
        }
    }

    private static char GetGenericParameterType(Type type) {
        if (type.IsGenericParameter) {
            return 'T';
        }
        else if (type.ContainsGenericParameters) {
            return 't';
        }
        else {
            return 's';
        }
    }

    private static bool StaticMethodIsCompatible(MethodInfo method) {
        if (!typeof(TResult).IsAssignableFrom(method.ReturnType)) return false;
        if (!method.GetParameters()[0].ParameterType.IsAssignableFrom(typeof(T))) return false;
        return true;
    }
}
