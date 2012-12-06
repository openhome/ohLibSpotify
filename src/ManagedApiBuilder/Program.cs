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
        public HashSet<string> HandleTable { get; private set; }

        public CategorizedDeclarations()
        {
            FunctionTable = new OrderedDictionary<string, FunctionCType>();
            StructTable = new OrderedDictionary<string, StructCType>();
            EnumTable = new OrderedDictionary<string, EnumCType>();
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
                }
                else if (decl.Kind == "instance")
                {
                    FunctionCType funcType = decl.CType as FunctionCType;
                    if (funcType == null) continue;
                    FunctionTable.Add(decl.Name, funcType);
                }
            }
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
        const string DllImportTemplate =
            "{0}[DllImport(\"spotify\")]\n" +
            "{0}internal static extern {1} {2}({3});\n";

        HashSet<string> iEnumNames;

        public CSharpGenerator(IEnumerable<string> aEnumNames)
        {
            iEnumNames = new HashSet<string>(aEnumNames);
        }

        string GetCSharpMarshalType(CType aType)
        {
            var pointerType = aType as PointerCType;
            var namedType = aType as NamedCType;
            var arrayType = aType as ArrayCType;
            if (pointerType != null)
            {
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
            string argString;
            if (aFunctionType.Arguments.Count == 1 && aFunctionType.Arguments[0].CType.ToString() == "void")
            {
                argString = "";
            }
            else
            {
                var args = aFunctionType.Arguments.Select(x => GetCSharpMarshalType(x.CType) + " @" + x.Name);
                argString = String.Join(", ", args.ToArray());
            }
            string returnType = GetCSharpMarshalType(aFunctionType.ReturnType);
            return String.Format(DllImportTemplate, aIndent, returnType, aFunctionName, argString);
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
            var constantStrings = aEnumType.Constants.Select(x => String.Format(EnumConstantTemplate, aIndent + SingleIndent, x.Name, x.Value));
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
            CSharpGenerator gen = new CSharpGenerator(categorizedDeclarations.EnumTable.Keys);
            Console.WriteLine("using System;");
            Console.WriteLine("using System.Runtime.InteropServices;");
            Console.WriteLine("namespace Stuff");
            Console.WriteLine("{");
            foreach (var kvpEnum in categorizedDeclarations.EnumTable)
            {
                Console.Write(gen.GenerateEnumDeclaration("    ", kvpEnum.Key, kvpEnum.Value));
            }
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
