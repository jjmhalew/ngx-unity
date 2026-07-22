#nullable enable
#if UNITY_EDITOR
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityAngularBridge.Models;
using UnityEditor;
using UnityEngine;

namespace UnityAngularBridge
{
    /// <summary>
    /// Scans the assembly for [DllImport("__Internal")] methods and generates:
    /// 1. BrowserInteractions.jslib — JavaScript bridge functions for Unity WebGL/WebGPU
    /// 2. unity-jslib-exported.service.ts — Angular service with signals and callback handlers
    ///
    /// Supports:
    /// - String/void parameters → Angular signals
    /// - [JSLibExport(IsStringArray = true)] → string[] signals
    /// - [JSLibExport(JsonType = typeof(MyDto))] → typed DTO signals via JSON.parse
    /// - Action/Action&lt;string&gt; callback parameters → request-response or registration patterns
    /// - Multi-instance routing: the jslib passes the Unity canvas id so Angular can key
    ///   signals per instance (UnityJSLibExportedService.forInstance)
    /// - XML doc comments → TSDoc in generated TypeScript
    /// - Configurable output paths via Tools &gt; UnityAngularBridge &gt; Settings
    /// </summary>
    [InitializeOnLoad]
    public class JSLibExport
    {
        private static readonly string _jsLibFileName = "BrowserInteractions.jslib";
        private static readonly string _jsLibClientFileName = "unity-jslib-exported.service.ts";
        private static readonly string _tabString = "  ";
        private static readonly List<JSLibVariable> _jSLibVariables = new();
        private static readonly Dictionary<string, string> _xmlDocs = new();

        /// <summary>
        /// Emitted at the top of every jslib function body. Unity's loader assigns the target
        /// canvas to the Emscripten Module, so its DOM id identifies the instance.
        /// </summary>
        private const string InstanceIdLine =
            "var instanceId = (typeof Module !== 'undefined' && Module['canvas'] && Module['canvas'].id) || 'default';";

        static JSLibExport()
        {
            try
            {
                BridgeGeneratorUtilities.LoadXmlDocumentation(_xmlDocs);
                ScanMethods();
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityAngularBridge] Scanning [DllImport] methods failed: {e}");
                return;
            }

            try
            {
                GenerateJSLib();
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityAngularBridge] {_jsLibFileName} generation failed: {e}");
            }

