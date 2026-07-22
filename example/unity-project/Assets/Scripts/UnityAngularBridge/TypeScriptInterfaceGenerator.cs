#nullable enable
#if UNITY_EDITOR
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;

namespace UnityAngularBridge
{
    /// <summary>
    /// Emits TypeScript interfaces for [Serializable] DTO classes referenced via JsonType
    /// on [AngularExposed] / [JSLibExport] attributes.
    /// Supported fields (mirroring JsonUtility): public instance fields of string, int, long,
    /// float, double, bool, arrays/List&lt;T&gt; of those, and nested [Serializable] classes.
    /// Properties, dictionaries, and polymorphism are not supported.
    /// </summary>
    internal static class TypeScriptInterfaceGenerator
    {
        /// <summary>
        /// Writes TS interfaces for the given DTO types (plus any nested DTO types they reference).
        /// Throws on unsupported field types, non-[Serializable] types, and short-name collisions.
        /// </summary>
        internal static void WriteInterfaces(IndentedTextWriter writer, IEnumerable<Type> dtoTypes)
        {
            Dictionary<string, Type> emittedByName = new();
            List<Type> queue = new(dtoTypes);
            HashSet<Type> queued = new(queue);

            for (int i = 0; i < queue.Count; i++)
            {
                Type type = queue[i];

                if (!type.IsSerializable)
                {
                    throw new InvalidOperationException(
                        $"[UnityAngularBridge] JsonType {type.FullName} must be marked [Serializable] so JsonUtility can handle it.");
                }

                if (emittedByName.TryGetValue(type.Name, out Type? existing))
                {
                    if (existing == type) continue;
                    throw new InvalidOperationException(
                        $"[UnityAngularBridge] Two DTO types share the short name '{type.Name}' ({existing.FullName} and {type.FullName}). Rename one of them.");
                }
                emittedByName[type.Name] = type;

                writer.WriteLine($"export interface {type.Name} {{");
                writer.Indent++;
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    writer.WriteLine($"{field.Name}: {ToTypeScriptType(field, queue, queued)};");
                }
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine();
            }
        }

        private static string ToTypeScriptType(FieldInfo field, List<Type> queue, HashSet<Type> queued)
        {
            Type type = field.FieldType;
            bool isArray = false;

            if (type.IsArray)
            {
                isArray = true;
                type = type.GetElementType()!;
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                isArray = true;
                type = type.GetGenericArguments()[0];
            }

            string tsType;
            if (type == typeof(string))
            {
                tsType = "string";
            }
            else if (type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double))
            {
                tsType = "number";
            }
            else if (type == typeof(bool))
            {
                tsType = "boolean";
            }
            else if (type.IsClass && type.IsSerializable)
            {
                // Nested DTO — queue it for its own interface.
                if (queued.Add(type)) queue.Add(type);
                tsType = type.Name;
            }
            else
            {
                throw new InvalidOperationException(
                    $"[UnityAngularBridge] Field '{field.DeclaringType?.Name}.{field.Name}' has unsupported type {type}. " +
                    "Supported: string, int, long, float, double, bool, arrays/List<T> of those, and nested [Serializable] classes.");
            }

            return isArray ? $"{tsType}[]" : tsType;
        }
    }
}
#endif
