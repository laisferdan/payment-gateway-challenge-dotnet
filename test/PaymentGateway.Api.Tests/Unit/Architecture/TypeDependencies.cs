using System.Reflection;
using System.Reflection.Emit;

namespace PaymentGateway.Api.Tests.Unit.Architecture;

/// <summary>
/// Every type a type uses – in its signatures, attributes and method bodies (IL), including its
/// nested and compiler-generated types (lambdas, async state machines). Reading the IL catches what a
/// <c>using</c> check cannot: the Web SDK's implicit global usings make ASP.NET Core, HTTP and logging
/// types available in every file without a visible <c>using</c>.
/// </summary>
internal static class TypeDependencies
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private const byte TwoByteOpCodePrefix = 0xFE;

    private static readonly OpCode[] OneByteOpCodes = new OpCode[0x100];
    private static readonly OpCode[] TwoByteOpCodes = new OpCode[0x100];

    static TypeDependencies()
    {
        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            OpCode opCode = (OpCode)field.GetValue(null)!;
            ushort value = unchecked((ushort)opCode.Value);
            if (opCode.Size == 1)
            {
                OneByteOpCodes[value] = opCode;
            }
            else
            {
                TwoByteOpCodes[value & 0xFF] = opCode;
            }
        }
    }

    /// <summary>
    /// "<c>Type -> Dependency</c>" for every type in <paramref name="layer"/> (a namespace of the
    /// gateway) that uses a type in <paramref name="forbiddenNamespace"/> or below it.
    /// </summary>
    public static IReadOnlyList<string> FromLayerOn(string layer, string forbiddenNamespace)
    {
        return typeof(Program).Assembly.GetTypes()
            .Where(type => !type.IsNested && IsIn(type, layer))
            .SelectMany(type => Of(type)
                .Where(dependency => IsIn(dependency, forbiddenNamespace))
                .Select(dependency => $"{type.FullName} -> {dependency.FullName}"))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlySet<Type> Of(Type type)
    {
        HashSet<Type> dependencies = [];
        CollectType(type, dependencies);
        return dependencies;
    }

    // A nested type reports its outermost type's namespace, so compiler-generated types count too.
    private static bool IsIn(Type type, string @namespace)
    {
        return type.Namespace is string typeNamespace
            && (typeNamespace == @namespace || typeNamespace.StartsWith(@namespace + ".", StringComparison.Ordinal));
    }

    private static void CollectType(Type type, HashSet<Type> dependencies)
    {
        Add(type.BaseType, dependencies);
        foreach (Type implemented in type.GetInterfaces())
        {
            Add(implemented, dependencies);
        }

        AddAttributes(type.GetCustomAttributesData(), dependencies);

        foreach (FieldInfo field in type.GetFields(Declared))
        {
            Add(field.FieldType, dependencies);
            AddAttributes(field.GetCustomAttributesData(), dependencies);
        }

        foreach (PropertyInfo property in type.GetProperties(Declared))
        {
            Add(property.PropertyType, dependencies);
            AddAttributes(property.GetCustomAttributesData(), dependencies);
        }

        foreach (MethodBase method in type.GetMethods(Declared).Concat<MethodBase>(type.GetConstructors(Declared)))
        {
            CollectMethod(method, dependencies);
        }

        foreach (Type nested in type.GetNestedTypes(Declared))
        {
            CollectType(nested, dependencies);
        }
    }

    private static void CollectMethod(MethodBase method, HashSet<Type> dependencies)
    {
        AddAttributes(method.GetCustomAttributesData(), dependencies);
        IEnumerable<ParameterInfo> parameters = method is MethodInfo info
            ? method.GetParameters().Append(info.ReturnParameter)
            : method.GetParameters();
        foreach (ParameterInfo parameter in parameters)
        {
            Add(parameter.ParameterType, dependencies);
            AddAttributes(parameter.GetCustomAttributesData(), dependencies);
        }

        MethodBody? body = method.GetMethodBody();
        if (body is null)
        {
            return;
        }

        foreach (LocalVariableInfo local in body.LocalVariables)
        {
            Add(local.LocalType, dependencies);
        }

        foreach (ExceptionHandlingClause clause in body.ExceptionHandlingClauses)
        {
            if (clause.Flags == ExceptionHandlingClauseOptions.Clause)
            {
                Add(clause.CatchType, dependencies);
            }
        }

        CollectInstructions(method, body.GetILAsByteArray() ?? [], dependencies);
    }

    // Resolves the operand of every instruction that names a type, method or field.
    private static void CollectInstructions(MethodBase method, byte[] il, HashSet<Type> dependencies)
    {
        Type[]? typeArguments = method.DeclaringType is { IsGenericType: true } declaringType
            ? declaringType.GetGenericArguments()
            : null;
        Type[]? methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;

        int position = 0;
        while (position < il.Length)
        {
            OpCode opCode = il[position] == TwoByteOpCodePrefix
                ? TwoByteOpCodes[il[position + 1]]
                : OneByteOpCodes[il[position]];
            position += opCode.Size;

            if (opCode.OperandType is OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineType or OperandType.InlineTok)
            {
                int token = BitConverter.ToInt32(il, position);
                AddMember(method.Module.ResolveMember(token, typeArguments, methodArguments), dependencies);
            }

            position += OperandSize(opCode.OperandType, il, position);
        }
    }

    private static int OperandSize(OperandType operandType, byte[] il, int position)
    {
        return operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, position)),
            _ => 4,
        };
    }

    private static void AddMember(MemberInfo? member, HashSet<Type> dependencies)
    {
        switch (member)
        {
            case Type type:
                Add(type, dependencies);
                break;
            case FieldInfo field:
                Add(field.DeclaringType, dependencies);
                Add(field.FieldType, dependencies);
                break;
            case MethodBase method:
                Add(method.DeclaringType, dependencies);
                if (method is MethodInfo { IsGenericMethod: true } genericMethod)
                {
                    foreach (Type argument in genericMethod.GetGenericArguments())
                    {
                        Add(argument, dependencies);
                    }
                }

                break;
        }
    }

    private static void AddAttributes(IEnumerable<CustomAttributeData> attributes, HashSet<Type> dependencies)
    {
        foreach (CustomAttributeData attribute in attributes)
        {
            Add(attribute.AttributeType, dependencies);
        }
    }

    // Arrays, by-refs and generic arguments count as the types they are made of.
    private static void Add(Type? type, HashSet<Type> dependencies)
    {
        if (type is null || type.IsGenericParameter)
        {
            return;
        }

        if (type.HasElementType)
        {
            Add(type.GetElementType(), dependencies);
            return;
        }

        if (!dependencies.Add(type) || !type.IsGenericType)
        {
            return;
        }

        foreach (Type argument in type.GetGenericArguments())
        {
            Add(argument, dependencies);
        }
    }
}
