using System;

namespace UnityAngularBridge
{
    /// <summary>
    /// This attribute will expose marked Method for Angular as a type of SwaggerClient.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class AngularExposedAttribute : Attribute
    {
        private const string DefaultGameObjectName = "AngularInformer";

        /// <summary>
        /// GameObject name exposed method is part of.
        /// </summary>
        public string GameObjectName { get; } = DefaultGameObjectName;

        /// <summary>
        /// Override documentation for the generated TypeScript method. If empty, XML doc comments are used.
        /// </summary>
        public string Documentation { get; set; } = string.Empty;

        /// <summary>
        /// Serializable DTO type for JSON transport. The generated TypeScript wrapper accepts this
        /// shape and JSON.stringifies it before SendMessage; the C# method must take a single string
        /// parameter and deserialize it with JsonUtility.FromJson&lt;T&gt;.
        /// </summary>
        public Type JsonType { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public AngularExposedAttribute()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="gameObjectName">GameObject name exposed method is part of.</param>
        public AngularExposedAttribute(string gameObjectName = DefaultGameObjectName)
        {
            GameObjectName = gameObjectName;
        }
    }
}
