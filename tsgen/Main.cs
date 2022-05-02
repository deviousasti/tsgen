using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Serialization;
using JSHints;
using System.Threading.Tasks;
using System.ServiceModel;
using System.Xml.Linq;
using System.Xml;
using System.Diagnostics;

namespace tsgen
{
    internal class Program
    {
        #region Main

        private static void Main(string[] args)
        {
            if (args.Length == 0 || args.Contains("/?"))
            {
                Console.WriteLine(tsgen.Properties.Resources.Help);
                return;
            }

            Program p = new()
            {
                FilePath = Path.GetFullPath(args.Where(s => s.Contains(".dll") || s.Contains(".exe")).FirstOrDefault()),
                OutputPath = Path.GetFullPath(args.Where(s => s.Contains(".js") || s.Contains(".ts") || s.Contains(".txt")).FirstOrDefault()),
                GenerateComments = args.Contains("-comments"),
                Wait = args.Contains("-wait"),
                IncludeServiceLibrary = args.Contains("-lib"),
                IncludeDependencies = args.Contains("-dep"),
                Verbose = args.Contains("-verbose"),
            };

            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                string FileName = string.Format("{0}.dll", e.Name.Split(',')[0]);
                string fullPath;

                string directory = Path.GetDirectoryName(p.FilePath);

                fullPath = Path.Combine(directory, FileName);

                if (File.Exists(fullPath))
                    return Assembly.LoadFile(fullPath);

                fullPath = Path.GetFullPath(FileName);

                if (File.Exists(fullPath))
                    return Assembly.LoadFile(fullPath);

                return Assembly.LoadFile(Path.Combine(directory, "External", FileName));
            };

            p.Run();
        }

        #endregion Main

        #region Flags

        public Assembly ServiceAssembly { get; set; }

        public bool GenerateComments { get; set; }

        public string FilePath { get; set; }

        public string FileDependencyPath { get; set; }

        public string OutputPath { get; set; }

        public bool TypeSpecifier { get; set; }

        public bool Wait { get; set; }

        public bool IncludeServiceLibrary { get; set; }

        public bool IncludeDependencies { get; set; }

        public bool Verbose { get; set; } = true;

        #endregion Flags

        #region Run

        public void Run()
        {
            try
            {
                ServiceAssembly = Assembly.LoadFile(System.IO.Path.GetFullPath(FilePath));
            }
            catch (FileNotFoundException filex)
            {
                Console.WriteLine("File not found: {0}\n{1}", System.IO.Path.GetFullPath(FilePath), filex.Message);
                Console.ReadLine();
                return;
            }
            catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine(ex.ToString());
                Console.ReadLine();
            }

            RootNamespace = new JSGlobal() { Name = ServiceAssembly.GetName().Name };

            if (OutputPath == null) OutputPath = String.Format("{0}.js", System.IO.Path.GetFileNameWithoutExtension(FilePath));

            //Fold in starting assembly
            GenerateAssembly(ServiceAssembly);

            StringBuilder buffer = new();

            //Write header
            buffer.AppendFormat(tsgen.Properties.Resources.Header, ServiceAssembly.FullName);

            if (IncludeDependencies)
            {
                //deps are external
            }

            //core defs
            buffer.AppendLine(tsgen.Properties.Resources.BasicTypes);


            if (IncludeServiceLibrary)
                buffer.AppendLine(tsgen.Properties.Resources.Service);

            Console.WriteLine("Rendering output...");

            RenderTypes(buffer);

            ResolveTypes(buffer);

            string Output = buffer.ToString();

            Console.WriteLine("Writing file '{0}'", OutputPath);
            File.WriteAllText(OutputPath, Output);

            Console.WriteLine("Done.");

