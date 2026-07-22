#nullable enable
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using UnityEngine;

namespace UnityAngularBridge
{
    /// <summary>
    /// Shared helpers for the UnityAngularBridge code generators.
    /// </summary>
    internal static class BridgeGeneratorUtilities
    {
        /// <summary>
        /// Loads XML documentation summaries from Assembly-CSharp.xml into the given dictionary.
        /// </summary>
        internal static void LoadXmlDocumentation(Dictionary<string, string> xmlDocs)
        {
            xmlDocs.Clear();
            try
            {
                string xmlPath = Path.Combine(Application.dataPath, "..", "Library", "ScriptAssemblies", "Assembly-CSharp.xml");
                if (!File.Exists(xmlPath)) return;

                XDocument doc = XDocument.Load(xmlPath);
                foreach (var member in doc.Descendants("member"))
                {
                    string? name = member.Attribute("name")?.Value;
                    string? summary = member.Element("summary")?.Value;
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(summary))
                    {
                        string cleaned = summary.Trim().Replace("\r\n", " ").Replace("\n", " ");
                        while (cleaned.Contains("  "))
                            cleaned = cleaned.Replace("  ", " ");
                        xmlDocs[name] = cleaned;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityAngularBridge] Could not load XML documentation: {e.Message}");
            }
        }

        /// <summary>
        /// Looks up the XML documentation summary for a method.
        /// </summary>
        internal static string GetXmlDocumentation(Dictionary<string, string> xmlDocs, Type type, MethodInfo methodInfo)
        {
            string paramTypes = string.Join(",",
                methodInfo.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name));
            string memberName = methodInfo.GetParameters().Length > 0
                ? $"M:{type.FullName}.{methodInfo.Name}({paramTypes})"
                : $"M:{type.FullName}.{methodInfo.Name}";

            return xmlDocs.TryGetValue(memberName, out string? doc) ? doc : string.Empty;
        }

        /// <summary>
        /// Lowercases the first character (PascalCase → camelCase).
        /// </summary>
        internal static string? FirstCharToLowerCase(string? str)
        {
            if (!string.IsNullOrEmpty(str) && char.IsUpper(str[0]))
            {
                return str.Length == 1 ? char.ToLower(str[0]).ToString() : char.ToLower(str[0]) + str[1..];
            }
            return str;
        }

        /// <summary>
        /// Writes content only if it differs from the existing file, so unchanged output
        /// does not trigger file watchers (e.g. the Angular dev server) on every domain reload.
        /// </summary>
        internal static void WriteFileIfChanged(string path, string content)
        {
            if (File.Exists(path) && File.ReadAllText(path) == content) return;
            File.WriteAllText(path, content);
        }
    }
}
#endif
