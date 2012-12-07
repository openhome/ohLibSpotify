using System;
using System.Collections.Generic;
using System.Linq;
using ApiParser;

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
        string GetAttributeString(string aPrefix="")
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
            "{0}[DllImport(\"spotify\")]\n" +
            "{4}" +
            "{0}internal static extern {1} {2}({3});\n";
        const string RawDelegateTemplate =
            "{4}" +
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
                var pointedToType = pointerType.BaseType as NamedCType;
                if (pointedToType != null)
                {
                    string typeName = pointedToType.Name;
                    if (iStructNames.Contains(typeName))
                    {
                        return new CSharpType(pointedToType.Name) { IsRef = true };
                    }
                    if (iEnumNames.Contains(typeName))
                    {
                        return new CSharpType(pointedToType.Name) { IsRef = true };
                    }
                    if (iHandleNames.Contains(typeName))
                    {
                        return new CSharpType("IntPtr");
                    }
                    if (iDelegateNames.Contains(typeName))
                    {
                        return new CSharpType(typeName); // DelegateNameFromTypedef??
                    }
                    if (typeName == "void") return new CSharpType("IntPtr");
                    if (typeName == "int") return new CSharpType("int") { IsRef = true };
                    if (typeName == "bool") return new CSharpType("bool") { IsRef = true, Attributes = { "MarshalAs(UnmanagedType.I1)" } };
                    if (typeName == "char") return new CSharpType("IntPtr");
                    if (typeName == "byte") return new CSharpType("IntPtr");
                    if (typeName == "size_t") return new CSharpType("UIntPtr");
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
                    case "sp_error":
                        return new CSharpType("sp_error");
                    case "size_t":
                        return new CSharpType("UIntPtr");
                    case "sp_uint64":
                        return new CSharpType("UIntPtr");
                }
                if (iEnumNames.Contains(namedType.Name))
                {
                    return new CSharpType(namedType.Name);
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

        const string StructTemplate =
            "{0}internal struct {1}\n" +
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
                        attribute = aIndent + attribute + "\n";
                    return String.Format(
                        StructFieldTemplate,
                        aIndent + SingleIndent,
                        csharpType.CreateFieldDeclaration(x.Name),
                        attribute);
                });
            var joinedFieldStrings = String.Join("", fieldStrings);
            return String.Format(StructTemplate, aIndent, aStructName, joinedFieldStrings);
        }
    }
}