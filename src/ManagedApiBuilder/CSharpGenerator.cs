using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using ApiParser;
using Newtonsoft.Json;

namespace ManagedApiBuilder
{



    class CSharpType
    {
        public List<string> Attributes { get; private set; }
        public bool IsRef { get; set; }
        public string Name { get; private set; }
        public CSharpType(string aName)
        {
            Name = aName;
            Attributes = new List<string>();
        }
        string GetTypeString()
        {
            return (IsRef ? "ref " : "") + Name;
        }
        string GetAttributeString(string aPrefix = "")
        {
            if (Attributes.Count == 0)
                return "";
            return String.Format(
                "[{0}{1}]",
                aPrefix,
                String.Join(
                    ", ",
                    Attributes));
        }
        public string CreateParameterDeclaration(string aParameterName)
        {
            return GetAttributeString("") + GetTypeString() + " @" + aParameterName;
        }
        public string CreateReturnTypeDeclaration()
        {
            return GetTypeString();
        }
        public string CreateReturnTypeAttribute()
        {
            if (IsRef) throw new Exception("Cannot use ref type as return type.");
            return GetAttributeString("return:");
        }
        public string CreateFieldAttribute()
        {
            if (IsRef) throw new Exception("Cannot use ref type as return type.");
            return GetAttributeString("");
        }
        public string CreateFieldDeclaration(string aFieldName)
        {
            if (IsRef) throw new Exception("Cannot use ref type as field type.");
            return GetTypeString() + " @" + aFieldName;
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
            "{0}[DllImport(\"libspotify\")]\n" +
            "{4}" +
            "{0}internal static extern {1} {2}({3});\n";
        const string RawDelegateTemplate =
            "{4}" +
            "{0}internal delegate {1} {2}({3});\n";

        HashSet<string> iEnumNames;
        HashSet<string> iStructNames;
        HashSet<string> iHandleNames;
        HashSet<string> iDelegateNames;
        Dictionary<string, ApiStructConfiguration> iStructConfigurations;
        Dictionary<string, ApiEnumConfiguration> iEnumConfigurations;

        public CSharpGenerator(
            IEnumerable<string> aEnumNames,
            IEnumerable<string> aStructNames,
            IEnumerable<string> aHandleNames,
            IEnumerable<string> aDelegateNames,
            IEnumerable<ApiStructConfiguration> aStructConfigurations,
            IEnumerable<ApiEnumConfiguration> aEnumConfigurations)
        {
            iEnumNames = new HashSet<string>(aEnumNames);
            iStructNames = new HashSet<string>(aStructNames);
            iHandleNames = new HashSet<string>(aHandleNames);
            iDelegateNames = new HashSet<string>(aDelegateNames);
            iStructConfigurations = aStructConfigurations.ToDictionary(x => x.NativeName);
            iEnumConfigurations = aEnumConfigurations.ToDictionary(x => x.NativeName);
        }

        string ManagedNameForNativeType(string aNativeTypeName)
        {
            ApiStructConfiguration structConfig;
            if (iStructConfigurations.TryGetValue(aNativeTypeName, out structConfig))
            {
                return structConfig.ManagedName ?? aNativeTypeName;
            }
            ApiEnumConfiguration enumConfig;
            if (iEnumConfigurations.TryGetValue(aNativeTypeName, out enumConfig))
            {
                return enumConfig.ManagedName ?? aNativeTypeName;
            }
            // Delegate??
            // Handle??
            if (iEnumNames.Contains(aNativeTypeName))
            {
                return DefaultManagedEnumName(aNativeTypeName);
            }
            if (iHandleNames.Contains(aNativeTypeName))
            {
                return DefaultManagedClassName(aNativeTypeName);
            }
            return aNativeTypeName;
        }

        /*class PinvokeArgumentMapping
        {
            public Declaration NativeArgument { get; private set;}
            public string PinvokeName { get; private set; }
            public CSharpType PinvokeType { get; private set; }
            public PinvokeArgumentMapping(Declaration aNativeArgument, string aPinvokeName, CSharpType aPinvokeType)
            {
                NativeArgument = aNativeArgument;
                PinvokeName = aPinvokeName;
                PinvokeType = aPinvokeType;
            }
        }*/

        /*class ManagedArgumentMapping
        {
            public List<PinvokeArgumentMapping> NativeArguments { get; private set; }
            public string ManagedName { get; private set; }
            public CSharpType ManagedType { get; private set; }
            public ManagedArgumentMapping(string aManagedName, CSharpType aManagedType, params PinvokeArgumentMapping[] aPinvokeMappings)
            {
                ManagedName = aManagedName;
                ManagedType = aManagedType;
                NativeArguments = aPinvokeMappings.ToList();
            }
        }*/

        CSharpType GetCSharpMarshalType(CType aType, string aName, bool aAsParameter)
        {
            var pointerType = aType as PointerCType;
            var namedType = aType as NamedCType;
            var arrayType = aType as ArrayCType;
            if (pointerType != null)
            {
                var functionType = pointerType.BaseType as FunctionCType;
                if (functionType != null)
                {
                    // Delegate
                    // TODO: Derive delegate name from declaration name?
                    return new CSharpType(aName);
                }
                if (!aAsParameter)
                {
                    return new CSharpType("IntPtr");
                }
                var pointerPointerType = pointerType.BaseType as PointerCType;
                if (pointerPointerType != null)
                {
                    return new CSharpType("IntPtr") { IsRef = true };
                }
                var pointedToType = pointerType.BaseType as NamedCType;
                if (pointedToType != null)
                {
                    string typeName = pointedToType.Name;
                    if (iStructNames.Contains(typeName))
                    {
                        return new CSharpType(ManagedNameForNativeType(pointedToType.Name)) { IsRef = true };
                    }
                    if (iEnumNames.Contains(typeName))
                    {
                        return new CSharpType(ManagedNameForNativeType(pointedToType.Name)) { IsRef = true };
                    }
                    if (iHandleNames.Contains(typeName))
                    {
                        return new CSharpType("IntPtr");
                    }
                    if (iDelegateNames.Contains(typeName))
                    {
                        return new CSharpType(ManagedNameForNativeType(typeName));
                    }
                    if (typeName == "void") return new CSharpType("IntPtr");
                    if (typeName == "int") return new CSharpType("int") { IsRef = true };
                    if (typeName == "bool") return new CSharpType("bool") { IsRef = true, Attributes = { "MarshalAs(UnmanagedType.I1)" } };
                    if (typeName == "char") return new CSharpType("IntPtr");
                    if (typeName == "byte") return new CSharpType("IntPtr");
                    if (typeName == "size_t") return new CSharpType("UIntPtr") { IsRef = true };
                    return new CSharpType("/* ??? */ IntPtr"); // TODO: Out of band warning
                }
                return new CSharpType("IntPtr");
            }
            if (namedType != null)
            {
                switch (namedType.Name)
                {
                    case "void":
                        return new CSharpType("void");
                    case "int":
                        return new CSharpType("int");
                    case "bool":
                        return new CSharpType("bool") { Attributes = { "MarshalAs(UnmanagedType.I1)" } };
                    case "size_t":
                        return new CSharpType("UIntPtr");
                    case "sp_uint64":
                        return new CSharpType("ulong");
                }
                if (iEnumNames.Contains(namedType.Name))
                {
                    return new CSharpType(ManagedNameForNativeType(namedType.Name));
                }
                throw new Exception("Don't know how to marshal type: " + namedType.Name);
            }
            if (arrayType != null)
            {
                return new CSharpType("IntPtr");
            }
            throw new Exception("Don't know how to marshal type: " + aType);
        }

        public string GenerateDllImportFunction(string aIndent, string aFunctionName, FunctionCType aFunctionType)
        {
            /*string oldResult = GenerateFunctionDeclaration(
                aIndent, DllImportTemplate, aFunctionName, aFunctionType);*/
            var assembler = AssembleFunction(aFunctionName, aFunctionType, null, null);
            if (assembler == null)
            {
                Console.WriteLine("/////////////// FAILURE //////////////////");
                //Console.Write(oldResult);
            }
            string newResult = assembler.GeneratePInvokeDeclaration(aIndent);
            /*if (newResult != oldResult)
            {
                Console.WriteLine("/////////////// MISMATCH /////////////////");
                Console.Write(oldResult);
                Console.Write(newResult);
                throw new Exception("Mismatch!");
            }*/
            return newResult;
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
                var args = aFunctionType.Arguments.Select(x => GetCSharpMarshalType(x.CType, x.Name, true).CreateParameterDeclaration(x.Name));
                argString = String.Join(", ", args.ToArray());
            }
            var returnType = GetCSharpMarshalType(aFunctionType.ReturnType, "", false);
            var returnAttribute = returnType.CreateReturnTypeAttribute();
            if (returnAttribute != "")
                returnAttribute = aIndent + returnAttribute + "\n";
            var returnTypeDeclaration = returnType.CreateReturnTypeDeclaration();
            return String.Format(aTemplate, aIndent, returnTypeDeclaration, aFunctionName, argString, returnAttribute);
        }

