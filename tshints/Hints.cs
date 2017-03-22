using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSHints
{

    /// <summary>
    /// Defines an abstract manifest
    /// </summary>
    public abstract class Manifest { }

    /// <summary>
    /// Generate JS Web Service API for this class
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class JsServiceAttribute : Attribute
    {
        /// <summary>
        /// URI of the service
        /// </summary>
        public string URI { get; set; }

        /// <summary>
        /// Initializes a new instance of the GenerateJSAttribute class.
        /// </summary>
        public JsServiceAttribute(string URI)
        {
            this.URI = URI;
        }
    }

    /// <summary>
    /// Generate JS Web Socket API for this class
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class JsSocketServiceAttribute : Attribute
    {
        private Type _callbackType;
        public Type CallbackType { get { return _callbackType; } }

        public JsSocketServiceAttribute(Type callbackType)
        {
            _callbackType = callbackType;
        }

    }

    /// <summary>
    /// Denotes that this constructor for the class should be mirrored in the client
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor)]
    public class FunctionConstructorAttribute : Attribute { }


    /// <summary>
    /// Denotes that a method will be called synchronously from the client
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class SynchronousAttribute : Attribute { }

    /// <summary>
    /// Generates an HTML template for the public members of this class
    /// </summary>
    public class GenerateTemplateAttribute : Attribute { }

    /// <summary>
    /// Generates a function proxy in javascript
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class JsMethodAttribute : Attribute { }

    /// <summary>
    /// Generates a event proxy in javascript
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class JsEventAttribute : JsMethodAttribute { }

    /// <summary>
    /// Generate a JS proxy of this class
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class JsClassAttribute : Attribute { }


    /// <summary>
    /// Generate a JS proxy of this class as a static enumeration class
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum)]
    public class JsEnumAttribute : Attribute { }


    /// <summary>
    /// Generate a JS proxy of this class which 
    /// has observable properties
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class JsViewModelAttribute : JsClassAttribute { }

    /// <summary>
    /// Makes this property observable for data binding    
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ObservableAttribute : Attribute { }

    /// <summary>
    /// Makes this property observable for data binding    
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DependentAttribute : Attribute
    {
        public bool DeferEvaluation { get; set; }

        public string[] Dependencies { get; private set; }

        public DependentAttribute(params string[] dependencies)
        {
            Dependencies = dependencies;
        }

        /// <summary>
        /// Initializes a new instance of the DependentAttribute class.
        /// </summary>
        public DependentAttribute(bool DeferEvaluation)
        {
            this.DeferEvaluation = DeferEvaluation;
        }
    }

    /// <summary>
    /// Creates this property as an observable collection
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ObservableCollectionAttribute : Attribute { }

    /// <summary>
    /// Creates a notify handler for this property
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class NotifyChangeAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Assembly)]
    public class JsIncludeAttribute : Attribute
    {
        public Type Type { get; set; }

        public JsIncludeAttribute(Type rootType)
        {
            this.Type = rootType;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class JsConstructAttribute : Attribute { }
}
