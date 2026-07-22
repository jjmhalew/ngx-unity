namespace UnityAngularBridge.Models
{
    /// <summary>
    /// Used in <seealso cref="JSLibExport"/> to set Unity methods to JS functions.
    /// </summary>
    public class JSLibVariable
    {
        /// <summary>
        /// Method name.
        /// </summary>
        public string MethodName { get; set; } = string.Empty;

        /// <summary>
        /// Return type for signal-based methods.
        /// </summary>
        public ReturnType ReturnType { get; set; }

        /// <summary>
        /// Default value for the signal.
        /// </summary>
        public string DefaultValue { get; set; } = string.Empty;

        /// <summary>
        /// First parameter name (if has any).
        /// Only possible with ReturnType String or StringArray.
        /// </summary>
        public string ParameterName { get; set; } = string.Empty;

        /// <summary>
        /// Method documentation from XML docs or attribute.
        /// </summary>
        public string MethodDocumentation { get; set; } = string.Empty;

        /// <summary>
        /// Category for organizing methods in generated TypeScript.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Callback type for this method.
        /// </summary>
        public CallbackType CallbackType { get; set; } = CallbackType.None;

        /// <summary>
        /// Whether the callback Action has a string parameter (Action&lt;string&gt; vs Action).
        /// Only relevant when CallbackType is not None.
        /// </summary>
        public bool CallbackHasStringParam { get; set; }

        /// <summary>
        /// DTO type for JSON-serialized payloads. Only set when ReturnType is Json.
        /// </summary>
        public System.Type JsonType { get; set; }
    }

    /// <summary>
    /// TypeScript return type enum for JSLibVariable.
    /// </summary>
    public enum ReturnType
    {
        /// <summary>
        /// void.
        /// </summary>
        Void,

        /// <summary>
        /// string.
        /// </summary>
        String,

        /// <summary>
        /// string[].
        /// </summary>
        StringArray,

        /// <summary>
        /// JSON-serialized DTO, typed via JSLibVariable.JsonType.
        /// </summary>
        Json,
    }

    /// <summary>
    /// Callback type for method generation.
    /// </summary>
    public enum CallbackType
    {
        /// <summary>
        /// No callback — regular signal-based method.
        /// </summary>
        None,

        /// <summary>
        /// C# calls JS with a callback that JS invokes to respond.
        /// </summary>
        RequestResponse,

        /// <summary>
        /// C# registers a callback that Angular can invoke later.
        /// </summary>
        Registration,
    }
}