        IEnumerable<string> SplitName(string aName)
        {
            return aName.Split('_');
        }

        string PascalCase(string aFragment)
        {
            if (aFragment.Length == 0)
                return "";
            //Console.WriteLine("PascalCase:[{0}]", aFragment);
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

        public string GenerateCSharpClass(string aIndent, string aHandleName, IEnumerable<KeyValuePair<string, FunctionCType>> aFunctions)
        {
            ApiStructConfiguration config;

            if (!iStructConfigurations.TryGetValue(aHandleName, out config))
            {
                config = new ApiStructConfiguration{
                    ManagedName = ManagedNameForNativeType(aHandleName),
                    NativeName = aHandleName
                };
            }

            StringBuilder methodBuilder = new StringBuilder();

            methodBuilder.AppendFormat("{0}public partial class {1}\n", aIndent, config.ManagedName);
            methodBuilder.AppendFormat("{0}{{\n", aIndent);
            methodBuilder.AppendFormat("{0}    internal IntPtr _handle;\n", aIndent);
            methodBuilder.AppendFormat("{0}    internal {1}(IntPtr handle)\n", aIndent, config.ManagedName);
            methodBuilder.AppendFormat("{0}    {{\n", aIndent);
            methodBuilder.AppendFormat("{0}        this._handle = handle;\n", aIndent);
            methodBuilder.AppendFormat("{0}    }}\n", aIndent);
            methodBuilder.AppendFormat("\n");
            HashSet<string> suppressedFunctions = new HashSet<string>(config.SuppressFunctions);
            foreach (var kvpFunction in aFunctions)
            {
                if (suppressedFunctions.Contains(kvpFunction.Key))
                {
                    methodBuilder.Append(aIndent + String.Format("    // Suppressed function '{0}'.\n", kvpFunction.Key));
                    continue;
                }
                methodBuilder.Append(GenerateCSharpWrappingMethod(aIndent+"    ", kvpFunction.Key, aHandleName, kvpFunction.Value));
            }
            methodBuilder.AppendLine(aIndent + "}");
            return methodBuilder.ToString();
        }

        FunctionAssembler AssembleFunction(string aFunctionName, FunctionCType aFunctionType, string aHandleName, string aMethodName)
        {
            var enumNames = iEnumNames.ToDictionary(x => x, ManagedNameForNativeType); // iEnumConfigurations.Values.ToDictionary(x => x.NativeName, x => x.ManagedName);
            var structNames = iStructNames.ToDictionary(x => x, ManagedNameForNativeType);
            var classNames = iHandleNames.ToDictionary(x => x, ManagedNameForNativeType);
            var delegateNames = iDelegateNames.ToDictionary(x => x, ManagedNameForNativeType);

            // TODO: Generate transformers once only.
            // This will require a different approach for ThisPointerArgumentTransformer.

            var transformers = new List<IArgumentTransformer>{
                // This-pointer must be handled first to avoid it getting
                // stolen as a normal handle argument.
                aHandleName == null ? null : new ThisPointerArgumentTransformer{HandleType = aHandleName},

                // C-style empty argument list can go in any order.
                new VoidArgumentListTransformer(),

                // These consume multiple arguments. They need to happen
                // early enough that single-argument mappings don't steal
                // their arguments.
                new StringReturnTransformer(),
                new UnknownLengthStringReturnTransformer(),
                new StringArgumentTransformer(),
                new HandleArrayArgumentTransformer(classNames),
                new TrivialArrayArgumentTransformer(),

                // Normal arguments.
                new FunctionPointerArgumentTransformer(delegateNames),
                new VoidStarArgumentTransformer(),
                new HandleArgumentTransformer(classNames),
                new RefStructArgumentTransformer(structNames),
                new ByteArrayArgumentTransformer(),
                new TrivialArgumentTransformer(enumNames),
                new RefHandleArgumentTransformer(iStructNames.Concat(iHandleNames)),
                new RefArgumentTransformer(enumNames),

                // Return types.
                // Error-return has to come first out of the return handlers,
                // otherwise the error will end up being treated as a regular
                // enum.
                new SpotifyErrorReturnTransformer(),
                new TrivialReturnTransformer(enumNames),
                new HandleReturnTransformer(classNames),
                new SimpleStringReturnTransformer(),
                // Pointer-return must come later than handle-return.
                new PointerReturnTransformer(),
                new VoidReturnTransformer()}.Where(x=>x!=null).ToList();

            var assembler = new FunctionAssembler(aFunctionName, aMethodName);
            var nativeFunction = new FunctionSpecificationAnalyser(aFunctionType.Arguments, aFunctionType.ReturnType);
            while (nativeFunction.CurrentParameter != null || nativeFunction.ReturnType != null)
            {
                var transformer = transformers.FirstOrDefault(x=>x.Apply(nativeFunction, assembler));
                if (transformer == null)
                {
                    //Console.WriteLine("/* FAILED AT ARG {0} */", nativeFunction.CurrentParameter != null ? nativeFunction.CurrentParameter.Name : "none");
                    return null; //aIndent + String.Format("// Skipped function '{0}'.\n", aFunctionName);
                }
                assembler.NextArgument();
            }
            return assembler;
        }

        public string GenerateCSharpWrappingMethod(string aIndent, string aFunctionName, string aHandleName, FunctionCType aFunctionType)
        {
            if (!aFunctionName.StartsWith(aHandleName+"_"))
            {
                return aIndent + "// Bad method " + aFunctionName + "\n";
            }
            var methodName = PascalCase(SplitName(aFunctionName.Substring(aHandleName.Length+1)));
            var assembler = AssembleFunction(aFunctionName, aFunctionType, aHandleName, methodName);

            /*
             * //var enumNames = new Dictionary<string, string> { { "sp_error", "SpotifyError" } }; // FIXME
            var enumNames = iEnumNames.ToDictionary(x => x, ManagedNameForNativeType); // iEnumConfigurations.Values.ToDictionary(x => x.NativeName, x => x.ManagedName);
            var classNames = iHandleNames.ToDictionary(x => x, ManagedNameForNativeType);

            var transformers = new List<IArgumentTransformer>{
                new ThisPointerArgumentTransformer{HandleType = aHandleName},
                new HandleArgumentTransformer(classNames),
                new SpotifyErrorReturnTransformer(),
                new StringReturnTransformer(),
                new UnknownLengthStringReturnTransformer(),
                new StringArgumentTransformer(),
                new TrivialArgumentTransformer(enumNames),
                new RefArgumentTransformer(enumNames),
                new TrivialReturnTransformer(enumNames),
                new HandleReturnTransformer(classNames),
                new SimpleStringReturnTransformer(),
                new VoidReturnTransformer()};
            //Declaration arg1 = new Declaration { Name = "inputString", Kind = "instance", CType = new PointerCType(new NamedCType("char")) };
            //Declaration arg2 = new Declaration { Name = "flag1", Kind = "instance", CType = new NamedCType("bool") };
            //Declaration arg3 = new Declaration { Name = "ptrToInt", Kind = "instance", CType = new PointerCType(new NamedCType("int")) };
            //Console.WriteLine(stringTransformer.CanApply(arg1, null, null));
            //Declaration arg4 = new Declaration { Name = "outputString", Kind = "instance", CType = new PointerCType(new NamedCType("char")) };
            //Declaration arg5 = new Declaration { Name = "bufferLength", Kind = "instance", CType = new NamedCType("size_t") };
            //CType retType = new NamedCType("int");
            //List<Declaration> arguments = new List<Declaration> { arg1, arg2, arg3, arg4, arg5 };
            var assembler = new FunctionAssembler(aFunctionName, methodName);
            var nativeFunction = new FunctionSpecificationAnalyser(aFunctionType.Arguments, aFunctionType.ReturnType);
            while (nativeFunction.CurrentParameter != null || nativeFunction.ReturnType != null)
            {
                var transformer = transformers.FirstOrDefault(x=>x.Apply(nativeFunction, assembler));
                if (transformer == null)
                {
                    return aIndent + String.Format("// Skipped function '{0}'.\n", aFunctionName);
                }
                assembler.NextArgument();
            }*/

            if (assembler == null)
            {
                return aIndent + String.Format("// Skipped function '{0}'.\n", aFunctionName);
            }

            return assembler.GenerateWrapperMethod(aIndent);

            //return "";
        }

        const string EnumTemplate =
            "{0}public enum {1}\n" +
                "{0}{{\n" +
                    "{2}" +
                        "{0}}}\n";
        const string EnumConstantTemplate =
            "{0}{1} = {2},\n";

        const string SingleIndent = "    ";

        static string DropPrefix(string aString, string aPrefix)
        {
            if (!aString.StartsWith(aPrefix))
            {
                throw new Exception(String.Format("Expected '{0}' to start with '{1}'.", aString, aPrefix));
            }
            var retval = aString.Substring(aPrefix.Length);
            //Console.WriteLine("DropPrefix([{0}], [{1}]) -> {2}", aString, aPrefix, retval);
            return retval;
        }

        string DefaultManagedClassName(string aNativeName)
        {
            return DefaultManagedTypeName(aNativeName);
        }
        string DefaultManagedEnumName(string aNativeName)
        {
            return DefaultManagedTypeName(aNativeName);
        }
        string DefaultManagedTypeName(string aNativeName)
        {
            if (aNativeName.StartsWith("sp_"))
            {
                aNativeName = aNativeName.Substring(3);
            }
            return PascalCase(SplitName(aNativeName));
        }

        string DefaultNativeConstantPrefix(string aNativeName)
        {
            return aNativeName.ToUpperInvariant() + "_";
        }

        public string GenerateEnumDeclaration(string aIndent, string aEnumName, EnumCType aEnumType)
        {
            ApiEnumConfiguration configuration;
            iEnumConfigurations.TryGetValue(aEnumName, out configuration);
            string managedName = configuration != null ? configuration.ManagedName : null;
            managedName = managedName ?? DefaultManagedEnumName(aEnumName);
            string memberPrefix = configuration != null ? configuration.NativeConstantPrefix : null;
            memberPrefix = memberPrefix ?? DefaultNativeConstantPrefix(aEnumName);
            string managedMemberPrefix = configuration != null ? configuration.ManagedConstantPrefix : null;
            managedMemberPrefix = managedMemberPrefix ?? "";
            var constantStrings = aEnumType.Constants.Select(x =>
                String.Format(
                    EnumConstantTemplate,
                    aIndent + SingleIndent,
                    managedMemberPrefix + PascalCaseMemberName(aEnumName, DropPrefix(x.Name, memberPrefix)),
                    x.Value));
            var joinedConstantStrings = String.Join("", constantStrings);
            return String.Format(EnumTemplate, aIndent, managedName, joinedConstantStrings);
        }

        const string StructTemplate =
            "{0}{3} struct {1}\n" +
            "{0}{{\n" +
            "{2}" +
            "{0}}}\n";
        const string StructFieldTemplate =
            "{2}" +
            "{0}public {1};\n";

        public string GenerateStruct(string aIndent, string aStructName, StructCType aStructType)
        {
            var fieldStrings = aStructType.Fields.Select(x =>
                {
                    var csharpType = GetCSharpMarshalType(x.CType, x.Name, false);
                    var attribute = csharpType.CreateFieldAttribute();
                    if (attribute != "")
                        attribute = aIndent + SingleIndent + attribute + "\n";
                    return String.Format(
                        StructFieldTemplate,
                        aIndent + SingleIndent,
                        csharpType.CreateFieldDeclaration(x.Name),
                        attribute);
                });
            var joinedFieldStrings = String.Join("", fieldStrings);
            ApiStructConfiguration config;
            if (!iStructConfigurations.TryGetValue(aStructName, out config))
            {
                config = new ApiStructConfiguration{
                    ManagedName = ManagedNameForNativeType(aStructName),
                    NativeName = aStructName,
                    ForcePublic = false
                };
            }
            string visibility = config.ForcePublic ? "public" : "internal";
            return String.Format(StructTemplate, aIndent, config.ManagedName, joinedFieldStrings, visibility);
        }
    }
}