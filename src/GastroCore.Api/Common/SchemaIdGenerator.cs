namespace GastroCore.Api.Common;

public sealed class SchemaIdGenerator
{
    private static readonly HashSet<Type> InlineTypes =
    [
        typeof(string), typeof(decimal), typeof(DateTime), typeof(DateTimeOffset),
        typeof(DateOnly), typeof(TimeOnly), typeof(TimeSpan), typeof(Guid),
        typeof(Uri), typeof(byte[]), typeof(object)
    ];

    private static readonly HashSet<Type> DictionaryTypes =
    [
        typeof(Dictionary<,>), typeof(IDictionary<,>), typeof(IReadOnlyDictionary<,>)
    ];

    private static readonly HashSet<Type> CollectionTypes =
    [
        typeof(List<>), typeof(IList<>), typeof(ICollection<>),
        typeof(IEnumerable<>), typeof(IReadOnlyList<>), typeof(IReadOnlyCollection<>)
    ];

    public static string? Create(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return Create(underlying);

        if (type.IsPrimitive || InlineTypes.Contains(type))
            return null;

        if (type.IsArray)
            return Create(type.GetElementType()!) is { } element ? $"{element}[]" : null;

        if (!type.IsGenericType)
            return type.IsNested
                ? $"{Create(type.DeclaringType!)}.{type.Name}"
                : type.Name;

        var definition = type.GetGenericTypeDefinition();
        var args = type.GetGenericArguments();

        if (DictionaryTypes.Contains(definition))
            return $"DictionaryOf{Name(args[0])}And{Name(args[1])}";

        if (CollectionTypes.Contains(definition))
            return $"ListOf{Name(args[0])}";

        var baseName = type.Name[..type.Name.IndexOf('`')];
        return $"{baseName}Of{string.Join("And", args.Select(Name))}";
    }

    private static string Name(Type type)
    {
        return Create(type) ?? "Object";
    }
}