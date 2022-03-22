using System;

/// <summary>Specifies the type to be used as intermediate type when no viable caster method was found</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum, AllowMultiple = true)]
public class DefaultTypeCasterAttribute : Attribute {

    public Type CastType { get; }

    public Type DesiredType { get; }

    public DefaultTypeCasterAttribute(Type CastType) {
        this.CastType = CastType;
    }

    public DefaultTypeCasterAttribute(Type CastType, Type DesiredType) {
        this.CastType = CastType;
        this.DesiredType = DesiredType;
    }

}