            if (Wait) Console.ReadLine();
        }

        #endregion Run

        #region Generate

        public void GenerateAssembly(Assembly assm)
        {
            try
            {
                foreach (var satelliteassm in assm.GetCustomAttributes(true).OfType<JsIncludeAttribute>())
                {
                    GenerateAssembly(satelliteassm.Type.Assembly);
                }

                List<JSClass> services = new();
                Type[] types = assm.GetTypes();
                foreach (var type in types)
                {
                    if (type.IsDefined(typeof(JsServiceAttribute), false) ||
                        type.IsSubclassOf(typeof(Microsoft.AspNetCore.Mvc.ControllerBase)) &&
                        !type.IsAbstract)
                        services.Add(GenerateWebServiceClass(type));

                    if (type.IsDefined(typeof(JsSocketServiceAttribute), false) ||
                        type.IsDefined(typeof(ServiceContractAttribute), false))
                        services.Add(GenerateSocketServiceClass(type));

                    if (type.IsDefined(typeof(JsViewModelAttribute), false))
                        GenerateType(type, ClassType.ViewModel);

                    if (type.IsDefined(typeof(JsClassAttribute), false) || type.IsDefined(typeof(DataContractAttribute), false))
                        GenerateType(type, ClassType.Class);

                    if (type.IsDefined(typeof(JsEnumAttribute), false))
                        GenerateType(type, ClassType.Enum);

                }

                var names = services.Select(c => c.FullName.Split('.')).ToArray();
                var commonNamespace =
                        Enumerable
                        .Range(0, names.Max(n => n.Length))
                        .Select(skip => names.Select(n =>
                            n.Length < skip ? null :
                            String.Join(".", n.SkipLast(skip))).Distinct())
                        .MinBy(ns => ns.Count())
                        .FirstOrDefault();

                if (commonNamespace != null)
                {
                    var endpoint = new JSWebEndpoint { FullName = commonNamespace, Name = "Endpoint" };
                    
                    endpoint.Properties.AddRange(services.Select(svc => new JSProperty
                    {
                        Name = svc.Name.Replace("Controller", "").ToLowerInvariant(),
                        PropertyType = JsPropertyType.Simple,
                        Type = JSType.GetType(svc)
                    }));
                    GenerateNamespace(commonNamespace).Classes.Add(endpoint);
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (Exception lex in ex.LoaderExceptions)
                {
                    Console.WriteLine(lex.ToString());
                }

            }
        }

        #region Namespacing

        private JSNamespace GenerateNamespace(string name)
        {
            if (NSDictionary.ContainsKey(name))
                return NSDictionary[name];

            if (Verbose)
                Console.WriteLine("Generating Namespace: {0}", name);

            string[] sparents = name.Split('.');
            JSNamespace parent;

            if (sparents.Length > 1)
            {
                string parentName = string.Join(".", sparents, 0, sparents.Length - 1);
                parent = GenerateNamespace(parentName);
            }
            else
            {
                parent = RootNamespace;
            }

            JSNamespace ns = new() { Name = sparents.Last(), FullName = name };

            parent.Namespaces.Add(ns);

            NSDictionary[name] = ns;

            return ns;
        }

        internal Dictionary<string, JSNamespace> NSDictionary = new();

        internal Dictionary<Type, JSClass> TypeDictionary = new();

        public JSNamespace RootNamespace
        {
            get => NSDictionary[String.Empty];
            set => NSDictionary[String.Empty] = value;
        }

        #endregion Namespacing

        #region Socket Service

        public JSSocketService GenerateSocketServiceClass(Type service)
        {
            JSSocketService jclass = GenerateType(service, ClassType.SocketService) as JSSocketService;

            if (jclass == null) return null;

            Type callback =
                service.GetCustomAttributes(false).OfType<JsSocketServiceAttribute>().FirstOrDefault()?.CallbackType ??
                service.GetCustomAttributes(false).OfType<ServiceContractAttribute>().FirstOrDefault()?.CallbackContract;

            GenerateMethods(callback, jclass);

            return jclass;
        }

        private JSFunction GenerateSocketMethod(Type service, MethodInfo method)
        {
            if (!method.IsDefined(typeof(JsMethodAttribute), false) && !method.IsDefined(typeof(OperationContractAttribute), false))
                return null;

            JSFunction jsm;

            if (service.IsDefined(typeof(JsSocketServiceAttribute), true) || service.IsDefined(typeof(ServiceContractAttribute), true))
                jsm = new JSSocketHostMethod();
            else
                jsm = new JSSocketClientMethod() { IsEvent = method.IsDefined(typeof(JsEventAttribute), true) };

            if (method.ReturnType != typeof(void))
            {
                if (method.ReturnType.IsGenericType &&
                    (method.ReturnType.GetGenericTypeDefinition() == typeof(IObservable<>) ||
                    method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))
                {
                    Type returnType = method.ReturnType.GetGenericArguments()[0];
                    GenerateType(returnType);

                    if (method.IsDefined(typeof(SynchronousAttribute), true))
                    {
                        jsm.ReturnType = JSType.GetType(returnType);
                    }
                    else
                        jsm.ReturnType = JSType.GetType(method.ReturnType);
                }
                else
                {
                    jsm.ReturnType = JSType.GetGenericType(JSType.Promise, JSType.GetType(method.ReturnType));
                }
            }

            GenerateParameters(method, jsm);

            return jsm;
        }

        private void GenerateMethods(Type service, JSClass jclass)
        {
            foreach (var method in service.GetMethods())
            {
                JSFunction jsm = null;

                if (method.IsPrivate || method.IsSpecialName)
                    continue;

                if (jclass is JSSocketService)
                    jsm = GenerateSocketMethod(service, method);
                else if (jclass is JSWebService)
                    jsm = GenerateWebMethod(service, method, jclass as JSWebService);
                else if (jclass.DefinitionOnly || method.IsDefined(typeof(JsMethodAttribute), false) || method.IsDefined(typeof(OperationContractAttribute), false))
                {
                    jsm = GenerateVirtualMethod(method);
                }

                if (jsm == null) continue;

                GenerateType(method.ReturnType);

                jsm.Name = ToCamelCase(method.Name);
                jsm.IsSignature = jclass.DefinitionOnly;

                try
                {
                    if (GenerateComments)
                    {
                        var docs = DocsByReflection.DocsService.GetXmlFromMember(method);
                        jsm.Description = GetElement(docs, "summary");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Documentation not found for: {0}", method.Name, ex.Message);
                }


                jclass.Methods.Add(jsm);
            }
        }

        static string GetElement(XmlElement docs, string tagName, Func<XmlElement, bool> filter = default)
        {
            filter = filter ?? (_ => true);
            return docs.GetElementsByTagName(tagName).OfType<XmlElement>().Where(filter).FirstOrDefault()?.InnerText;
        }

        private JSFunction GenerateVirtualMethod(MethodInfo method)
        {
            var sm = new JSFunction();
            GenerateParameters(method, sm);
            sm.ReturnType = JSType.GetType(method.ReturnType);
            return sm;
        }

        #endregion Socket Service

        #region Web Service

        public JSWebService GenerateWebServiceClass(Type service)
        {
            object[] attribs = service.GetCustomAttributes(false);

            var jswebservice = attribs.OfType<Microsoft.AspNetCore.Mvc.RouteAttribute>().FirstOrDefault();

            JSWebService jclass = GenerateType(service, ClassType.WebService) as JSWebService;
            jclass.Prefix = jswebservice?.Template ?? "";

            return jclass;
        }

        private JSFunction GenerateWebMethod(Type _service, MethodInfo method, JSWebService jclass)
        {
            object[] attribs = method.GetCustomAttributes(true);

            var httpMethod = attribs.OfType<Microsoft.AspNetCore.Mvc.Routing.IActionHttpMethodProvider>().SelectMany(c => c.HttpMethods).FirstOrDefault() ?? "GET";
            var routeTemplate = attribs.OfType<Microsoft.AspNetCore.Mvc.RouteAttribute>().FirstOrDefault()?.Template;

            if (routeTemplate == null)
                return null;

            GenerateType(method.ReturnType);

            JSWebMethod jsm = new()
            {
                Name = method.Name,
                Type = JSType.GetType(method.ReturnType),
                HTTPMethod = httpMethod,
                ParentService = jclass,
                ReturnType = JSType.GetType(method.ReturnType),
                RouteTemplate = routeTemplate,
            };

            GenerateParameters(method, jsm);

            JSOLN data = new JSOLN() { AppendNewLine = false };

            jsm.Parameters.AddRange(
                data.Properties.Select(param => new JSProperty()
                {
                    Name = param.Name,
                    Value = param.Name,
                    Type = param.Type,
                    AppendNewLine = false,
                    PropertyType = JsPropertyType.Assignment
                })
            );


            jsm.ObjectData = data;
            return jsm;
        }

        #endregion Web Service

        #region Types

        private JSClass GenerateType(Type type)
        {
            return GenerateType(type, ClassType.Default);
        }

        private JSClass GenerateType(Type type, ClassType classType)
        {
            if (type.FullName.Contains("Person"))
                Debugger.Break();

            if (type.FullName != null && type.FullName.StartsWith("Microsoft."))
                return JSClass.JSObject;

            if (type == null)
                return JSClass.JSObject;

            if (type.Name.Contains("&"))
                return JSClass.JSObject;

            if (type == typeof(Object))
                return JSClass.JSObject;

            if (type == typeof(string))
                return JSClass.JSObject;

            if (type.IsAssignableFrom(typeof(ICollection<>)))
                return JSClass.JSArray;

            if (type.IsEnum || type.IsDefined(typeof(JsEnumAttribute), false))
                classType = ClassType.Enum;

            if (type.IsDefined(typeof(JsViewModelAttribute), false))
                classType = ClassType.ViewModel;

            if (!type.IsEnum && (type.IsPrimitive || type.IsGenericType || (Path.GetDirectoryName(type.Assembly.Location) != Path.GetDirectoryName(ServiceAssembly.Location)) /* || type.IsValueType || !type.Assembly.Equals(ServiceAssembly)*/))
                return JSClass.JSObject;

            if (type.IsInterface)
                classType = ClassType.Interface;

            if (type.BaseType == typeof(Array))
            {
                GenerateType(type.GetElementType());
                return JSClass.JSArray;
            }

            if (type.IsValueType && !type.IsEnum)
                return JSClass.JSObject;

            if (Verbose)
                Console.WriteLine("Generating Type: {0}", type.FullName);

            if (TypeDictionary.ContainsKey(type))
            {
                return TypeDictionary[type];
            }

            JSNamespace ns = GenerateNamespace(type.Namespace);

            JSClass jclass;

            switch (classType)
            {
                case ClassType.OLN:
                    jclass = new JSOLN();
                    break;

                case ClassType.Enum:
                    jclass = new JSEnum();
                    break;

                case ClassType.WebService:
                    jclass = new JSWebService();
                    break;

                case ClassType.SocketService:
                    jclass = new JSSocketService();
                    break;

                case ClassType.ViewModel:
                    jclass = new JSViewModel();
                    break;

                case ClassType.Interface:
                    jclass = new JSClass() { DefinitionOnly = true };
                    break;

                default:
                    jclass = new JSClass();
                    break;
            }

            if (GenerateComments)
            {
                jclass.Description = DocsByReflection.DocsService.GetXmlFromType(type, false)?.InnerText;
            }

            jclass.Name = type.Name;
            jclass.FullName = type.FullName;
            jclass.IsBaseClass = type.GetCustomAttributes().Any(a => a.GetType().Name.Contains("BaseClass"));
            TypeDictionary.Add(type, jclass);

            if (!(jclass is JSEnum) && !(jclass is JSWebService))
                jclass.ParentClass = GenerateType(type.BaseType);

            //if (jclass is JSViewModel)
            //    jclass.ParentClass = JSClass.JSViewModelClass;

            GenerateProperties(type, jclass);
            GenerateMethods(type, jclass);

            foreach (var cons in type.GetConstructors())
            {
                if (cons.IsDefined(typeof(FunctionConstructorAttribute), false) || cons.GetCustomAttributes(true).Any(a => a.GetType().Name.StartsWith("FunctionConstructor")))
                {
                    JSConstructor constructor = new JSConstructor(jclass);
                    GenerateParameters(cons, constructor);
                    jclass.Constructors.Add(constructor);
                }
            }

            ns.Classes.Add(jclass);

            jclass.Interfaces = type.GetInterfaces().Select(GenerateType).Where(d => d.DefinitionOnly).ToList();


            return jclass;
        }

        private void GenerateProperties(Type type, JSClass jclass)
        {
            if (jclass is JSSocketService || jclass is JSWebService)
                return;

            foreach (var constant in type.GetFields(BindingFlags.Public | BindingFlags.Static)/*.Where(x =>  x.IsLiteral  && !x.IsInitOnly) */)
            {
                try
                {
                    if (!constant.IsLiteral)
                        continue;

                    object value = constant.IsLiteral ? constant.GetRawConstantValue() : constant.GetValue(null);


                    JSProperty prop = new JSProperty()
                    {
                        Name = constant.Name,
                        Type = constant.IsDefined(typeof(JsConstructAttribute), false) ?
                                                   JSType.Function
                                                   :
                                                   JSType.GetType(value.GetType()),
                        Value = value.ToString(),
                        PropertyType = JsPropertyType.Constant,
                        IsStatic = true
                    };

                    jclass.Properties.Add(prop);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error adding constant : {0}", ex.Message);
                }
            }

            foreach (var prop in type.GetProperties())
            {
                if (prop.IsDefined(typeof(XmlIgnoreAttribute), false) ||
                    prop.DeclaringType != type)
                    continue;

                JSClass propTypeClass = GenerateType(prop.PropertyType);

                object[] attribs = prop.GetCustomAttributes(false);

                string defValue = null;

                if (propTypeClass.IsPrimitive)
                    defValue = null;

                //defValue = String.Format("new {0}()", propTypeClass.FullName);

                var defaultValueAttrib = attribs.OfType<DefaultValueAttribute>().FirstOrDefault();

                if (defaultValueAttrib != null)
                {
                    if (defaultValueAttrib.Value is bool)
                    {
                        defValue = ((bool)defaultValueAttrib.Value) ? "true" : "false";
                    }
                    else if (!prop.PropertyType.IsEnum)
                        defValue = defaultValueAttrib.Value == null ? "null" : defaultValueAttrib.Value.ToString();
                    else
                        defValue = String.Format("{0}.{1}", prop.PropertyType.FullName, defaultValueAttrib.Value);
                }

                JSProperty newProperty = new JSProperty()
                {
                    Name = prop.Name,
                    Type = JSType.GetType(prop.PropertyType),
                    DefaultValue = defValue,
                };

                foreach (var dependency in newProperty.Type.Dependencies)
                {
                    GenerateType(dependency);
                }

                if (!prop.CanRead)
                    newProperty.PropertyType = JsPropertyType.WriteOnly;

                if (!prop.CanWrite)
                    newProperty.PropertyType = JsPropertyType.ReadOnly;

                if (prop.IsDefined(typeof(NotifyChangeAttribute), false))
                    newProperty.IsWatched = true;

                if (jclass.DefinitionOnly)
                {
                    newProperty.PropertyType = JsPropertyType.Simple;
                    newProperty.HasDefaultValue = false;
                }

                jclass.Properties.Add(newProperty);
            }

            if (!jclass.DefinitionOnly && TypeSpecifier)
            {
                jclass.Properties.Insert(0, new JSProperty() { Name = "__type", Value = string.Format("{0}", type.FullName), Type = JSType.String });
            }
        }

        static string ToCamelCase(string name)
        {
            return name[..1].ToLower() + name[1..];
        }

        private void GenerateParameters(MethodBase method, JSFunction jsm)
        {
            foreach (var param in method.GetParameters())
            {
                GenerateType(param.ParameterType);
                var paramJsType = JSType.GetType(param.ParameterType);
                var isNullable = paramJsType.IsGenericTypeOf(JSType.Nullable);
                if (isNullable)
                    paramJsType = (paramJsType as tsgenericType).TypeParameters.First();

                string description = null;
                if (GenerateComments)
                {
                    var docs = DocsByReflection.DocsService.GetXmlFromParameter(param, false);
                    description = docs?.InnerText;
                }

                jsm.Parameters.Add(new JSProperty()
                {
                    Name = param.Name,
                    PropertyType = JsPropertyType.Inline,
                    AppendNewLine = false,
                    Type = paramJsType,
                    IsByRef = param.IsOut || param.ParameterType.IsByRef,
                    IsOptional = param.IsOptional || isNullable,
                    DefaultValue = param.HasDefaultValue && param.DefaultValue != null ? param.DefaultValue.ToString() : TSConstructs.Null,
                    HasDefaultValue = param.HasDefaultValue,
                    Description = description
                });
            }
        }

        #endregion Types

        #region Render

        private void RenderTypes(StringBuilder buffer)
        {
            foreach (var item in NSDictionary.Values)
            {
                item.Render(buffer);
                buffer.AppendLine();
            }
        }

        private void ResolveTypes(StringBuilder buffer)
        {
            //buffer.AppendLine("(function () { ");

            //foreach (var type in TypeDictionary.Values)
            //{
            //    if (type is JSOLN)
            //        continue;

            //    JSNSResolver resolver = new JSNSResolver() { Class = type };
            //    resolver.Render(buffer);
            //}

            //buffer.AppendLine("})();");
            //buffer.AppendLine();
        }

        #endregion Render

        #endregion Generate
    }
}