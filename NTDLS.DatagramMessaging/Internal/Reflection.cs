using NTDLS.DatagramMessaging.Framing;
using System;
using System.Linq;

namespace NTDLS.DatagramMessaging.Internal
{
    internal class Reflection
    {
        public static string GetAssemblyQualifiedTypeName(object obj)
        {
            return GetAssemblyQualifiedTypeName(obj.GetType());
        }

        public static string GetAssemblyQualifiedTypeName(Type type)
        {
            return DmCaching.GetOrCreateOneMinute($"AQTN:{type}", entry =>
            {
                string assemblyQualifiedName;

                if (type.IsGenericType)
                {
                    var typeDefinitionName = type.GetGenericTypeDefinition().FullName
                         ?? throw new Exception("The generic type name is not available.");

                    var assemblyName = type.Assembly.FullName
                         ?? throw new Exception("The generic assembly type name is not available.");

                    assemblyQualifiedName = $"{typeDefinitionName}, {assemblyName}";
                }
                else
                {
                    assemblyQualifiedName = type.AssemblyQualifiedName ?? type.Name
                        ?? throw new Exception("The type name is not available.");
                }

                string objectTypeName = CompiledRegEx.TypeTagsRegex().Replace(assemblyQualifiedName, string.Empty);
                objectTypeName = CompiledRegEx.TypeCleanupRegex().Replace(objectTypeName, ", ").Trim();

                return objectTypeName;
            });
        }

        public static string GetAssemblyQualifiedTypeNameWithClosedGenerics(object obj)
            => GetAssemblyQualifiedTypeNameWithClosedGenerics(obj.GetType());

        public static string GetAssemblyQualifiedTypeNameWithClosedGenerics(Type type)
        {
            return DmCaching.GetOrCreateOneMinute($"AQTN-WCG:{type}", entry =>
            {
                string assemblyQualifiedName;

                if (type.IsGenericType)
                {
                    var typeDefinitionName = type.GetGenericTypeDefinition().FullName
                         ?? throw new Exception("The generic type name is not available.");

                    var assemblyName = type.Assembly.FullName
                         ?? throw new Exception("The generic assembly type name is not available.");

                    // Recursively get the AssemblyQualifiedName of generic arguments
                    var genericArguments = type.GetGenericArguments()
                        .Select(t => t.AssemblyQualifiedName ?? GetAssemblyQualifiedTypeNameWithClosedGenerics(t));

                    string genericArgumentsString = '[' + string.Join("], [", genericArguments) + ']';

                    assemblyQualifiedName = $"{typeDefinitionName}[{genericArgumentsString}], {assemblyName}";
                }
                else
                {
                    assemblyQualifiedName = type.AssemblyQualifiedName ?? type.Name
                        ?? throw new Exception("The type name is not available.");
                }

                string objectTypeName = CompiledRegEx.TypeTagsRegex().Replace(assemblyQualifiedName, string.Empty);
                objectTypeName = CompiledRegEx.TypeCleanupRegex().Replace(objectTypeName, ", ").Trim();

                return objectTypeName;
            });
        }

        public static bool ImplementsGenericInterfaceWithArgument(Type type, Type genericInterface, Type argumentType)
        {
            return type.GetInterfaces().Any(interfaceType =>
                interfaceType.IsGenericType &&
                interfaceType.GetGenericTypeDefinition() == genericInterface &&
                interfaceType.GetGenericArguments().Any(arg => argumentType.IsAssignableFrom(arg)));
        }
    }
}
