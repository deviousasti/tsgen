using Microsoft.FSharp.Control;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tsgen
{
    public static class Extensions
    {

        public static void AppendFormatLine(this StringBuilder buffer, string format, params object[] args)
        {
            buffer.AppendFormat(format, args);
            buffer.AppendLine();
        }
    }

    #region Property

    public class JSProperty : JSVar
    {
        public string DefaultValue { get; set; }

        public string Value { get; set; }

        public JsPropertyType PropertyType { get; set; }

        public string[] Dependencies { get; set; }

        public bool IsWatched { get; set; }

        public bool HasDefaultValue { get; set; }

        public override string ToString()
        {
            return base.ToString();
        }

        public bool IsPrototyped
        {
            get
            {
                return Type.IsValueType &&
                    PropertyType == JsPropertyType.Attached;
            }
        }

        public string QualifiedValue
        {
            get
            {
                if (Type.SourceType == typeof(string) && DefaultValue != "null")
                    return string.Format("\"{0}\"", Value == null ? string.Empty : Value.Replace("\"", "\\\""));

                if (Value != "null" && !string.IsNullOrEmpty(Value))
                    return Value;

                if (DefaultValue == null)
                    return Type.DefaultValue;
                else
                    return DefaultValue;
            }
        }

        public JSProperty()
        {
            AppendNewLine = true;
            Dependencies = new string[] { };
            Type = JSType.Any;
            HasDefaultValue = true;
        }

        protected override void OnRender(StringBuilder buffer)
        {

            switch (PropertyType)
            {
                default:
                    buffer.AppendFormatLine("{0}: {1}{2};", Name, Type, HasDefaultValue ? string.Format(" = {0}", QualifiedValue) : "");
                    break;

                case JsPropertyType.Assignment:
                    buffer.AppendFormat("{0}: {1}", Name, Value);
                    break;

                case JsPropertyType.Inline:
                    buffer.AppendFormat("{0}{1}: {2}{3}", Name, IsOptional ? "?" : "", Type, HasDefaultValue && !IsOptional ? string.Format(" = {0}", QualifiedValue) : "");
                    break;

                case JsPropertyType.Static:
                case JsPropertyType.Constant:
                    buffer.AppendFormatLine("static {0}: {1} = {2};", Name, Type, QualifiedValue);
                    break;

                case JsPropertyType.ReadOnly:
                    buffer.AppendFormatLine("{2} get {0} (): {1}{{ throw new Error(\"Not implemented: {0}\"); }}", Name, Type, IsStatic ? "static" : "");
                    break;

            }

            if (IsWatched)
            {
                buffer.AppendFormat(@"{0}.prototype.On{1}Change = {2};",
                    Name, TSConstructs.BlankFunction);
                buffer.AppendLine();
            }


        }
    }

    #endregion Property

    #region JSType

    public class JSType
    {
        private const string Null = "null";

        public readonly static JSType String = new JSType(typeof(string)) { DefaultValue = "''", TypeName = TSConstructs.String };

        public readonly static JSType Number = new JSType(typeof(int)) { DefaultValue = "0", TypeName = TSConstructs.Number };

        public readonly static JSType Decimal = new JSType(typeof(double)) { DefaultValue = "0.0", TypeName = TSConstructs.Number };

        public readonly static JSType Boolean = new JSType(typeof(bool)) { DefaultValue = "false", TypeName = TSConstructs.Boolean };

        public readonly static JSType Date = new JSType(typeof(DateTime)) { DefaultValue = Null, TypeName = TSConstructs.DateTime };

        public readonly static JSType Object = new JSType(typeof(Object)) { DefaultValue = Null, TypeName = TSConstructs.Any };

        public readonly static JSType Type = new JSType(typeof(Type));

        public readonly static JSType Function = new JSType(typeof(Action)) { TypeName = "function", DefaultValue = TSConstructs.BlankFunction };

        public readonly static JSType Any = new JSType(typeof(Object)) { TypeName = "any", DefaultValue = null };

        public readonly static JSType Void = new JSType(typeof(void)) { TypeName = "void" };

        public readonly static JSType Constructor = new JSType(typeof(void)) { TypeName = "constructor" };

        public readonly static JSType Array = new JSType(typeof(Array)) { TypeName = "Array", DefaultValue = TSConstructs.Array };

        public readonly static JSType ByteArray = new JSType(typeof(byte[])) { TypeName = "ArrayBuffer", DefaultValue = TSConstructs.Null };

        public readonly static JSType ByRef = new JSType(typeof(Array)) { TypeName = "ByRef", DefaultValue = Null };

        public readonly static JSType Enumerable = new JSType(typeof(IEnumerable<>)) { TypeName = "IEnumerable" };

        public readonly static JSType Dictionary = new JSType(typeof(Dictionary<,>)) { TypeName = "Map" };

        public readonly static JSType HashSet = new JSType(typeof(HashSet<>)) { TypeName = "Set" };

        public readonly static JSType Queue = new JSType(typeof(Queue<>)) { TypeName = "collections.Queue" };

        public readonly static JSType Promise = new JSType(typeof(Task<>)) { TypeName = "Promise" };

        public readonly static JSType ApiResult = new JSType(typeof(Microsoft.AspNetCore.Mvc.ActionResult)) { TypeName = "ApiResult" };

        public readonly static JSType Observable = new JSType(typeof(IObservable<>)) { TypeName = "Rx.Observable" };

        public readonly static JSType DataTable = new JSType(typeof(DataTable)) { TypeName = "DataTable" };

        public readonly static JSType Nullable = new JSType(typeof(Nullable)) { TypeName = "Nullable" };

        readonly static Dictionary<Type, JSType> TypeMap = new Dictionary<Type, JSType>();

        protected JSType()
        {
            SourceType = typeof(string);
            DefaultValue = Null;
        }

        protected JSType(Type sourceType)
            : this()
        {
            SourceType = sourceType;
            TypeName = string.Format("{0}.{1}", sourceType.Namespace ?? "global", sourceType.Name);
        }

        public virtual IEnumerable<Type> Dependencies => new[] { SourceType };

        public virtual bool IsGenericType => false;

        public bool IsGlobalQualified { get; set; }

        public Type SourceType { get; set; }

        public virtual string TypeName { get; private set; }

        private string _defaultValue;
        public virtual string DefaultValue
        {
            get
            {
                if (_defaultValue == null)
                {
                    if (SourceType != typeof(void) && SourceType.IsPrimitive)
                        return Activator.CreateInstance(SourceType).ToString();
                    else
                        return "null";
                }
                else
                    return _defaultValue;
            }
            set
            {
                _defaultValue = value;
            }
        }

        public bool IsArray
        {
            get
            {
                return SourceType == typeof(Array) || SourceType.BaseType == typeof(Array);
            }
        }

        public bool IsValueType
        {
            get
            {
                return SourceType.IsValueType || SourceType == typeof(string);
            }
        }

        public override string ToString()
        {
            return (IsGlobalQualified ? "globalThis." : "") + TypeName;
        }

        public static JSType GetType(JSClass jsClass)
        {
            return new JSType { TypeName = jsClass.FullName, SourceType = typeof(object) };
        }

        public static JSType GetType(Type type)
        {
            JSType jstype;
            if (TypeMap.TryGetValue(type, out jstype))
                return jstype;

            jstype = GetTypeImpl(type);
            TypeMap[type] = jstype;

            return jstype;
        }

        static JSType GetTypeImpl(Type type)
        {
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
                return new TsGenericType(type);

            if (type.IsByRef)
                return new TsGenericType(type)
                {
                    TypeDefinition = JSType.ByRef,
                    TypeParameters = new JSType[] { GetType(type.GetElementType()) }
                };

            if (type == typeof(string) || type == typeof(char) || type == typeof(Guid))
                return JSType.String;

            if (type == typeof(Action))
                return JSType.Function;

            if (type == typeof(Microsoft.AspNetCore.Mvc.ActionResult))
                return JSType.ApiResult;

            if (type == typeof(int) || type == typeof(long) || type == typeof(byte) || type == typeof(uint) || type == typeof(short) || type == typeof(ushort))
                return JSType.Number;

            if (type == typeof(Single) || type == typeof(double))
                return JSType.Decimal;

            if (type == typeof(bool))
                return JSType.Boolean;

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
                return JSType.Date;

            if (type == typeof(object))
                return JSType.Object;

            if (type == typeof(Uri))
                return JSType.String;

            if (type == typeof(byte[]))
                return JSType.ByteArray;

            if (type == typeof(Array))
                return JSType.Array;

            if (type == typeof(Microsoft.FSharp.Collections.FSharpList<>))
                return JSType.Array;

            if (type.BaseType == typeof(Array))
                return new TsGenericType(type)
                {
                    TypeDefinition = Array,
                    TypeParameters = new JSType[] { GetType(type.GetElementType()) },
                    DefaultValue = TSConstructs.Array
                };

            if (type == typeof(IEnumerable<>))
                return JSType.Enumerable;

            if (type == typeof(List<>))
                return JSType.Array;

            if (type == typeof(Dictionary<,>))
                return JSType.Dictionary;

            if (type == typeof(Queue<>))
                return JSType.Queue;

            if (type == typeof(HashSet<>))
                return JSType.HashSet;

            if (type == typeof(Action))
                return JSType.Function;

            if (type == typeof(Task<>))
                return JSType.Promise;

            if (type == typeof(FSharpAsync<>))
                return JSType.Promise;

            if (type == typeof(Microsoft.FSharp.Collections.FSharpMap<,>))
                return JSType.Dictionary;

            if (type == typeof(Microsoft.FSharp.Core.Unit))
                return JSType.Void;

            if (type == typeof(IObservable<>))
                return JSType.Observable;

            if (type == typeof(DataTable))
                return JSType.DataTable;

            if (type == typeof(void))
                return Void;

            if (type == typeof(Microsoft.FSharp.Core.FSharpOption<>) || type == typeof(System.Nullable<>))
                return JSType.Nullable;

            if (type.IsValueType && !type.IsEnum)
                return JSType.Any;

            JSType newType = new JSType(type);

            if (type.IsGenericTypeDefinition) 
                newType.TypeName = type.FullName.Substring(0, type.FullName.LastIndexOf("`"));

            if (type.IsEnum)
                newType.DefaultValue = string.Format("{0}.{1}", type.FullName, Enum.GetNames(type).First());

            return newType;
        }

        public static JSType GetGenericType(JSType genericType, params JSType[] typeParams)
        {
            return new TsGenericType() { SourceType = genericType.SourceType, TypeDefinition = genericType, TypeParameters = typeParams };
        }

        public virtual bool Is(JSType type)
        {
            return this.TypeName == type.TypeName;
        }

        public bool IsGenericTypeOf(JSType type)
        {
            return (this as TsGenericType)?.TypeDefinition == type;
        }

    }


    public class TsGenericType : JSType
    {

        public JSType TypeDefinition { get; set; }

        public IEnumerable<JSType> TypeParameters { get; set; }

        public TsGenericType()
            : base()
        {

        }

        public TsGenericType(Type sourceType)
            : base(sourceType)
        {
            if (sourceType.IsGenericType)
            {
                TypeDefinition = GetType(sourceType.GetGenericTypeDefinition());
                TypeParameters = sourceType.GetGenericArguments().Select(GetType).ToArray();
                DefaultValue = TypeDefinition.DefaultValue;
            }
        }

        public override IEnumerable<Type> Dependencies
        {
            get { return this.TypeParameters.SelectMany(t => t.Dependencies); }
        }

        public override bool IsGenericType { get { return true; } }

        public override string TypeName
        {
            get
            {
                return string.Format("{0}<{1}>", TypeDefinition, string.Join(",", TypeParameters)); ;
            }
        }

        public override bool Is(JSType type)
        {
            if (!(type is TsGenericType))
                return this.TypeDefinition.TypeName == type.TypeName;
            else
                return base.Is(type);
        }
    }

    public class TsUnionType : JSType
    {
        public IEnumerable<JSType> Cases { get; }

        public TsUnionType(IEnumerable<JSType> types)
        {
            this.Cases = types;
            this.SourceType = typeof(object);
        }

        public override string TypeName => System.String.Join(" | ", Cases);

    }

    #endregion JSType

    #region Parameter

    public class JSVar : JSRenderable
    {
        public static readonly JSVar SuccessCallback = new JSVar() { Name = "success", Type = JSType.Function };

        public static readonly JSVar FailureCallback = new JSVar() { Name = "failure", Type = JSType.Function };

        public JSType Type { get; set; }

        public bool IsStatic { get; set; }

        public bool IsByRef { get; set; }

        public JSVar()
        {
            AppendNewLine = false;
            Type = JSType.String;
        }

        public JSVar(string Name)
            : this()
        {
            this.Name = Name;
        }

        public JSVar(string Name, JSType Type)
            : this(Name)
        {
            this.Type = Type;
        }

        protected override void OnRender(StringBuilder buffer)
        {
            buffer.AppendFormat("{0}: {1}", Name, Type.TypeName);
        }

        public bool IsOptional { get; set; }
    }

    #endregion Parameter

    #region Function

    public class JSFunction : JSVar
    {
        protected new JSType Type { get => base.Type; set => base.Type = value; }

        public JSType ReturnType { get; set; }

        public virtual bool IsVirtual { get { return true; } }

        public bool IsSignature { get; set; }

        public JSFunction()
        {
            Type = JSType.Function;
            ReturnType = JSType.Void;
            AppendNewLine = true;
            Parameters = new List<JSVar>();
        }

        public List<JSVar> Parameters { get; set; }

        public string Body { get; set; }

        protected override void OnRender(StringBuilder buffer)
        {
            buffer.AppendLine(TSConstructs.DocCommentStart);
            WriteDescription(buffer);
            buffer.AppendLine(TSConstructs.DocCommentEnd);

            buffer.AppendFormat("{0} (", Name, ReturnType);

            WriteParameters(buffer, IncludeThis: false);

            buffer.Append(')');

            if (Type != JSType.Constructor)
                buffer.AppendFormat(": {0} ", ReturnType);

            if (!IsSignature)
            {
                buffer.Append('{');

                OnRenderBody(buffer);

                buffer.AppendLine("}");
            }
        }

        protected virtual void OnRenderBody(StringBuilder buffer)
        {
            if (!string.IsNullOrEmpty(Body))
                buffer.Append(Body);

            else if (IsVirtual)
            {
                buffer.AppendFormat("throw new Error(\"Method {0} not implemented\");", Name);
            }

            buffer.AppendLine();
        }

        protected void WriteParameters(StringBuilder buffer, bool IncludeThis)
        {
            IEnumerable<JSVar> paramrs;
            paramrs = IncludeThis ? new JSVar[] { new JSVar("this", Type) }.Concat(Parameters) : Parameters;

            BufferListAsCSV(buffer, paramrs.Where(p => !p.IsStatic).Cast<JSRenderable>(), string.Empty);
        }

        protected override void WriteDescription(StringBuilder buffer)
        {
            base.WriteDescription(buffer);

            foreach (var param in Parameters)
            {
                buffer.AppendFormatLine("* @param {0} {{{1}}} {2}", param.Name, param.Type.TypeName, param.Description);
            }

            if (IsVirtual)
                buffer.AppendLine("* @virtual");

            foreach (var param in Parameters)
                if (param.IsByRef)
                {
                    buffer.AppendFormatLine("* @byref {0}", param.Name);
                }

            if (ReturnType != null)
            {
                buffer.AppendFormatLine("* @returns {{{0}}}", ReturnType);
            }
        }
    }

    #endregion Function

    #region Constructor

    public class JSConstructor : JSFunction
    {
        public static readonly JSConstructor Empty = new JSConstructor();

        public JSClass Class { get; set; }

        public List<JSProperty> ClassProperties { get { return Class.Properties; } }

        public override bool IsVirtual { get { return false; } }

        public JSConstructor(JSClass @class = null)
        {
            Name = "constructor";
            Type = JSType.Constructor;
            Class = @class ?? JSClass.JSObject;
        }

        protected override void OnRenderBody(StringBuilder buffer)
        {

            if (!Class.ParentClass.IsPrimitive)
            {
                buffer.Append("super(");
                var constructor = Class.ParentClass.Constructors.FirstOrDefault(c => c.Parameters.Select(p => p.Name).Intersect(Parameters.Select(p => p.Name)).Count() == Parameters.Count);
                if (constructor != null)
                    buffer.Append(String.Join(", ", constructor.Parameters.Select(p => p.Name)));

                buffer.AppendLine(");");
            }

            if ((Parameters.Count > 0))
                buffer.AppendFormat("//set properties in {0}", Class.Name);

            buffer.AppendLine();

            foreach (var prop in ClassProperties)
            {
                if (!prop.IsPrototyped)
                {
                    if (prop.IsWatched)
                    {
                        buffer.AppendFormat("this.${0}.subscribe(function(value) {{ this.On{0}Change(value); }}, this);", prop.Name);
                        buffer.AppendLine();
                    }
                }
            }

            foreach (var param in Parameters)
            {
                if (param.IsStatic) continue;

                var matching = this.Class.Properties.Select(p => p.Name).FirstOrDefault(p => string.Equals(p, param.Name, StringComparison.OrdinalIgnoreCase));
                if (matching != null)
                    buffer.AppendFormat("if ({1} !== null) this.{0} = {1};",
                        matching,
                        param.Name
                    );

                buffer.AppendLine();
            }

            base.OnRenderBody(buffer);
        }

        protected override void OnResolve(StringBuilder buffer)
        {

        }
    }

    #endregion Constructor

    #region WebMethod

    public class JSWebMethod : JSFunction
    {
        public string HTTPMethod { get; set; }

        public JSWebService ParentService { get; set; }

        public JSOLN ObjectData { get; set; }

        public override bool IsVirtual => false;

        public string RouteTemplate { get; set; }

        public string Policy { get; set; } = "";

        public JSVar RequestParam { get; set; } = new JSVar("null");

        protected override void OnRenderBody(StringBuilder buffer)
        {
            base.OnRenderBody(buffer);

            buffer.Append("\treturn this.serviceRequest(");

            buffer.AppendFormat("'{0}', ", HTTPMethod);

            buffer.Append(String.Format("`{0}/{1}`, ", ParentService.Prefix, RouteTemplate).Replace("{", "${").Replace("?", ""));

            buffer.AppendFormat("'{0}', ", Policy);

            buffer.AppendFormat("{0}, ", RequestParam.Name);

            ObjectData?.Render(buffer);

            buffer.AppendLine(");");
        }
    }

    #endregion WebMethod

    #region SocketMethod

    public class JSSocketClientMethod : JSFunction
    {
        public JSSocketService ParentService { get; set; }

        public bool IsEvent { get; set; }

        public override bool IsVirtual { get { return true; } }

        protected override void OnRender(StringBuilder buffer)
        {
            if (IsEvent)
            {
                buffer.AppendFormatLine("{0} = new Services.Event();", Name);
            }
            else
            {
                base.OnRender(buffer);
            }
        }

    }

    public class JSSocketHostMethod : JSFunction
    {
        public override bool IsVirtual { get { return false; } }

        protected override void OnRenderBody(StringBuilder buffer)
        {
            if (ReturnType.Is(JSType.Observable))
                buffer.AppendLine(string.Format("return <{1}> this.service.rpcCallStream('{0}', arguments);", Name, ReturnType));
            else if (ReturnType.Is(JSType.Void))
                buffer.AppendLine(string.Format("this.service.rpcCall('{0}', arguments);", Name));
            else
                buffer.AppendLine(string.Format("return <{1}> this.service.rpcCallReturn('{0}', arguments);", Name, ReturnType));
        }
    }

    #endregion SocketMethod

    #region Classes

    public class JSInterface : JSClass
    {
        protected override void OnRender(StringBuilder buffer)
        {
            buffer.AppendFormat("export interface {0} ", Name);

            if (ParentClass != JSClass.JSObject)
                buffer.AppendFormat("extends {0}", ParentClass.FullName);

            buffer.AppendLine("{");

            buffer.AppendLine("}");
        }
    }

    public class JSClass : JSRenderable
    {
        public static readonly JSClass JS = new JSClass() { Name = "JsonString", FullName = "Object", IsPrimitive = true };

        public static readonly JSClass JSObject = new JSClass() { Name = "Object", FullName = "Object", IsPrimitive = true };

        public static readonly JSClass JSArray = new JSClass() { Name = "Array", FullName = "Array", IsPrimitive = true };

        public static readonly JSClass JSFunction = new JSClass() { Name = "Function", FullName = "Function", IsPrimitive = true };

        public List<JSConstructor> Constructors { get; set; } = new();

        public List<JSProperty> Properties { get; set; } = new();

        public List<JSFunction> Methods { get; set; } = new();

        public List<JSClass> Interfaces { get; set; } = new();

        public JSClass ParentClass { get; set; } = JSObject;

        public string FullName { get; set; }

        public bool IsPrimitive { get; set; }

        public bool DefinitionOnly { get; set; }

        public bool IsBaseClass { get; set; }

        protected override void OnRender(StringBuilder buffer)
        {
            OnRenderHeader(buffer);

            buffer.AppendLine("{");

            Constructors.ForEach(c => c.Render(buffer));
            buffer.AppendLine();

            OnRenderProperties(buffer);
            buffer.AppendLine();

            OnRenderMethods(buffer);

            if (!DefinitionOnly)
            {

                buffer.AppendLine();
                buffer.AppendLine("//types");
                buffer.AppendFormat("static TypeDescriptor: Type = {{ Name: '{1}', FullName : '{0}', Parent: {2} }};", FullName, Name, ParentClass.FullName);

                buffer.AppendLine();
            }
            buffer.AppendLine();
            buffer.AppendLine("}");

        }

        protected virtual void OnRenderHeader(StringBuilder buffer)
        {
            buffer.AppendFormat("export {0} {1} ", DefinitionOnly ? TSConstructs.Interface : TSConstructs.Class, GetExtendedName());

            if (ParentClass != JSClass.JSObject)
                buffer.AppendFormat("extends {0} ", ParentClass.FullName);

            if (Interfaces.Count > 0)
            {
                buffer.AppendFormat(" {0} {1} ", DefinitionOnly ? "extends" : "implements", string.Join(", ", Interfaces.Select(i => i.FullName)));
            }
        }

        public string GetExtendedName()
        {
            if (!this.IsBaseClass)
                if (this.DefinitionOnly || (this.Methods.All(m => !m.IsVirtual) && this.Properties.All(p => p.PropertyType != JsPropertyType.ReadOnly && p.PropertyType != JsPropertyType.WriteOnly)))
                    return Name;

            return string.Format(TSConstructs.BaseClassFormat, Name);
        }

        public override IEnumerable<JSRenderable> Children()
        {
            return Constructors
                             .Concat<JSRenderable>(Properties.ToArray())
                             .Concat<JSRenderable>(Methods.ToArray());
        }

        protected override void OnResolve(StringBuilder buffer)
        {
            buffer.AppendLine();
        }

        protected virtual void OnRenderProperties(StringBuilder buffer)
        {
            foreach (var prop in Properties)
            {
                prop.Render(buffer);
            }
        }

        protected virtual void OnRenderMethods(StringBuilder buffer)
        {
            foreach (var meth in Methods)
            {
                meth.Render(buffer);
                buffer.AppendLine();
            }
        }
    }

    public abstract class JSProxyService : JSClass
    {
        public static readonly JSVar ProxyParameter = new JSVar("proxy", JSType.Any);
        public static readonly JSClass JSProxyBaseClass = new JSClass() { Name = "Class", FullName = "Services.ProxyBase" };
        
        public static JSClass ProxyServiceBaseClass(string name)
        {
            var jsclass = new JSClass() { Name = "Class", FullName = name, ParentClass = JSProxyBaseClass };
            jsclass.Constructors.Add(new JSConstructor(jsclass){ 
                Parameters = { ProxyParameter } 
                }
            );
            return jsclass;
        }

        protected JSProxyService(JSClass parentClass)
        {
            ParentClass = parentClass;
            Constructors = new() {
                new JSConstructor(this) {
                    Parameters = { ProxyParameter } }
            };
        }
    }

    public class JSSocketService : JSProxyService
    {
        public static readonly JSClass JSSocketServiceClass = ProxyServiceBaseClass("Services.SocketProxyBase");

        public JSSocketService() : base(JSSocketServiceClass)
        {

        }
    }

    public class JSWebService : JSProxyService
    {
        public static readonly JSClass JSWebServiceClass = ProxyServiceBaseClass("Services.WebServiceBase");

        public string Prefix { get; set; }

        public JSWebService() : base(JSWebServiceClass)
        {
        }

        protected override void OnRenderProperties(StringBuilder buffer)
        {
            new JSProperty { Name = "prefix", Value = Prefix, Type = JSType.String }.Render(buffer);
            base.OnRenderProperties(buffer);
        }
    }

    public class JSWebEndpoint : JSClass
    {
        public static readonly JSClass JSWebEndpointClass = new JSClass() { Name = "Class", FullName = "Services.EndpointBase" };

        public const string ClassName = "Endpoint";

        public string Prefix { get; set; }

        class JSWebEndpointConstructor : JSConstructor
        {
            public JSWebEndpointConstructor(JSWebEndpoint ep) : base(ep)
            {
                this.Parameters = new() { JSProxyService.ProxyParameter };
            }

            protected override void OnRenderBody(StringBuilder buffer)
            {
                base.OnRenderBody(buffer);
                foreach (var prop in Class.Properties)
                {
                    buffer.AppendFormatLine("this.{0} = new {1}({2});", prop.Name, prop.Type, JSProxyService.ProxyParameter.Name);
                }
            }
        }

        public JSWebEndpoint()
        {
            ParentClass = JSWebEndpointClass;
            Constructors = new() { new JSWebEndpointConstructor(this) };
        }
    }

    public class JSOLN : JSVar
    {
        public List<JSProperty> Properties { get; set; }

        public override IEnumerable<JSRenderable> Children()
        {
            return Properties;
        }

        protected override void OnRender(StringBuilder buffer)
        {
            buffer.Append('{');

            if (AppendNewLine)
                buffer.AppendLine();

            BufferListAsOLN(buffer, Children().ToArray());

            buffer.Append('}');
        }
    }

    public class JSViewModel : JSClass
    {


    }


    #endregion Classes

    #region Enum

    public class JSEnum : JSClass
    {
        public JSEnum()
        {
            IsPrimitive = true;
        }

        protected override void OnRender(StringBuilder buffer)
        {
            buffer.AppendFormat("export enum {0} ", Name);

            buffer.AppendLine("{");

            foreach (var prop in Properties)
            {
                buffer.AppendFormat("{0} = {1},", prop.Name, prop.Value);
                buffer.AppendLine();
            }

            buffer.AppendLine("}");
        }
    }

    #endregion Enum

    #region JSNamespace

    public class JSNamespace : JSRenderable
    {
        public List<JSNamespace> Namespaces { get; set; }

        public List<JSClass> Classes { get; set; }

        public string FullName { get; set; }

        protected readonly JSProperty Specifier = new JSProperty() { Name = "__namespace", Value = "true", Type = JSType.Boolean };

        public JSNamespace()
        {   
            Classes = new List<JSClass>();
            Namespaces = new List<JSNamespace>();
        }

        protected override void OnRender(StringBuilder buffer)
        {
            buffer.AppendFormat("namespace {0} {{", FullName);
            buffer.AppendLine();

            BufferList(buffer, Children());

            buffer.AppendLine("}");
        }

        public override IEnumerable<JSRenderable> Children()
        {
            return Classes;
        }
    }

    #endregion JSNamespace

    #region Global

    public class JSGlobal : JSNamespace
    {
        protected override void OnRender(StringBuilder buffer)
        {
            return;
        }
    }

    #endregion Global

    #region Resolver

    public class JSNSResolver : JSRenderable
    {
        public JSClass Class { get; set; }

        protected override void OnRender(StringBuilder buffer)
        {
            Class.Resolve(buffer);
        }
    }

    #endregion Resolver
}