            try
            {
                GenerateJSLibClient();
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityAngularBridge] {_jsLibClientFileName} generation failed: {e}");
            }
        }

        #region Scanning

        private static void ScanMethods()
        {
            _jSLibVariables.Clear();
            Assembly assembly = Assembly.GetExecutingAssembly();
            IEnumerable<Type> publicClasses = assembly.GetExportedTypes().Where(p => p.IsClass);

            foreach (Type type in publicClasses)
            {
                IEnumerable<MethodInfo> methodInfos = type
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(m => m.GetCustomAttributes(typeof(DllImportAttribute), false).Length > 0);

                foreach (MethodInfo methodInfo in methodInfos)
                {
                    JSLibVariable variable = new()
                    {
                        MethodName = methodInfo.Name
                    };

                    // Read [JSLibExport] attribute if present
                    var jsLibExportAttr = methodInfo.GetCustomAttribute<JSLibExportAttribute>();

                    // Read documentation: attribute override > XML docs
                    string attrDoc = jsLibExportAttr?.Documentation ?? string.Empty;
                    variable.MethodDocumentation = !string.IsNullOrEmpty(attrDoc)
                        ? attrDoc
                        : BridgeGeneratorUtilities.GetXmlDocumentation(_xmlDocs, type, methodInfo);

                    variable.Category = jsLibExportAttr?.Category ?? string.Empty;

                    // Analyze parameters: classify each as data (string) or callback (Action/Action<string>)
                    ParameterInfo[] parameters = methodInfo.GetParameters();
                    string dataParamName = string.Empty;
                    bool hasCallback = false;
                    bool callbackHasStringParam = false;

                    foreach (ParameterInfo param in parameters)
                    {
                        if (param.ParameterType == typeof(Action))
                        {
                            hasCallback = true;
                            callbackHasStringParam = false;
                        }
                        else if (param.ParameterType == typeof(Action<string>))
                        {
                            hasCallback = true;
                            callbackHasStringParam = true;
                        }
                        else if (param.ParameterType == typeof(string))
                        {
                            dataParamName = param.Name ?? "data";
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                $"[UnityAngularBridge] Parameter type {param.ParameterType} on {methodInfo.Name} is not supported. " +
                                "Supported types: string, Action, Action<string>.");
                        }
                    }

                    Type? jsonType = jsLibExportAttr?.JsonType;
                    if (jsonType != null)
                    {
                        if (jsLibExportAttr!.IsStringArray)
                        {
                            throw new InvalidOperationException(
                                $"[UnityAngularBridge] {methodInfo.Name}: JsonType and IsStringArray are mutually exclusive.");
                        }
                        if (hasCallback)
                        {
                            throw new InvalidOperationException(
                                $"[UnityAngularBridge] {methodInfo.Name}: JsonType is not supported on callback methods.");
                        }
                        if (string.IsNullOrEmpty(dataParamName))
                        {
                            throw new InvalidOperationException(
                                $"[UnityAngularBridge] {methodInfo.Name}: JsonType requires a single string parameter " +
                                "(pass JsonUtility.ToJson(obj) at the call site).");
                        }
                    }

                    if (hasCallback)
                    {
                        variable.CallbackType = jsLibExportAttr?.IsCallbackRegistration == true
                            ? CallbackType.Registration
                            : CallbackType.RequestResponse;
                        variable.CallbackHasStringParam = callbackHasStringParam;
                        variable.ParameterName = dataParamName;
                        variable.ReturnType = ReturnType.Void;
                    }
                    else if (jsonType != null)
                    {
                        variable.ParameterName = dataParamName;
                        variable.ReturnType = ReturnType.Json;
                        variable.JsonType = jsonType;
                        variable.DefaultValue = "null";
                    }
                    else if (!string.IsNullOrEmpty(dataParamName))
                    {
                        variable.ParameterName = dataParamName;

                        bool isStringArray = jsLibExportAttr?.IsStringArray == true;

                        if (isStringArray)
                        {
                            variable.ReturnType = ReturnType.StringArray;
                            variable.DefaultValue = "[]";
                        }
                        else
                        {
                            variable.ReturnType = ReturnType.String;
                            variable.DefaultValue = "null";
                        }
                    }
                    else
                    {
                        variable.ReturnType = ReturnType.Void;
                        variable.DefaultValue = string.Empty;
                    }

                    _jSLibVariables.Add(variable);
                }
            }
        }

        #endregion

        #region GenerateJSLib

        private static void GenerateJSLib()
        {
            using StringWriter stringWriter = new();
            using (IndentedTextWriter writer = new(stringWriter, _tabString))
            {
                writer.WriteLine("mergeInto(LibraryManager.library, {");
                writer.Indent++;

                foreach (JSLibVariable variable in _jSLibVariables)
                {
                    WriteJSLibMethod(writer, variable);
                }

                writer.Indent--;
                writer.WriteLine("});");
            }

            BridgeGeneratorUtilities.WriteFileIfChanged(
                Path.Combine(GetPluginsPath(), _jsLibFileName), stringWriter.ToString());
        }

        private static void WriteJSLibMethod(IndentedTextWriter writer, JSLibVariable variable)
        {
            string methodName = variable.MethodName;
            string windowFnName = $"{BridgeGeneratorUtilities.FirstCharToLowerCase(methodName)}FromUnity";
            bool hasDataParam = !string.IsNullOrEmpty(variable.ParameterName);

            if (variable.CallbackType == CallbackType.RequestResponse)
            {
                // Request-response: optional data param + callback
                if (hasDataParam)
                {
                    writer.WriteLine($"{methodName}: function ({variable.ParameterName}Ptr, callbackPtr)" + " {");
                    writer.Indent++;
                    writer.WriteLine(InstanceIdLine);
                    writer.WriteLine($"var {variable.ParameterName} = UTF8ToString({variable.ParameterName}Ptr);");
                }
                else
                {
                    writer.WriteLine($"{methodName}: function (callbackPtr)" + " {");
                    writer.Indent++;
                    writer.WriteLine(InstanceIdLine);
                }

                if (variable.CallbackHasStringParam)
                {
                    string dataArg = hasDataParam ? variable.ParameterName + ", " : "";
                    writer.WriteLine($"window.{windowFnName}({dataArg}function (result)" + " {");
                    writer.Indent++;
                    writer.WriteLine("var bufferSize = lengthBytesUTF8(result) + 1;");
                    writer.WriteLine("var buffer = _malloc(bufferSize);");
                    writer.WriteLine("stringToUTF8(result, buffer, bufferSize);");
                    writer.WriteLine("{{{ makeDynCall('vi', 'callbackPtr') }}}(buffer);");
                    writer.WriteLine("_free(buffer);");
                    writer.Indent--;
                    writer.WriteLine("}, instanceId);");
                }
                else
                {
                    string dataArg = hasDataParam ? variable.ParameterName + ", " : "";
                    writer.WriteLine($"window.{windowFnName}({dataArg}function ()" + " {");
                    writer.Indent++;
                    writer.WriteLine("{{{ makeDynCall('v', 'callbackPtr') }}}();");
                    writer.Indent--;
                    writer.WriteLine("}, instanceId);");
                }

                writer.Indent--;
                writer.WriteLine("},");
                writer.WriteLine();
            }
            else if (variable.CallbackType == CallbackType.Registration)
            {
                // Registration: Unity passes a C# callback pointer, JS wraps it for Angular to call later
                writer.WriteLine($"{methodName}: function (callbackPtr)" + " {");
                writer.Indent++;
                writer.WriteLine(InstanceIdLine);

                if (variable.CallbackHasStringParam)
                {
                    writer.WriteLine($"window.{windowFnName}(function (data)" + " {");
                    writer.Indent++;
                    writer.WriteLine("var bufferSize = lengthBytesUTF8(data) + 1;");
                    writer.WriteLine("var buffer = _malloc(bufferSize);");
                    writer.WriteLine("stringToUTF8(data, buffer, bufferSize);");
                    writer.WriteLine("{{{ makeDynCall('vi', 'callbackPtr') }}}(buffer);");
                    writer.WriteLine("_free(buffer);");
                    writer.Indent--;
                    writer.WriteLine("}, instanceId);");
                }
                else
                {
                    writer.WriteLine($"window.{windowFnName}(function ()" + " {");
                    writer.Indent++;
                    writer.WriteLine("{{{ makeDynCall('v', 'callbackPtr') }}}();");
                    writer.Indent--;
                    writer.WriteLine("}, instanceId);");
                }

                writer.Indent--;
                writer.WriteLine("},");
                writer.WriteLine();
            }
            else if (hasDataParam)
            {
                // Regular method with string parameter
                writer.WriteLine($"{methodName}: function ({variable.ParameterName}, size)" + " {");
                writer.Indent++;
                writer.WriteLine(InstanceIdLine);
                writer.WriteLine($"window.{windowFnName}(UTF8ToString({variable.ParameterName}), instanceId);");
                writer.Indent--;
                writer.WriteLine("},");
                writer.WriteLine();
            }
            else
            {
                // No parameter method
                writer.WriteLine($"{methodName}: function ()" + " {");
                writer.Indent++;
                writer.WriteLine(InstanceIdLine);
                writer.WriteLine($"window.{windowFnName}(instanceId);");
                writer.Indent--;
                writer.WriteLine("},");
                writer.WriteLine();
            }
        }

        #endregion

        #region GenerateJSLibClient

        private static void GenerateJSLibClient()
        {
            using StringWriter stringWriter = new();
            using (IndentedTextWriter writer = new(stringWriter, _tabString))
            {
                WriteAutoGeneratedHeader(writer);
                WriteImports(writer);
                TypeScriptInterfaceGenerator.WriteInterfaces(
                    writer,
                    _jSLibVariables.Where(v => v.JsonType != null).Select(v => v.JsonType).Distinct());
                WriteModuleScopeSignals(writer);
                WriteModuleScopeCallbackHolders(writer);
                WriteInstanceChannel(writer);
                WriteWindowCallbacks(writer);
                WriteServiceClass(writer);
            }

            string outputPath = UnityAngularBridgeSettings.GetJSLibServiceOutputPath();
            BridgeGeneratorUtilities.WriteFileIfChanged(
                Path.Combine(outputPath, _jsLibClientFileName), stringWriter.ToString());
        }

        private static void WriteAutoGeneratedHeader(IndentedTextWriter writer)
        {
            writer.WriteLine("//----------------------");
            writer.WriteLine("// <auto-generated>");
            writer.WriteLine("//    Generated using JSLibExport.cs in UnityAngularBridge project.");
            writer.WriteLine("// </auto-generated>");
            writer.WriteLine("//----------------------");
            writer.WriteLine();
            writer.WriteLine("/* eslint-disable */");
            writer.WriteLine();
        }

        private static void WriteImports(IndentedTextWriter writer)
        {
            writer.WriteLine("import { Injectable, signal, WritableSignal, Signal } from \"@angular/core\";");
            writer.WriteLine();
        }

        private static string GetSignalTsType(JSLibVariable variable)
        {
            return variable.ReturnType switch
            {
                ReturnType.Void => "number",
                ReturnType.String => "string | null",
                ReturnType.StringArray => "string[]",
                ReturnType.Json => $"{variable.JsonType.Name} | null",
                _ => "unknown",
            };
        }

        private static string GetSignalInitializer(JSLibVariable variable)
        {
            return variable.ReturnType == ReturnType.Void
                ? "signal<number>(0)"
                : $"signal<{GetSignalTsType(variable)}>({variable.DefaultValue}, {{ equal: () => false }})";
        }

        /// <summary>
        /// Handler signature for RequestResponse variables, e.g.
        /// "(query: string, respond: (result: string) =&gt; void) =&gt; void".
        /// </summary>
        private static string GetRequestResponseHandlerSignature(JSLibVariable variable)
        {
            bool hasData = !string.IsNullOrEmpty(variable.ParameterName);
            string respond = variable.CallbackHasStringParam
                ? "respond: (result: string) => void"
                : "respond: () => void";
            return hasData ? $"(query: string, {respond}) => void" : $"({respond}) => void";
        }

        private static string GetRegistrationCallbackSignature(JSLibVariable variable)
        {
            return variable.CallbackHasStringParam ? "(data: string) => void" : "() => void";
        }

        private static string GetRegistrationBaseName(JSLibVariable variable)
        {
            return variable.MethodName.StartsWith("Register")
                ? variable.MethodName.Substring("Register".Length)
                : variable.MethodName;
        }

        private static void WriteModuleScopeSignals(IndentedTextWriter writer)
        {
            var signalVars = _jSLibVariables.Where(v => v.CallbackType == CallbackType.None).ToList();
            if (!signalVars.Any()) return;

            writer.WriteLine("// Module-scope writable signals (accessed by window callbacks below).");
            foreach (JSLibVariable variable in signalVars)
            {
                string name = $"{BridgeGeneratorUtilities.FirstCharToLowerCase(variable.MethodName)}Signal";
                writer.WriteLine($"const {name}: WritableSignal<{GetSignalTsType(variable)}> = {GetSignalInitializer(variable)};");
            }
            writer.WriteLine();
        }

        private static void WriteModuleScopeCallbackHolders(IndentedTextWriter writer)
        {
            var callbackVars = _jSLibVariables.Where(v => v.CallbackType != CallbackType.None).ToList();
            if (!callbackVars.Any()) return;

            writer.WriteLine("// Module-scope callback holders.");
            foreach (JSLibVariable variable in callbackVars)
            {
                string name = BridgeGeneratorUtilities.FirstCharToLowerCase(variable.MethodName)!;

                if (variable.CallbackType == CallbackType.RequestResponse)
                {
                    writer.WriteLine($"let {name}Handler: ({GetRequestResponseHandlerSignature(variable)}) | null = null;");
                }
                else // Registration
                {
                    writer.WriteLine($"let {name}Callback: ({GetRegistrationCallbackSignature(variable)}) | null = null;");
                }
            }
            writer.WriteLine();
        }

        private static void WriteInstanceChannel(IndentedTextWriter writer)
        {
            var signalVars = _jSLibVariables.Where(v => v.CallbackType == CallbackType.None).ToList();
            var callbackVars = _jSLibVariables.Where(v => v.CallbackType != CallbackType.None).ToList();

            writer.WriteLine("/** Per-instance signal set and callback holders, keyed by the Unity canvas DOM id. */");
            writer.WriteLine("export class UnityJSLibInstanceChannel {");
            writer.Indent++;

            foreach (JSLibVariable variable in signalVars)
            {
                string name = BridgeGeneratorUtilities.FirstCharToLowerCase(variable.MethodName)!;
                string tsType = GetSignalTsType(variable);
                writer.WriteLine($"readonly {name}Signal: WritableSignal<{tsType}> = {GetSignalInitializer(variable)};");
                writer.WriteLine($"readonly {name}: Signal<{tsType}> = this.{name}Signal.asReadonly();");
            }

            if (callbackVars.Any())
            {
                writer.WriteLine();
                foreach (JSLibVariable variable in callbackVars)
                {
                    string name = BridgeGeneratorUtilities.FirstCharToLowerCase(variable.MethodName)!;

                    if (variable.CallbackType == CallbackType.RequestResponse)
                    {
                        writer.WriteLine($"{name}Handler: ({GetRequestResponseHandlerSignature(variable)}) | null = null;");
                    }
                    else // Registration
                    {
                        writer.WriteLine($"{name}Callback: ({GetRegistrationCallbackSignature(variable)}) | null = null;");
                    }
                }

                foreach (JSLibVariable variable in callbackVars)
                {
                    string name = BridgeGeneratorUtilities.FirstCharToLowerCase(variable.MethodName)!;
                    writer.WriteLine();

                    if (variable.CallbackType == CallbackType.RequestResponse)
                    {
                        writer.WriteLine($"/** Register a handler for {variable.MethodName} requests from this instance. */");
                        writer.WriteLine($"register{variable.MethodName}Handler(handler: {GetRequestResponseHandlerSignature(variable)}): void {{");
                        writer.Indent++;
                        writer.WriteLine($"this.{name}Handler = handler;");
                        writer.Indent--;
                        writer.WriteLine("}");
                    }
                    else // Registration
                    {
                        string baseName = GetRegistrationBaseName(variable);
                        writer.WriteLine($"/** Invoke the callback registered by Unity via {variable.MethodName} for this instance. */");
                        if (variable.CallbackHasStringParam)
                        {
                            writer.WriteLine($"notify{baseName}(data: string): void {{");
                            writer.Indent++;
                            writer.WriteLine($"this.{name}Callback?.(data);");
                        }
                        else
                        {
                            writer.WriteLine($"notify{baseName}(): void {{");
                            writer.Indent++;
                            writer.WriteLine($"this.{name}Callback?.();");
                        }
                        writer.Indent--;
                        writer.WriteLine("}");
                    }
                }
            }

            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
            writer.WriteLine("const channels = new Map<string, UnityJSLibInstanceChannel>();");
            writer.WriteLine();
            writer.WriteLine("function getOrCreateChannel(instanceId: string): UnityJSLibInstanceChannel {");
            writer.Indent++;
            writer.WriteLine("let channel = channels.get(instanceId);");
            writer.WriteLine("if (!channel) {");
            writer.Indent++;
            writer.WriteLine("channel = new UnityJSLibInstanceChannel();");
            writer.WriteLine("channels.set(instanceId, channel);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("return channel;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
        }

        private static void WriteWindowCallbacks(IndentedTextWriter writer)
        {
            writer.WriteLine("// Register window callbacks invoked by Unity's jslib.");
            writer.WriteLine("// The trailing instanceId is appended by the generated jslib (Unity canvas id);");
            writer.WriteLine("// calls without it (older jslib builds) are routed to the \"default\" channel.");
            writer.WriteLine("/* eslint-disable @typescript-eslint/no-explicit-any */");

            foreach (JSLibVariable variable in _jSLibVariables)
            {
                string methodNameLower = BridgeGeneratorUtilities.FirstCharToLowerCase(variable.MethodName)!;
                string windowFnName = $"{methodNameLower}FromUnity";

                if (variable.CallbackType == CallbackType.RequestResponse)
                {
                    bool hasData = !string.IsNullOrEmpty(variable.ParameterName);
                    string paramNameLower = hasData ? BridgeGeneratorUtilities.FirstCharToLowerCase(variable.ParameterName)! : "";
                    string respondType = variable.CallbackHasStringParam ? "(result: string) => void" : "() => void";
                    string handlerArgs = hasData ? $"{paramNameLower}, respond" : "respond";
                    string fnParams = hasData
                        ? $"{paramNameLower}: string, respond: {respondType}, instanceId?: string"
                        : $"respond: {respondType}, instanceId?: string";

                    writer.WriteLine($"(window as any)[\"{windowFnName}\"] = ({fnParams}): void => {{");
                    writer.Indent++;
                    writer.WriteLine($"const channelHandler = channels.get(instanceId ?? \"default\")?.{methodNameLower}Handler;");
                    writer.WriteLine("if (channelHandler) {");
                    writer.Indent++;
                    writer.WriteLine($"channelHandler({handlerArgs});");
                    writer.Indent--;
                    writer.WriteLine("} else {");
                    writer.Indent++;
                    writer.WriteLine($"{methodNameLower}Handler?.({handlerArgs});");
                    writer.Indent--;
                    writer.WriteLine("}");
                    writer.Indent--;
                    writer.WriteLine("};");
                }
                else if (variable.CallbackType == CallbackType.Registration)
                {
                    writer.WriteLine($"(window as any)[\"{windowFnName}\"] = (handler: {GetRegistrationCallbackSignature(variable)}, instanceId?: string): void => {{");
                    writer.Indent++;
                    writer.WriteLine($"{methodNameLower}Callback = handler;");
                    writer.WriteLine($"getOrCreateChannel(instanceId ?? \"default\").{methodNameLower}Callback = handler;");
                    writer.Indent--;
                    writer.WriteLine("};");
                }
                else if (!string.IsNullOrEmpty(variable.ParameterName))
                {
                    string paramNameLower = BridgeGeneratorUtilities.FirstCharToLowerCase(variable.ParameterName)!;
                    writer.WriteLine($"(window as any)[\"{windowFnName}\"] = ({paramNameLower}: string, instanceId?: string): void => {{");
                    writer.Indent++;

                    if (variable.ReturnType == ReturnType.StringArray)
                    {
                        writer.WriteLine($"const values = {paramNameLower} === \"\" ? [] : {paramNameLower}.split(\"|\");");
                        writer.WriteLine($"{methodNameLower}Signal.set(values);");
                        writer.WriteLine($"getOrCreateChannel(instanceId ?? \"default\").{methodNameLower}Signal.set(values);");
                    }
                    else if (variable.ReturnType == ReturnType.Json)
                    {
                        writer.WriteLine("try {");
                        writer.Indent++;
                        writer.WriteLine($"const value = JSON.parse({paramNameLower}) as {variable.JsonType.Name};");
                        writer.WriteLine($"{methodNameLower}Signal.set(value);");
                        writer.WriteLine($"getOrCreateChannel(instanceId ?? \"default\").{methodNameLower}Signal.set(value);");
                        writer.Indent--;
                        writer.WriteLine("} catch (e) {");
                        writer.Indent++;
                        writer.WriteLine($"console.error(\"[UnityAngularBridge] Failed to parse {variable.MethodName} JSON:\", e);");
                        writer.Indent--;
                        writer.WriteLine("}");
                    }
                    else
                    {
                        writer.WriteLine($"{methodNameLower}Signal.set({paramNameLower});");
                        writer.WriteLine($"getOrCreateChannel(instanceId ?? \"default\").{methodNameLower}Signal.set({paramNameLower});");
                    }

                    writer.Indent--;
                    writer.WriteLine("};");
                }
                else
                {
                    writer.WriteLine($"(window as any)[\"{windowFnName}\"] = (instanceId?: string): void => {{");
                    writer.Indent++;
                    writer.WriteLine($"{methodNameLower}Signal.update(v => v + 1);");
                    writer.WriteLine($"getOrCreateChannel(instanceId ?? \"default\").{methodNameLower}Signal.update(v => v + 1);");
                    writer.Indent--;
                    writer.WriteLine("};");
                }
            }
            writer.WriteLine();
        }

        private static void WriteServiceClass(IndentedTextWriter writer)
        {
            writer.WriteLine("/**");
            writer.WriteLine(" * Auto-generated service for Unity → Angular communication.");
            writer.WriteLine(" * Signals are updated when Unity calls the corresponding jslib functions.");
            writer.WriteLine(" * Register callback handlers to respond to Unity requests.");
            writer.WriteLine(" * The flat signals reflect the last event from any instance; use forInstance()");
            writer.WriteLine(" * to observe a single Unity instance when multiple viewports are on the page.");
            writer.WriteLine(" * See: https://docs.unity3d.com/Manual/webgl-interactingwithbrowserscripting.html");
            writer.WriteLine(" */");
            writer.WriteLine("@Injectable({");
            writer.Indent++;
            writer.WriteLine("providedIn: \"root\",");
            writer.Indent--;
            writer.WriteLine("})");
            writer.WriteLine("export class UnityJSLibExportedService {");
            writer.Indent++;

            // Signal properties
            var signalVars = _jSLibVariables.Where(v => v.CallbackType == CallbackType.None).ToList();
            foreach (JSLibVariable variable in signalVars)
            {
                string name = BridgeGeneratorUtilities.FirstCharToLowerCase(variable.MethodName)!;
                string signalName = $"{name}Signal";

                if (!string.IsNullOrEmpty(variable.MethodDocumentation))
                {
                    writer.WriteLine($"/** {variable.MethodDocumentation} */");
                }

                writer.WriteLine($"readonly {name}: Signal<{GetSignalTsType(variable)}> = {signalName}.asReadonly();");
            }

            // Per-instance access
            writer.WriteLine();
            writer.WriteLine("/** Per-instance view of the Unity → Angular channels, keyed by the Unity canvas DOM id. */");
            writer.WriteLine("forInstance(instanceId: string): UnityJSLibInstanceChannel {");
            writer.Indent++;
            writer.WriteLine("return getOrCreateChannel(instanceId);");
            writer.Indent--;
            writer.WriteLine("}");

            // Callback methods
            var callbackVars = _jSLibVariables.Where(v => v.CallbackType != CallbackType.None).ToList();

            foreach (JSLibVariable variable in callbackVars)
            {
                string name = BridgeGeneratorUtilities.FirstCharToLowerCase(variable.MethodName)!;
                writer.WriteLine();

                if (variable.CallbackType == CallbackType.RequestResponse)
                {
                    string doc = !string.IsNullOrEmpty(variable.MethodDocumentation)
                        ? variable.MethodDocumentation
                        : $"Register a handler for {variable.MethodName} requests from Unity.";
                    writer.WriteLine($"/** {doc} Used for any instance without its own forInstance() handler. */");
                    writer.WriteLine($"register{variable.MethodName}Handler(handler: {GetRequestResponseHandlerSignature(variable)}): void {{");
                    writer.Indent++;
                    writer.WriteLine($"{name}Handler = handler;");
                    writer.Indent--;
                    writer.WriteLine("}");
                }
                else // Registration
                {
                    string baseName = GetRegistrationBaseName(variable);
                    string doc = !string.IsNullOrEmpty(variable.MethodDocumentation)
                        ? variable.MethodDocumentation
                        : $"Invoke the callback registered by Unity via {variable.MethodName}.";
                    writer.WriteLine($"/** {doc} Broadcasts to all instances; use forInstance() to target one. */");

                    if (variable.CallbackHasStringParam)
                    {
                        writer.WriteLine($"notify{baseName}(data: string): void {{");
                        writer.Indent++;
                        writer.WriteLine("if (channels.size > 0) {");
                        writer.Indent++;
                        writer.WriteLine($"channels.forEach((channel) => channel.{name}Callback?.(data));");
                        writer.Indent--;
                        writer.WriteLine("} else {");
                        writer.Indent++;
                        writer.WriteLine($"{name}Callback?.(data);");
                        writer.Indent--;
                        writer.WriteLine("}");
                    }
                    else
                    {
                        writer.WriteLine($"notify{baseName}(): void {{");
                        writer.Indent++;
                        writer.WriteLine("if (channels.size > 0) {");
                        writer.Indent++;
                        writer.WriteLine($"channels.forEach((channel) => channel.{name}Callback?.());");
                        writer.Indent--;
                        writer.WriteLine("} else {");
                        writer.Indent++;
                        writer.WriteLine($"{name}Callback?.();");
                        writer.Indent--;
                        writer.WriteLine("}");
                    }
                    writer.Indent--;
                    writer.WriteLine("}");
                }
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        #endregion

        #region Utilities

        private static string GetPluginsPath()
        {
            string path = Application.dataPath + "/Plugins";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        #endregion
    }
}
#endif
