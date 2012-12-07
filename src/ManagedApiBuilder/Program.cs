using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ApiParser;
using Newtonsoft.Json;

namespace ManagedApiBuilder
{
    class OrderedDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        readonly Dictionary<TKey, TValue> iDictionary = new Dictionary<TKey, TValue>();
        readonly List<TKey> iOrder = new List<TKey>();

        public OrderedDictionary()
        {
        }
        public OrderedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> aSource)
        {
            foreach (var kvp in aSource)
            {
                Add(kvp.Key, kvp.Value);
            }
        }
        public void Add(TKey aKey, TValue aValue)
        {
            iDictionary.Add(aKey, aValue);
            iOrder.Add(aKey);
        }
        public TValue this[TKey aKey]
        {
            get
            {
                return iDictionary[aKey];
            }
            set
            {
                if (!iDictionary.ContainsKey(aKey))
                {
                    iOrder.Add(aKey);
                }
                iDictionary[aKey] = value;
            }
        }
        public IEnumerable<TKey> Keys { get { return iOrder; } }
        public IEnumerable<TValue> Values { get { return this.Select(x => x.Value); } }
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var key in iOrder)
            {
                yield return new KeyValuePair<TKey, TValue>(key, iDictionary[key]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    class CategorizedDeclarations
    {
        public OrderedDictionary<string, FunctionCType> FunctionTable { get; private set; }
        public OrderedDictionary<string, StructCType> StructTable { get; private set; }
        public OrderedDictionary<string, EnumCType> EnumTable { get; private set; }
        public OrderedDictionary<string, FunctionCType> FunctionTypedefTable { get; private set; }
        public HashSet<string> HandleTable { get; private set; }

        public CategorizedDeclarations()
        {
            FunctionTable = new OrderedDictionary<string, FunctionCType>();
            StructTable = new OrderedDictionary<string, StructCType>();
            EnumTable = new OrderedDictionary<string, EnumCType>();
            FunctionTypedefTable = new OrderedDictionary<string, FunctionCType>();
            HandleTable = new HashSet<string>();
        }

        public void AddDeclarations(IEnumerable<Declaration> aDeclarations)
        {
            foreach (var decl in aDeclarations)
            {
                if (decl.Name == "sp_uint64" || decl.Name == "bool" || decl.Name == "byte") continue;
                if (decl.Kind == "typedef")
                {
                    StructCType structType = decl.CType as StructCType;
                    EnumCType enumType = decl.CType as EnumCType;
                    FunctionCType functionType = decl.CType as FunctionCType;
                    if (structType != null)
                    {
                        if (structType.Fields == null)
                        {
                            HandleTable.Add(decl.Name);
                        }
                        else
                        {
                            StructTable.Add(decl.Name, structType);
                        }
                    }
                    else if (enumType != null)
                    {
                        EnumTable.Add(decl.Name, enumType);
                    }
                    else if (functionType != null)
                    {
                        FunctionTypedefTable.Add(decl.Name, functionType);
                    }
                }
                else if (decl.Kind == "instance")
                {
                    FunctionCType funcType = decl.CType as FunctionCType;
                    if (funcType == null) continue;
                    FunctionTable.Add(decl.Name, funcType);
                }
            }
        }

        public OrderedDictionary<string, FunctionCType> FindAnonymousDelegates()
        {
            OrderedDictionary<string, FunctionCType> results = new OrderedDictionary<string, FunctionCType>();
            foreach (var kvp in StructTable)
            {
                var structName = kvp.Key;
                var structType = kvp.Value;
                foreach (var fieldDeclaration in structType.Fields)
                {
                    var fieldName = fieldDeclaration.Name;
                    var fieldType = fieldDeclaration.CType;
                    var pointerType = fieldType as PointerCType;
                    if (pointerType != null)
                    {
                        var pointedType = pointerType.BaseType;
                        var functionType = pointedType as FunctionCType;
                        if (functionType != null)
                        {
                            // What if there are duplicate names!?
                            results.Add(fieldName, functionType);
                        }
                    }
                }
            }
            return results;
        }

        public void CategorizeFunctions(
            out OrderedDictionary<string, SpotifyClass> aClasses,
            out OrderedDictionary<string, FunctionCType> aFunctions)
        {
            var classTable = new OrderedDictionary<string, SpotifyClass>();
            var unclaimedFunctions = new HashSet<string>(FunctionTable.Keys);
            foreach (string handleName in HandleTable)
            {
                var spotifyClass = new SpotifyClass(handleName);
                foreach (var kvp in FunctionTable)
                {
                    string name = kvp.Key;
                    var function = kvp.Value;
                    if (name.StartsWith(handleName + "_"))
                    {
                        spotifyClass.AddFunction(name, function);
                        unclaimedFunctions.Remove(name);
                    }
                }
                classTable.Add(handleName, spotifyClass);
            }
            aClasses = classTable;
            aFunctions = new OrderedDictionary<string, FunctionCType>(FunctionTable.Where(x => unclaimedFunctions.Contains(x.Key)));
        }
    }

    class SpotifyClass
    {
        public string HandleName { get; private set; }
        public Dictionary<string, FunctionCType> NativeFunctions { get; private set; }

        public SpotifyClass(string aHandleName)
        {
            HandleName = aHandleName;
            NativeFunctions = new Dictionary<string, FunctionCType>();
        }

        public void AddFunction(string aName, FunctionCType aFunction)
        {
            NativeFunctions.Add(aName, aFunction);
        }
    }

    class CSharpGenerator
    {
        /*const string FunctionDeclarationTemplate =
            "{0}[DllImport(\"spotify\")]\n" +
            "{0}{1}{2} {3}({4});\n";
        const string DllImportModifiers = "internal static extern ";
        const string RawDelegateModifiers = "internal delegate ";*/
        const string DllImportTemplate =
            "{0}[DllImport(\"spotify\")]\n" +
            "{0}internal static extern {1} {2}({3});\n";
        const string RawDelegateTemplate =
            "{0}internal delegate {1} {2}({3});\n";

        HashSet<string> iEnumNames;
        HashSet<string> iStructNames;
        HashSet<string> iHandleNames;
        HashSet<string> iDelegateNames;

        public CSharpGenerator(IEnumerable<string> aEnumNames, IEnumerable<string> aStructNames, IEnumerable<string> aHandleNames, IEnumerable<string> aDelegateNames)
        {
            iEnumNames = new HashSet<string>(aEnumNames);
            iStructNames = new HashSet<string>(aStructNames);
            iHandleNames = new HashSet<string>(aHandleNames);
            iDelegateNames = new HashSet<string>(aDelegateNames);
        }

        string GetCSharpMarshalType(CType aType, bool aAsParameter)
        {
            var pointerType = aType as PointerCType;
            var namedType = aType as NamedCType;
            var arrayType = aType as ArrayCType;
            if (pointerType != null)
            {
                if (!aAsParameter)
                {
                    return "IntPtr";
                }
                var pointedToType = pointerType.BaseType as NamedCType;
                if (pointedToType != null)
                {
                    string typeName = pointedToType.Name;
                    if (iStructNames.Contains(typeName))
                    {
                        return "ref " + pointedToType.Name;
                    }
                    if (iEnumNames.Contains(typeName))
                    {
                        return "ref " + pointedToType.Name;
                    }
                    if (iHandleNames.Contains(typeName))
                    {
                        return "IntPtr";
                    }
                    if (iDelegateNames.Contains(typeName))
                    {
                        return typeName; // DelegateNameFromTypedef??
                    }
                    if (typeName == "void") return "IntPtr";
                    if (typeName == "int") return "ref int";
                    if (typeName == "char") return "IntPtr";
                    if (typeName == "byte") return "IntPtr";
                    if (typeName == "size_t") return "UIntPtr";
                    return "/* ??? */ IntPtr";
                }
                return "IntPtr";
            }
            if (namedType != null)
            {
                switch (namedType.Name)
                {
                    case "void":
                        return "void";
                    case "int":
                        return "int";
                    case "bool":
                        return "bool";
                    case "sp_error":
                        return "sp_error";
                    case "size_t":
                        return "UIntPtr";
                    case "sp_uint64":
                        return "UIntPtr";
                }
                if (iEnumNames.Contains(namedType.Name))
                {
                    return namedType.Name;
                }
                throw new Exception("Don't know how to marshal type: " + namedType.Name);
            }
            if (arrayType != null)
            {
                return "IntPtr";
            }
            throw new Exception("Don't know how to marshal type: " + aType);
        }

        public string GenerateDllImportFunction(string aIndent, string aFunctionName, FunctionCType aFunctionType)
        {
            return GenerateFunctionDeclaration(
                aIndent, DllImportTemplate, aFunctionName, aFunctionType);
        }
        public string GenerateRawDelegate(string aIndent, string aFunctionName, FunctionCType aFunctionType)
        {
            return GenerateFunctionDeclaration(
                aIndent, RawDelegateTemplate, aFunctionName, aFunctionType);
        }

        string GenerateFunctionDeclaration(string aIndent, string aTemplate, string aFunctionName, FunctionCType aFunctionType)
        {
            string argString;
            if (aFunctionType.Arguments.Count == 1 && aFunctionType.Arguments[0].CType.ToString() == "void")
            {
                argString = "";
            }
            else
            {
                var args = aFunctionType.Arguments.Select(x => GetCSharpMarshalType(x.CType, true) + " @" + x.Name);
                argString = String.Join(", ", args.ToArray());
            }
            string returnType = GetCSharpMarshalType(aFunctionType.ReturnType, false);
            return String.Format(aTemplate, aIndent, returnType, aFunctionName, argString);
        }

        IEnumerable<string> SplitName(string aName)
        {
            return aName.Split('_');
        }

        string PascalCase(string aFragment)
        {
            return aFragment.Substring(0, 1).ToUpperInvariant() + aFragment.Substring(1).ToLowerInvariant();
        }

        string PascalCase(IEnumerable<string> aFragments)
        {
            return String.Join("", aFragments.Select(PascalCase));
        }

        string CamelCase(IEnumerable<string> aFragments)
        {
            var fragments = aFragments.ToList();
            var first = fragments[0].ToLowerInvariant();
            var remaining = fragments.Skip(1).Select(PascalCase);
            return first + String.Join("", remaining);
        }

        string PascalCaseMemberName(string aParentName, string aMemberName)
        {
            //if (!aMemberName.ToUpperInvariant().StartsWith(aParentName.ToUpperInvariant() + "_")) throw new Exception("Bad member name: " + aMemberName);
            //string trimmedName = aMemberName.Substring(aParentName.Length + 1);
            string trimmedName = aMemberName;
            return PascalCase(SplitName(trimmedName));
        }

        public string GenerateCSharpWrappingMethod(string aIndent, string aFunctionName, string aClassName, FunctionCType aFunctionType)
        {
            if (!aFunctionName.StartsWith(aClassName+"_"))
            {
                return aIndent + "// Bad method " + aFunctionName;
            }
            var methodName = PascalCase(SplitName(aFunctionName.Substring(aClassName.Length+1)));

            return "";
        }

        const string EnumTemplate =
            "{0}internal enum {1}\n" +
            "{0}{{\n" +
            "{2}" +
            "{0}}}\n";
        const string EnumConstantTemplate =
            "{0}{1} = {2},\n";

        const string SingleIndent = "    ";

        public string GenerateEnumDeclaration(string aIndent, string aEnumName, EnumCType aEnumType)
        {
            var constantStrings = aEnumType.Constants.Select(x =>
                String.Format(
                    EnumConstantTemplate,
                    aIndent + SingleIndent,
                    PascalCaseMemberName(aEnumName, x.Name),
                    x.Value));
            var joinedConstantStrings = String.Join("", constantStrings);
            return String.Format(EnumTemplate, aIndent, aEnumName, joinedConstantStrings);
        }

    }


    class Program
    {
        static void Main(string[] args)
        {
            var text = File.ReadAllText(args[0]);
            //var serializer = JsonSerializer.Create(new JsonSerializerSettings{Formatting=Formatting.Indented});
            var declarations = //(List<Declaration>)serializer.Deserialize(new StringReader(text), typeof(List<Declaration>));
                JsonConvert.DeserializeObject<List<Declaration>>(text, new CTypeConverter());
            /*foreach (var decl in declarations)
            {
                Console.WriteLine(decl.Name);
            }*/
            var categorizedDeclarations = new CategorizedDeclarations();
            categorizedDeclarations.AddDeclarations(declarations);
            OrderedDictionary<string, SpotifyClass> classes;
            OrderedDictionary<string, FunctionCType> functions;
            categorizedDeclarations.CategorizeFunctions(out classes, out functions);
            var anonymousDelegates = categorizedDeclarations.FindAnonymousDelegates();
            /*
            foreach (var kvpClass in classes.OrderBy(x=>x.Key))
            {
                var className = kvpClass.Key;
                var spotifyClass = kvpClass.Value;
                Console.WriteLine("CLASS: {0}", className);
                foreach (var kvpFunction in spotifyClass.NativeFunctions.OrderBy(x=>x.Key))
                {
                    var functionName = kvpFunction.Key;
                    var functionSignature = kvpFunction.Value;
                    Console.WriteLine("    {0}({1} arg(s))", functionName, functionSignature.Arguments.Count);
                }
            }
            Console.WriteLine("FUNCTIONS:");
            foreach (var kvpFunction in functions.OrderBy(x => x.Key))
            {
                var functionName = kvpFunction.Key;
                var functionSignature = kvpFunction.Value;
                Console.WriteLine("    {0}({1} arg(s))", functionName, functionSignature.Arguments.Count);
            }
             * */
            CSharpGenerator gen = new CSharpGenerator(
                categorizedDeclarations.EnumTable.Keys,
                categorizedDeclarations.StructTable.Keys,
                categorizedDeclarations.HandleTable,
                categorizedDeclarations.FunctionTypedefTable.Keys);
            Console.WriteLine("using System;");
            Console.WriteLine("using System.Runtime.InteropServices;");
            Console.WriteLine("namespace Stuff");
            Console.WriteLine("{");
            Console.WriteLine("");
            Console.WriteLine("    // Enums");
            foreach (var kvpEnum in categorizedDeclarations.EnumTable)
            {
                Console.Write(gen.GenerateEnumDeclaration("    ", kvpEnum.Key, kvpEnum.Value));
            }
            Console.WriteLine("");
            Console.WriteLine("    // Named delegates");
            foreach (var kvpDelegate in categorizedDeclarations.FunctionTypedefTable)
            {
                Console.Write(gen.GenerateRawDelegate("    ", kvpDelegate.Key, kvpDelegate.Value));
            }
            Console.WriteLine("");
            Console.WriteLine("    // Un-named delegates");
            foreach (var kvpDelegate in anonymousDelegates)
            {
                Console.Write(gen.GenerateRawDelegate("    ", kvpDelegate.Key, kvpDelegate.Value));
            }
            /*
            foreach (var kvpStruct in categorizedDeclarations.StructTable)
            {
                var structName = kvpStruct.Key;
                var structType = kvpStruct.Value;
                foreach (var field in structType.Fields)
                {
                    var pointerType = field.CType as PointerCType;
                    if (pointerType != null)
                    {
                        var functionType = pointerType.BaseType as FunctionCType;
                        if (functionType != null)
                        {
                            // Delegate!
                            gen.GenerateDelegateDeclaration(field.Name, functionType);
                        }
                    }
                }
                structType.Fields
            }*/
            Console.WriteLine("    class NativeMethods");
            Console.WriteLine("    {");
            foreach (var kvpFunction in categorizedDeclarations.FunctionTable)
            {
                var functionName = kvpFunction.Key;
                var functionSignature = kvpFunction.Value;
                Console.Write(gen.GenerateDllImportFunction("        ", functionName, functionSignature));
            }
            Console.WriteLine("    }");
            Console.WriteLine("}");


            /*
            Dictionary<string, FunctionCType> functionTable = new Dictionary<string, FunctionCType>();
            Dictionary<string, StructCType> structTable = new Dictionary<string, StructCType>();
            Dictionary<string, EnumCType> enumTable = new Dictionary<string, EnumCType>();
            HashSet<string> handleTable = new HashSet<string>();
            foreach (var decl in declarations)
            {
                if (decl.Name == "sp_uint64" || decl.Name == "bool" || decl.Name == "byte") continue;
                if (decl.Kind == "typedef")
                {
                    StructCType structType = decl.CType as StructCType;
                    EnumCType enumType = decl.CType as EnumCType;
                    if (structType != null)
                    {
                        if (structType.Fields == null)
                        {
                            handleTable.Add(decl.Name);
                        }
                        else
                        {
                            structTable.Add(decl.Name, structType);
                        }
                    }
                    else if (enumType != null)
                    {
                        enumTable.Add(decl.Name, enumType);
                    }
                }
                else if (decl.Kind == "instance")
                {
                    FunctionCType funcType = decl.CType as FunctionCType;
                    if (funcType == null) continue;
                    functionTable.Add(decl.Name, funcType);
                }
            }
            HashSet<string> allFunctions = new HashSet<string>(functionTable.Keys);
            HashSet<string> claimedFunctions = new HashSet<string>();
            foreach (var handleName in handleTable)
            {
                foreach (var kvp in functionTable)
                {
                    var name = kvp.Key;
                    var function = kvp.Value;
                    if (name.StartsWith(handleName))
                    {
                        claimedFunctions.Add(name);
                    }
                }
            }
            allFunctions.ExceptWith(claimedFunctions);
            foreach (string functionName in allFunctions)
            {
                Console.WriteLine("Unclaimed: {0}", functionName);
            }*/
        }
    }
}
