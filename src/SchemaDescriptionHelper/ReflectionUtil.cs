namespace SchemaDescriptionHelper;

/// <summary>
/// 反射
/// </summary>
public static class ReflectionUtil
{
    public static bool InheritsOrImplements(this Type child, Type parent)
    {
        parent = ResolveGenericTypeDefinition(parent);

        var currentChild = child.IsGenericType
            ? child.GetGenericTypeDefinition()
            : child;

        while (currentChild != typeof(object))
        {
            if (parent == currentChild || HasAnyInterfaces(parent, currentChild))
                return true;

            currentChild = currentChild.BaseType is { IsGenericType: true }
                ? currentChild.BaseType.GetGenericTypeDefinition()
                : currentChild.BaseType;

            if (currentChild == null)
                return false;
        }

        return false;
    }

    private static bool HasAnyInterfaces(Type parent, Type child)
    {
        return child.GetInterfaces()
            .Any(childInterface =>
            {
                var currentInterface = childInterface.IsGenericType
                    ? childInterface.GetGenericTypeDefinition()
                    : childInterface;

                return currentInterface == parent;
            });
    }

    /// <summary>
    /// 解析类型
    /// </summary>
    /// <param name="parent"></param>
    /// <returns></returns>
    private static Type ResolveGenericTypeDefinition(Type parent)
    {

        var shouldUseGenericType = !(parent.IsGenericType && parent.GetGenericTypeDefinition() != parent);

        if (parent.IsGenericType && shouldUseGenericType)
            parent = parent.GetGenericTypeDefinition();
        return parent;
    }

}