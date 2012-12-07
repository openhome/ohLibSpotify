using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ApiParser;
using Newtonsoft.Json;

namespace ManagedApiBuilder
{


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
            Console.WriteLine("namespace SpotifySharp");
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

            Console.WriteLine("");
            Console.WriteLine("    // Structs");
            foreach (var kvpStruct in categorizedDeclarations.StructTable)
            {
                var structName = kvpStruct.Key;
                var structType = kvpStruct.Value;
                Console.WriteLine(gen.GenerateStruct("    ", structName, structType));
                /*foreach (var field in structType.Fields)
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
                structType.Fields*/
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
