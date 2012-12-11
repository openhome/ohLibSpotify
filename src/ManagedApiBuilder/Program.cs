using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ApiParser;
using Newtonsoft.Json;

namespace ManagedApiBuilder
{

    [JsonObject]
    public class ApiBuilderConfiguration
    {
        [JsonProperty("ignore")]
        public List<string> DeclarationsToIgnore { get; set; }
        [JsonProperty("namespace")]
        public string RootNamespace { get; set; }
        [JsonProperty("structs")]
        public List<ApiStructConfiguration> Structs { get; set; }
        [JsonProperty("enums")]
        public List<ApiEnumConfiguration> Enums { get; set; }
    }

    [JsonObject]
    public class ApiStructConfiguration
    {
        [JsonProperty("native-name")]
        public string NativeName { get; set; }
        [JsonProperty("managed-name")]
        public string ManagedName { get; set; }
    }

    [JsonObject]
    public class ApiEnumConfiguration
    {
        [JsonProperty("native-name")]
        public string NativeName { get; set; }
        [JsonProperty("managed-name")]
        public string ManagedName { get; set; }
        [JsonProperty("native-constant-prefix")]
        public string NativeConstantPrefix { get; set; }
        [JsonProperty("managed-constant-prefix")]
        public string ManagedConstantPrefix { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var text = File.ReadAllText(args[0]);
            var declarations =
                JsonConvert.DeserializeObject<List<Declaration>>(text, new CTypeConverter());

            var configurationJson = File.ReadAllText(args[1]);
            var configuration = JsonConvert.DeserializeObject<ApiBuilderConfiguration>(configurationJson);

            var categorizedDeclarations = new CategorizedDeclarations(configuration.DeclarationsToIgnore);
            categorizedDeclarations.AddDeclarations(declarations);
            OrderedDictionary<string, SpotifyClass> classes;
            OrderedDictionary<string, FunctionCType> functions;
            categorizedDeclarations.CategorizeFunctions(out classes, out functions);
            var anonymousDelegates = categorizedDeclarations.FindAnonymousDelegates();
            CSharpGenerator gen = new CSharpGenerator(
                categorizedDeclarations.EnumTable.Keys,
                categorizedDeclarations.StructTable.Keys,
                categorizedDeclarations.HandleTable,
                categorizedDeclarations.FunctionTypedefTable.Keys,
                configuration.Structs,
                configuration.Enums);
            Console.WriteLine("using System;");
            Console.WriteLine("using System.Runtime.InteropServices;");
            Console.WriteLine("namespace "+configuration.RootNamespace);
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
        }
    }
}
