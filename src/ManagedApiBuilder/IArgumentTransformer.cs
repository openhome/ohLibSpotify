using System;
using System.Collections.Generic;
using System.Linq;
using ApiParser;

namespace ManagedApiBuilder
{
    interface IArgumentTransformer
    {
        bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aFunctionAssembler);
        //bool CanApply(Declaration aCurrentArg, Declaration aNextArg, CType aReturnType);
        //void Apply(Declaration aCurrentArg, Declaration aNextArg, CType aReturnType, IFunctionAssembler aAssembler);
    }

    class TrivialArgumentTransformer : IArgumentTransformer
    {
        Dictionary<string, string> iEnumNativeToManagedMappings;
        public TrivialArgumentTransformer(IEnumerable<KeyValuePair<string, string>> aEnumNativeToManagedMappings)
        {
            iEnumNativeToManagedMappings = aEnumNativeToManagedMappings.ToDictionary(x => x.Key, x => x.Value);
        }
        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {
            NamedCType nativeType = aNativeFunction.CurrentParameterType as NamedCType;
            if (nativeType == null) return false;
            CSharpType pinvokeArgType;
            CSharpType managedArgType;
            switch (nativeType.Name)
            {
                case "bool":
                    pinvokeArgType = new CSharpType("bool"){ Attributes = { "MarshalAs(UnmanagedType.I1)" } };
                    managedArgType = new CSharpType("bool");
                    break;
                case "int":
                    pinvokeArgType = managedArgType = new CSharpType("int");
                    break;
                case "size_t":
                    pinvokeArgType = managedArgType = new CSharpType("UIntPtr");
                    break;
                default:
                    string managedEnumName;
                    if (!iEnumNativeToManagedMappings.TryGetValue(nativeType.Name, out managedEnumName))
                    {
                        return false;
                    }
                    pinvokeArgType = managedArgType = new CSharpType(managedEnumName);
                    break;
            }
            aAssembler.AddPInvokeParameter(pinvokeArgType, aNativeFunction.CurrentParameter.Name, aNativeFunction.CurrentParameter.Name);
            //Console.WriteLine("foo {0} {1} {2}", aAssembler == null, aNativeFunction == null, aNativeFunction.CurrentParameter == null);
            aAssembler.AddManagedParameter(aNativeFunction.CurrentParameter.Name, managedArgType);
            aNativeFunction.ConsumeArgument();
            return true;
        }
    }

    class HandleArgumentTransformer : IArgumentTransformer
    {
        Dictionary<string, string> iHandlesToClassNames;

        public HandleArgumentTransformer(IEnumerable<KeyValuePair<string, string>> aHandlesToClassNames)
        {
            iHandlesToClassNames = aHandlesToClassNames.ToDictionary(x=>x.Key, x=>x.Value);
        }

        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {
            var pointerType = aNativeFunction.CurrentParameterType as PointerCType;
            if (pointerType == null) return false;
            var namedType = pointerType.BaseType as NamedCType;
            if (namedType == null) return false;
            string className;
            if (!iHandlesToClassNames.TryGetValue(namedType.Name, out className))
            {
                return false;
            }

            aAssembler.AddPInvokeParameter(new CSharpType("IntPtr"), aNativeFunction.CurrentParameter.Name, aNativeFunction.CurrentParameter.Name + "._handle");
            aAssembler.AddManagedParameter(aNativeFunction.CurrentParameter.Name, new CSharpType(className));
            aNativeFunction.ConsumeArgument();
            return true;
        }
    }

    class FunctionPointerArgumentTransformer : IArgumentTransformer
    {
        Dictionary<string, string> iFunctionTypedefsToDelegates;

        public FunctionPointerArgumentTransformer(IEnumerable<KeyValuePair<string, string>> aFunctionTypedefsToDelegates)
        {
            iFunctionTypedefsToDelegates = aFunctionTypedefsToDelegates.ToDictionary(x=>x.Key, x=>x.Value);
        }

        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {
            var pointerType = aNativeFunction.CurrentParameterType as PointerCType;
            if (pointerType == null) return false;
            var namedType = pointerType.BaseType as NamedCType;
            if (namedType == null) return false;
            string className;
            if (!iFunctionTypedefsToDelegates.TryGetValue(namedType.Name, out className))
            {
                return false;
            }

            aAssembler.AddPInvokeParameter(new CSharpType(className), aNativeFunction.CurrentParameter.Name, null);
            aAssembler.SuppressManagedWrapper();
            aNativeFunction.ConsumeArgument();
            return true;
        }
    }

    class RefStructArgumentTransformer : IArgumentTransformer
    {
        readonly Dictionary<string, string> iHandlesToStructNames;

        public RefStructArgumentTransformer(IEnumerable<KeyValuePair<string, string>> aHandlesToStructNames)
        {
            iHandlesToStructNames = aHandlesToStructNames.ToDictionary(x=>x.Key, x=>x.Value);
        }

        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {
            var pointerType = aNativeFunction.CurrentParameterType as PointerCType;
            if (pointerType == null) return false;
            var namedType = pointerType.BaseType as NamedCType;
            if (namedType == null) return false;
            string structName;
            if (!iHandlesToStructNames.TryGetValue(namedType.Name, out structName))
            {
                return false;
            }

            aAssembler.AddPInvokeParameter(new CSharpType(structName) { IsRef = true }, aNativeFunction.CurrentParameter.Name, null);
            aAssembler.SuppressManagedWrapper();
            //aAssembler.AddManagedParameter(aNativeFunction.CurrentParameter.Name, new CSharpType(className));
            aNativeFunction.ConsumeArgument();
            return true;
        }
    }

    class PointerReturnTransformer : IArgumentTransformer
    {
        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {
            if (aNativeFunction.CurrentParameter != null) return false;
            if (!(aNativeFunction.ReturnType is PointerCType)) return false;

            aAssembler.SetPInvokeReturn(new CSharpType("IntPtr"), null);
            aAssembler.SuppressManagedWrapper();
            aNativeFunction.ConsumeReturn();
            return true;
        }
    }

    class ThisPointerArgumentTransformer : IArgumentTransformer
    {
        public string HandleType { get; set; }
        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {
            if (aNativeFunction.CurrentParameterIndex != 0) return false;
            if (!aNativeFunction.CurrentParameter.CType.MatchToPattern(new PointerCType(new NamedCType(HandleType))).IsMatch)
            {
                return false;
            }
            aAssembler.AddPInvokeParameter(new CSharpType("IntPtr"), aNativeFunction.CurrentParameter.Name, "this._handle");
            aAssembler.IsStatic = false;
            aNativeFunction.ConsumeArgument();
            return true;
        }
    }
    

    /// <summary>
    /// Handles   int (... char *buffer, size_t buffer_size)
    /// </summary>
    class StringReturnTransformer : IArgumentTransformer
    {
        /*
        public bool CanApply(Declaration aCurrentArg, Declaration aNextArg, CType aReturnType)
        {
            var matcher = Matcher.CType(
                new TupleCType(aCurrentArg==null?null:aCurrentArg.CType, aNextArg==null?null:aNextArg.CType, aReturnType));
            return (matcher.Match(new TupleCType(
                new PointerCType(new NamedCType("char")),
                new NamedCType("size_t"),
                new NamedCType("int")
                )));
        }*/

        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {
            var matcher = Matcher.CType(
                new TupleCType(aNativeFunction.CurrentParameterType, aNativeFunction.NextParameterType, aNativeFunction.ReturnType));
            if (!aNativeFunction.CurrentParameterType.MatchToPattern(
                new PointerCType(new NamedCType("char"))).IsMatch)
            {
                return false;
            }
            if (-1 != Matcher.CType(aNativeFunction.CurrentParameterType).FirstMatch(
                new NamedCType("size_t"),
                new NamedCType("int")
                ))
            {
                return false;
            }
            if (!aNativeFunction.ReturnType.MatchToPattern(
                new NamedCType("int")).IsMatch)
            {
                return false;
            }
            if (aNativeFunction.CurrentParameterIndex != aNativeFunction.ParameterCount - 2)
            {
                return false;
            }
            string lengthNativeType = ((NamedCType)aNativeFunction.NextParameterType).Name;
            string lengthManagedType = lengthNativeType == "size_t" ? "UIntPtr" : "int";
            string parameterName = aNativeFunction.CurrentParameter.Name;
            string utf8StringName = "utf8_"+parameterName;
            aAssembler.AddPInvokeParameter(new CSharpType("IntPtr"), aNativeFunction.CurrentParameter.Name, utf8StringName + ".IntPtr");
            aAssembler.AddPInvokeParameter(new CSharpType(lengthManagedType), aNativeFunction.NextParameter.Name, "(" + lengthManagedType + ")(" + utf8StringName + ".BufferLength)");
            aAssembler.SetPInvokeReturn(new CSharpType("int"), "stringLength_"+parameterName);
            aAssembler.SetManagedReturn(new CSharpType("string"));
            aAssembler.InsertAtTop(      "string returnValue;");
            aAssembler.InsertAtTop("int stringLength_" + parameterName + " = 256;");

            aAssembler.InsertBeforeCall("using (Utf8String " + utf8StringName + " = SpotifyMarshalling.AllocBuffer(stringLength_" + parameterName + "))");
            aAssembler.InsertBeforeCall("{");
            aAssembler.IncreaseIndent();
            aAssembler.InsertPreCall("stringLength_" + parameterName);
            aAssembler.InsertBeforeCall(utf8StringName + ".ReallocIfSmaller(stringLength_" + parameterName + " + 1);");

            aAssembler.InsertAfterCall("returnValue = " + utf8StringName + ".GetString(stringLength_" + parameterName + ");");
            aAssembler.DecreaseIndent();
            aAssembler.InsertAfterCall("}");

            aAssembler.InsertAtEnd("return returnValue;");
            aNativeFunction.ConsumeArgument();
            aNativeFunction.ConsumeArgument();
            aNativeFunction.ConsumeReturn();
            return true;
        }
    }

    /// <summary>
    /// Handles (... char *buffer, int buffer_size ...)
    /// The returned string might be truncated to fit in the buffer. Since the function
    /// doesn't tell us how long the full string is, we just have to live with the
    /// truncation. (We use a fixed buffer size of 256. That seems to be the longest
    /// string the Spotify app will allow in such places.)
    /// </summary>
    class UnknownLengthStringReturnTransformer : IArgumentTransformer
    {
        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {
            var matcher = Matcher.CType(
                new TupleCType(aNativeFunction.CurrentParameterType, aNativeFunction.NextParameterType));
            if (!matcher.Match(new TupleCType(
                new PointerCType(new NamedCType("char")),
                new NamedCType("int")
                )))
            {
                return false;
            }
            if (aNativeFunction.CurrentParameter.Name != "buffer") return false;
            if (aNativeFunction.NextParameter.Name != "buffer_size") return false;

            string parameterName = aNativeFunction.CurrentParameter.Name;
            string utf8StringName = "utf8_"+parameterName;
            aAssembler.AddPInvokeParameter(new CSharpType("IntPtr"), aNativeFunction.CurrentParameter.Name, utf8StringName + ".IntPtr");
            aAssembler.AddPInvokeParameter(new CSharpType("int"), aNativeFunction.NextParameter.Name, utf8StringName + ".BufferLength");
            //aAssembler.SetPInvokeReturn(new CSharpType("int"), "stringLength_"+parameterName);
            aAssembler.SetManagedReturn(new CSharpType("string"));
            aAssembler.InsertAtTop(      "string returnValue;");

            aAssembler.InsertBeforeCall("using (Utf8String " + utf8StringName + " = SpotifyMarshalling.AllocBuffer(256))");
            aAssembler.InsertBeforeCall("{");
            aAssembler.IncreaseIndent();
            aAssembler.InsertAfterCall("returnValue = " + utf8StringName + ".Value;");
            aAssembler.DecreaseIndent();
            aAssembler.InsertAfterCall("}");

            aAssembler.InsertAtEnd("return returnValue;");
            aNativeFunction.ConsumeArgument();
            aNativeFunction.ConsumeArgument();
            return true;
        }
    }

    class StringArgumentTransformer : IArgumentTransformer
    {
        /*
        public bool CanApply(Declaration aCurrentArg, Declaration aNextArg, CType aReturnType)
        {
            var matcher = Matcher.CType(aCurrentArg==null?null:aCurrentArg.CType);
            return (matcher.Match(
                new PointerCType(new NamedCType("char"))
                ));
        }*/

        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {
            var matcher = Matcher.CType(aNativeFunction.CurrentParameterType);
            if (!matcher.Match(new PointerCType(new NamedCType("char"))))
            {
                return false;
            }
            string utf8StringName = "utf8_" + aNativeFunction.CurrentParameter.Name;
            aAssembler.AddPInvokeParameter(new CSharpType("IntPtr"), aNativeFunction.CurrentParameter.Name, utf8StringName + ".IntPtr");
            aAssembler.AddManagedParameter(aNativeFunction.CurrentParameter.Name, new CSharpType("string"));
            aAssembler.InsertBeforeCall("using (Utf8String " + utf8StringName + " = SpotifyMarshalling.StringToUtf8(" + aNativeFunction.CurrentParameter.Name + "))");
            aAssembler.InsertBeforeCall("{");
            aAssembler.IncreaseIndent();
            aAssembler.DecreaseIndent();
            aAssembler.InsertAfterCall("}");
            aNativeFunction.ConsumeArgument();
            return true;
        }
    }

    class RefHandleArgumentTransformer : IArgumentTransformer
    {
        readonly HashSet<string> iHandleNames;

        public RefHandleArgumentTransformer(IEnumerable<string> aHandleNames)
        {
            iHandleNames = new HashSet<string>(aHandleNames);
        }
        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {
            PointerCType pointer1Type = aNativeFunction.CurrentParameterType as PointerCType;
            if (pointer1Type == null) return false;
            PointerCType pointer2Type = pointer1Type.BaseType as PointerCType;
            if (pointer2Type == null) return false;
            NamedCType nativeType = pointer2Type.BaseType as NamedCType;
            if (nativeType == null) return false;
            if (!iHandleNames.Contains(nativeType.Name)) return false;

            aAssembler.AddPInvokeParameter(
                new CSharpType("IntPtr") { IsRef = true },
                aNativeFunction.CurrentParameter.Name,
                null);
            aAssembler.SuppressManagedWrapper();
            aNativeFunction.ConsumeArgument();
            return true;
        }
    }

    class RefArgumentTransformer : IArgumentTransformer
    {
        /*
        public bool CanApply(Declaration aCurrentArg, Declaration aNextArg, CType aReturnType)
        {
            PointerCType pointerType = aCurrentArg.CType as PointerCType;
            if (pointerType == null) return false;
            NamedCType nativeType = pointerType.BaseType as NamedCType;
            if (nativeType == null) return false;
            switch (nativeType.Name)
            {
                case "bool":
                case "int":
                    return true;
                default:
                    return false;
            }
        }*/
        readonly Dictionary<string, string> iEnumNativeToManagedMappings;
        //readonly Dictionary<string, string> iStructNativeToManagedMappings;
        //readonly Dictionary<string, string> iHandleNativeToManagedMappings;
        public RefArgumentTransformer(
            IEnumerable<KeyValuePair<string, string>> aEnumNativeToManagedMappings
            //IEnumerable<KeyValuePair<string, string>> aStructNativeToManagedMappings,
            //IEnumerable<KeyValuePair<string, string>> aHandleNativeToManagedMappings
            )
        {
            iEnumNativeToManagedMappings = aEnumNativeToManagedMappings.ToDictionary(x => x.Key, x => x.Value);
            //iStructNativeToManagedMappings = aStructNativeToManagedMappings.ToDictionary(x => x.Key, x => x.Value);
            //iHandleNativeToManagedMappings = aHandleNativeToManagedMappings.ToDictionary(x => x.Key, x => x.Value);
        }

        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {

            PointerCType pointerType = aNativeFunction.CurrentParameterType as PointerCType;
            if (pointerType == null) return false;
            NamedCType nativeType = pointerType.BaseType as NamedCType;
            if (nativeType == null) return false;


            CSharpType pinvokeArgType;
            CSharpType managedArgType;
            switch (nativeType.Name)
            {
                case "bool":
                    pinvokeArgType = new CSharpType("bool") { IsRef = true, Attributes = { "MarshalAs(UnmanagedType.I1)" } };
                    managedArgType = new CSharpType("bool") { IsRef = true };
                    break;
                case "int":
                    pinvokeArgType = managedArgType = new CSharpType("int") { IsRef = true };
                    break;
                case "size_t":
                    pinvokeArgType = managedArgType = new CSharpType("UIntPtr") { IsRef = true };
                    break;
                case "sp_uint64":
                    pinvokeArgType = managedArgType = new CSharpType("ulong") { IsRef = true };
                    break;
                default:
                    string managedEnumName;
                    if (!iEnumNativeToManagedMappings.TryGetValue(nativeType.Name, out managedEnumName))
                    {
                        return false;
                    }
                    pinvokeArgType = managedArgType = new CSharpType(managedEnumName) { IsRef = true };
                    break;
            }

            aAssembler.AddPInvokeParameter(pinvokeArgType, aNativeFunction.CurrentParameter.Name, "ref @" + aNativeFunction.CurrentParameter.Name);
            aAssembler.AddManagedParameter(aNativeFunction.CurrentParameter.Name, managedArgType);
            aNativeFunction.ConsumeArgument();
            //if (suppressManaged)
            //{
            //    aAssembler.SuppressManagedWrapper();
            //}
            return true;
        }
    }

    class SpotifyErrorReturnTransformer : IArgumentTransformer
    {
        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aFunctionAssembler)
        {
            if (aNativeFunction.CurrentParameter != null) { return false; }
            var namedType = aNativeFunction.ReturnType as NamedCType;
            if (namedType == null) { return false; }
            if (namedType.Name != "sp_error") { return false; }
            aFunctionAssembler.InsertAtTop("SpotifyError errorValue;");
            aFunctionAssembler.SetPInvokeReturn(new CSharpType("SpotifyError"), "errorValue");
            aFunctionAssembler.InsertAtEnd("SpotifyMarshalling.CheckError(errorValue);");
            aNativeFunction.ConsumeReturn();
            return true;
        }
    }
    class TrivialReturnTransformer : IArgumentTransformer
    {
        Dictionary<string, string> iEnumNativeToManagedMappings;
        public TrivialReturnTransformer(IEnumerable<KeyValuePair<string, string>> aEnumNativeToManagedMappings)
        {
            iEnumNativeToManagedMappings = aEnumNativeToManagedMappings.ToDictionary(x => x.Key, x => x.Value);
        }
        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aFunctionAssembler)
        {
            if (aNativeFunction.CurrentParameter != null) { return false; }
            var namedType = aNativeFunction.ReturnType as NamedCType;
            if (namedType == null) { return false; }
            CSharpType pinvokeArgType;
            CSharpType managedArgType;
            switch (namedType.Name)
            {
                case "bool":
                    pinvokeArgType = new CSharpType("bool"){ Attributes = { "MarshalAs(UnmanagedType.I1)" } };
                    managedArgType = new CSharpType("bool");
                    break;
                case "int":
                    pinvokeArgType = managedArgType = new CSharpType("int");
                    break;
                case "sp_uint64":
                    pinvokeArgType = managedArgType = new CSharpType("ulong");
                    break;
                default:
                    string managedEnumName;
                    if (!iEnumNativeToManagedMappings.TryGetValue(namedType.Name, out managedEnumName))
                    {
                        return false;
                    }
                    pinvokeArgType = managedArgType = new CSharpType(managedEnumName);
                    break;
            }
            aFunctionAssembler.InsertAtTop(managedArgType.Name + " returnValue;");
            aFunctionAssembler.SetPInvokeReturn(pinvokeArgType, "returnValue");
            aFunctionAssembler.SetManagedReturn(managedArgType);
            aFunctionAssembler.InsertAtEnd("return returnValue;");
            aNativeFunction.ConsumeReturn();
            return true;
        }
    }
    class HandleReturnTransformer : IArgumentTransformer
    {
        Dictionary<string, string> iHandlesToClassNames;

        public HandleReturnTransformer(IEnumerable<KeyValuePair<string, string>> aHandlesToClassNames)
        {
            iHandlesToClassNames = aHandlesToClassNames.ToDictionary(x=>x.Key, x=>x.Value);
        }

        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {
            if (aNativeFunction.CurrentParameter != null) { return false; }
            var pointerType = aNativeFunction.ReturnType as PointerCType;
            if (pointerType == null) return false;
            var namedType = pointerType.BaseType as NamedCType;
            if (namedType == null) return false;
            string className;
            if (!iHandlesToClassNames.TryGetValue(namedType.Name, out className))
            {
                return false;
            }

            //aAssembler.AddPInvokeParameter(new CSharpType("IntPtr"), aNativeFunction.CurrentParameter.Name + "._handle");
            //aAssembler.AddManagedParameter(aNativeFunction.CurrentParameter.Name, new CSharpType(className));
            //aNativeFunction.ConsumeArgument();


            aAssembler.InsertAtTop("IntPtr returnValue;");
            aAssembler.SetPInvokeReturn(new CSharpType("IntPtr"), "returnValue");
            aAssembler.SetManagedReturn(new CSharpType(className));
            aAssembler.InsertAtEnd("return new "+className+"(returnValue);");
            aNativeFunction.ConsumeReturn();
            return true;
        }
    }
    class VoidReturnTransformer : IArgumentTransformer
    {
        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aFunctionAssembler)
        {
            if (aNativeFunction.CurrentParameter != null) { return false; }
            if (!aNativeFunction.ReturnType.MatchToPattern(new NamedCType("void")).IsMatch)
            {
                return false;
            }
            aFunctionAssembler.SetManagedReturn(new CSharpType("void"));
            aNativeFunction.ConsumeReturn();
            return true;
        }
    }
    class SimpleStringReturnTransformer : IArgumentTransformer
    {
        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aFunctionAssembler)
        {
            if (aNativeFunction.CurrentParameter != null) { return false; }
            if (!aNativeFunction.ReturnType.MatchToPattern(new PointerCType(new NamedCType("char"))).IsMatch)
            {
                return false;
            }
            aFunctionAssembler.InsertAtTop("IntPtr returnValue;");
            aFunctionAssembler.SetPInvokeReturn(new CSharpType("IntPtr"), "returnValue");
            aFunctionAssembler.SetManagedReturn(new CSharpType("string"));
            aFunctionAssembler.InsertAtEnd("return SpotifyMarshalling.Utf8ToString(returnValue);");
            aNativeFunction.ConsumeReturn();
            return true;
        }
    }

    class VoidStarArgumentTransformer : IArgumentTransformer
    {
        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {
            var pointerType = aNativeFunction.CurrentParameterType as PointerCType;
            if (pointerType == null) return false;
            var namedType = pointerType.BaseType as NamedCType;
            if (namedType == null) return false;

            switch (namedType.Name)
            {
                case "void":
                    break;
                default:
                    return false;
            }

            aAssembler.AddPInvokeParameter(new CSharpType("IntPtr"), aNativeFunction.CurrentParameter.Name, null);
            aAssembler.SuppressManagedWrapper();
            aNativeFunction.ConsumeArgument();
            return true;
        }
    }

    class ByteArrayArgumentTransformer : IArgumentTransformer
    {
        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {
            var arrayType = aNativeFunction.CurrentParameterType as ArrayCType;
            if (arrayType == null) return false;
            var namedType = arrayType.BaseType as NamedCType;
            if (namedType == null) return false;

            switch (namedType.Name)
            {
                case "byte":
                    break;
                default:
                    return false;
            }

            aAssembler.AddPInvokeParameter(new CSharpType("IntPtr"), aNativeFunction.CurrentParameter.Name, null);
            aAssembler.SuppressManagedWrapper();
            aNativeFunction.ConsumeArgument();
            return true;
        }
    }

    class VoidArgumentListTransformer : IArgumentTransformer
    {
        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {
            if (aNativeFunction.CurrentParameterIndex != 0) return false;
            if (!aNativeFunction.CurrentParameterType.MatchToPattern(new NamedCType("void")).IsMatch)
            {
                return false;
            }
            aNativeFunction.ConsumeArgument();
            return true;
        }
    }
}