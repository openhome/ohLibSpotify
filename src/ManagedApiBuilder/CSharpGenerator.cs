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


    interface IFunctionAssembler
    {
        void AddPInvokeParameter(CSharpType aType, string aExpression);
        void SetPInvokeReturn(CSharpType aType, string aVariableName);
        void AddManagedParameter(string aName, CSharpType aType);
        void SetManagedReturn(CSharpType aType);
        void InsertAtTop(string aCode);
        void InsertAtEnd(string aCode);
        void InsertBeforeCall(string aCode);
        void InsertAfterCall(string aCode);
        void InsertPreCall(string aVariableName);
        void IncreaseIndent();
        void DecreaseIndent();
    }

    interface IFunctionSpecificationAnalyser
    {
        int CurrentParameterIndex { get; }
        Declaration CurrentParameter { get; }
        Declaration NextParameter { get; }
        int ParameterCount { get; }
        CType CurrentParameterType { get; }
        CType NextParameterType { get; }
        CType ReturnType { get; }
        void MoveNext();
    }

    class FunctionSpecificationAnalyser : IFunctionSpecificationAnalyser
    {
        int iIndex = 0;
        readonly List<Declaration> iParameters;
        readonly CType iReturnType;

        public FunctionSpecificationAnalyser(List<Declaration> aParameters, CType aReturnType)
        {
            iParameters = aParameters;
            iReturnType = aReturnType;
        }

        public int CurrentParameterIndex { get { return iIndex; } }

        public Declaration CurrentParameter { get { return iParameters.ElementAtOrDefault(iIndex); } }

        public Declaration NextParameter { get { return iParameters.ElementAtOrDefault(iIndex+1); } }

        public int ParameterCount { get { return iParameters.Count; } }

        public CType CurrentParameterType
        {
            get
            {
                var parameter = CurrentParameter;
                return parameter == null ? null : parameter.CType;
            }
        }

        public CType NextParameterType
        {
            get
            {
                var parameter = NextParameter;
                return parameter == null ? null : parameter.CType;
            }
        }

        public CType ReturnType { get { return iReturnType; } }

        public void MoveNext()
        {
            if (iIndex < iParameters.Count)
            {
                iIndex += 1;
            }
        }
    }

    class FunctionAssembler : IFunctionAssembler
    {
        int iIndentLevelAbove = 0;
        int iIndentLevelBelow = 0;
        CSharpType iPInvokeReturnType = new CSharpType("void");
        string iPInvokeReturnVariable = null;
        /// <summary>
        /// The C# type of each native argument.
        /// </summary>
        //List<KeyValuePair<string, CSharpType>> iPInvokeArguments = new List<KeyValuePair<string, CSharpType>>();
        /// <summary>
        /// The actual text of the C# expressions to use for each
        /// argument to the native function.
        /// </summary>
        List<string> iArgumentExpressions = new List<string>();
        List<KeyValuePair<string, CSharpType>> iManagedArguments = new List<KeyValuePair<string, CSharpType>>();
        List<string> iTopCode = new List<string>();
        List<List<string>> iBottomCode = new List<List<string>> { new List<string>() };
        List<string> iAboveCode = new List<string>();
        List<List<string>> iBelowCode = new List<List<string>> { new List<string>() };
        public string NativeFunctionName { get; set; }
        public string ManagedFunctionName { get; set; }
        CSharpType iManagedReturnType =null; // new CSharpType("void");
        public bool HasReturnType { get { return iManagedReturnType != null; } }
        //public List<Declaration> RemainingArguments { get; private set; }
        //public Declaration CurrentNativeArg { get { return RemainingArguments.FirstOrDefault(); } }
        //public Declaration NextNativeArg { get { return RemainingArguments.Skip(1).FirstOrDefault(); } }
        

        public FunctionAssembler(string aNativeFunctionName, string aManagedFunctionName)
        {
            //RemainingArguments = aRemainingArguments;
            NativeFunctionName = aNativeFunctionName;
            ManagedFunctionName = aManagedFunctionName;
        }

        public void AddPInvokeParameter(CSharpType aType, string aExpression)
        {
            //Declaration arg = RemainingArguments[0];
            //iPInvokeArguments.Add(new KeyValuePair<string, CSharpType>(arg.Name, aType));
            iArgumentExpressions.Add(aExpression);
        }

        public void SetPInvokeReturn(CSharpType aType, string aVariableName)
        {
            iPInvokeReturnType = aType;
            iPInvokeReturnVariable = aVariableName;
        }

        public void AddManagedParameter(string aName, CSharpType aType)
        {
            iManagedArguments.Add(new KeyValuePair<string, CSharpType>(aName, aType));
        }

        public void SetManagedReturn(CSharpType aType)
        {
            iManagedReturnType = aType;
        }

        public void InsertAtTop(string aCode)
        {
            iTopCode.Add(aCode);
        }

        public void InsertAtEnd(string aCode)
        {
            iBottomCode.Last().Add(aCode);
        }

        public void InsertBeforeCall(string aCode)
        {
            iAboveCode.Add(new String(' ', iIndentLevelAbove * 4) + aCode);
        }

        public void InsertAfterCall(string aCode)
        {
            iBelowCode.Last().Add(new String(' ', iIndentLevelBelow * 4) + aCode);
        }

        public void NextArgument()
        {
            iBottomCode.Add(new List<string>());
            iBelowCode.Add(new List<string>());
            iIndentLevelBelow = iIndentLevelAbove;
        }

        public string GetCallExpression()
        {
            return String.Format("NativeMethods.{0}({1})", NativeFunctionName, String.Join(", ", iArgumentExpressions));
        }

        public void InsertPreCall(string aVariableName)
        {
            InsertBeforeCall(String.Format("{0} = {1};", aVariableName, GetCallExpression()));
        }

        public void IncreaseIndent()
        {
            iIndentLevelAbove += 1;
            iIndentLevelBelow = iIndentLevelAbove;
        }

        public void DecreaseIndent()
        {
            iIndentLevelBelow -= 1;
        }

        public string GeneratePInvokeDeclaration()
        {
            throw new NotImplementedException();
        }

        public string GenerateWrapperMethod(string aIndent)
        {
            const string template =
                "{0}" +
                "{1}public {2} {3}({4})\n" +
                "{1}{{\n" +
                "{5}" +
                "{1}}}\n";
            string argString = String.Join(", ", iManagedArguments.Select(x => x.Value.CreateParameterDeclaration(x.Key)));

            var returnAttribute = iManagedReturnType.CreateReturnTypeAttribute();
            if (returnAttribute != "")
                returnAttribute = aIndent + returnAttribute + "\n";
            var returnTypeDeclaration = iManagedReturnType.CreateReturnTypeDeclaration();

            string bodyIndent = aIndent + "    ";
            StringBuilder bodyBuilder = new StringBuilder(); // *groan*
            foreach (string s in iTopCode)
            {
                bodyBuilder.Append(bodyIndent + s + "\n");
            }
            foreach (string s in iAboveCode)
            {
                bodyBuilder.Append(bodyIndent + s + "\n");
            }
            string assignmentLeft = iPInvokeReturnVariable == null ? "" : (iPInvokeReturnVariable + " = ");
            bodyBuilder.Append(bodyIndent + new string(' ', 4*iIndentLevelAbove) + assignmentLeft + GetCallExpression() + ";\n");
            foreach (string s in Enumerable.Reverse(iBelowCode).SelectMany(x=>x))
            {
                bodyBuilder.Append(bodyIndent + s + "\n");
            }
            foreach (string s in Enumerable.Reverse(iBottomCode).SelectMany(x=>x))
            {
                bodyBuilder.Append(bodyIndent + s + "\n");
            }


            return String.Format(template, returnAttribute, aIndent, returnTypeDeclaration, ManagedFunctionName, argString, bodyBuilder);
        }

    }

    static class Matcher
    {
        public static PatternMatcher<CType, CType> CType(CType aItem)
        {
            return new PatternMatcher<CType, CType>(new CTypeTreeWalker(), aItem);
        }
    }

    interface IArgumentTransformer
    {
        bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aFunctionAssembler);
        //bool CanApply(Declaration aCurrentArg, Declaration aNextArg, CType aReturnType);
        //void Apply(Declaration aCurrentArg, Declaration aNextArg, CType aReturnType, IFunctionAssembler aAssembler);
    }

    class TrivialArgumentTransformer : IArgumentTransformer
    {
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
                    /* Do something about enums. */
                    return false;
            }
            aAssembler.AddPInvokeParameter(pinvokeArgType, aNativeFunction.CurrentParameter.Name);
            //Console.WriteLine("foo {0} {1} {2}", aAssembler == null, aNativeFunction == null, aNativeFunction.CurrentParameter == null);
            aAssembler.AddManagedParameter(aNativeFunction.CurrentParameter.Name, managedArgType);
            aNativeFunction.MoveNext();
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
            aAssembler.AddPInvokeParameter(new CSharpType("IntPtr"), "this._handle");
            aNativeFunction.MoveNext();
            return true;
        }
    }
    

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
            if (!matcher.Match(new TupleCType(
                new PointerCType(new NamedCType("char")),
                new NamedCType("size_t"),
                new NamedCType("int")
                )))
            {
                return false;
            }
            if (aNativeFunction.CurrentParameterIndex != aNativeFunction.ParameterCount - 2)
            {
                return false;
            }
            string parameterName = aNativeFunction.CurrentParameter.Name;
            string utf8StringName = "utf8_"+parameterName;
            aAssembler.AddPInvokeParameter(new CSharpType("IntPtr"), utf8StringName + ".IntPtr");
            aAssembler.AddPInvokeParameter(new CSharpType("UIntPtr"), "(UIntPtr)(" + utf8StringName + ".BufferLength)");
            aAssembler.SetPInvokeReturn(new CSharpType("int"), "stringLength_"+parameterName);
            aAssembler.SetManagedReturn(new CSharpType("string"));
            aAssembler.InsertAtTop(      "string returnValue;");
            aAssembler.InsertAtTop("int stringLength_" + parameterName + ";");

            aAssembler.InsertBeforeCall("using (Utf8String " + utf8StringName + " = SpotifyMarshalling.AllocBuffer(stringLength_" + parameterName + "))");
            aAssembler.InsertBeforeCall("{");
            aAssembler.IncreaseIndent();
            aAssembler.InsertPreCall("stringLength_" + parameterName);
            aAssembler.InsertBeforeCall(utf8StringName + ".ReallocIfSmaller(stringLength_" + parameterName + " + 1);");

            aAssembler.InsertAfterCall("returnValue = " + utf8StringName + ".GetString(stringLength_" + parameterName + ");");
            aAssembler.DecreaseIndent();
            aAssembler.InsertAfterCall("}");

            aAssembler.InsertAtEnd("return returnValue;");
            aNativeFunction.MoveNext();
            aNativeFunction.MoveNext();
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
            aAssembler.AddPInvokeParameter(new CSharpType("IntPtr"), utf8StringName + ".IntPtr");
            aAssembler.AddManagedParameter(aNativeFunction.CurrentParameter.Name, new CSharpType("string"));
            aAssembler.InsertBeforeCall("using (Utf8String " + utf8StringName + " = SpotifyMarshalling.StringToUtf8(" + aNativeFunction.CurrentParameter.Name + "))");
            aAssembler.InsertBeforeCall("{");
            aAssembler.IncreaseIndent();
            aAssembler.DecreaseIndent();
            aAssembler.InsertAfterCall("}");
            aNativeFunction.MoveNext();
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

        public bool Apply(IFunctionSpecificationAnalyser aNativeFunction, IFunctionAssembler aAssembler)
        {

            PointerCType pointerType = aNativeFunction.CurrentParameterType as PointerCType;
            if (pointerType == null) return false;
            NamedCType nativeType = pointerType.BaseType as NamedCType;
            if (nativeType == null) return false;
            CSharpType csharpType;
            switch (nativeType.Name)
            {
                case "bool":
                    csharpType =
                        new CSharpType("bool") { IsRef = true };
                    break;
                case "int":
                    csharpType =
                        new CSharpType("int") { IsRef = true };
                    break;
                default:
                    return false;
            }
            aAssembler.AddPInvokeParameter(csharpType, "ref " + aNativeFunction.CurrentParameter.Name);
            aAssembler.AddManagedParameter(aNativeFunction.CurrentParameter.Name, csharpType);
            aNativeFunction.MoveNext();
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
            string typeName;
            switch (namedType.Name)
            {
                case "int":
                    typeName = "int";
                    break;
                case "bool":
                    typeName = "bool";
                    break;
                default:
                    if (iEnumNativeToManagedMappings.ContainsKey(namedType.Name))
                    {
                        typeName = iEnumNativeToManagedMappings[namedType.Name];
                        break;
                    }
                    return false;
            }
            aFunctionAssembler.InsertAtTop(typeName + " returnValue;");
            aFunctionAssembler.SetPInvokeReturn(new CSharpType(typeName), "returnValue");
            aFunctionAssembler.SetManagedReturn(new CSharpType(typeName));
            aFunctionAssembler.InsertAtEnd("return returnValue;");
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
            return true;
        }
    }




    interface IPatternTreeWalker<TPatternNode, TTreeNode>
    {
        /// <summary>
        /// Get the children of a node in the pattern we're using to match.
        /// </summary>
        /// <param name="aNode"></param>
        /// <returns></returns>
        IEnumerable<TPatternNode> PatternChildren(TPatternNode aNode);
        /// <summary>
        /// Get the children of a node in the tree we're trying to match.
        /// </summary>
        /// <param name="aNode"></param>
        /// <returns></returns>
        IEnumerable<TTreeNode> TreeChildren(TTreeNode aNode);
        /// <summary>
        /// If the node is a variable, return its name, otherwise return null.
        /// </summary>
        /// <param name="aNode"></param>
        /// <returns></returns>
        string Variable(TPatternNode aNode);
        /// <summary>
        /// Check whether the nodes are equivalent, ignoring their children.
        /// </summary>
        /// <param name="aPatternNode"></param>
        /// <param name="aSecond"></param>
        /// <returns></returns>
        bool NodeMatch(TPatternNode aPatternNode, TTreeNode aSecond);
    }

    // Used during pattern matching. If CType is null it matches anything,
    // while if CType is non-null it matches as CType. Regardless, when it
    // matches, it binds the match to the given variable name.
    public class VariableCType : CType
    {
        public string Name { get; set; }
        public CType CType { get; set; }
        public override string FundamentalType()
        {
            return Name;
        }
        public override string ToString()
        {
            return Name;
        }
        protected override void ConstructDeclaration(List<string> aPrefix, List<string> aSuffix)
        {
            return;
        }

        public VariableCType(string aName)
        {
            Name = aName;
        }
        public VariableCType(string aName, CType aCType)
        {
            Name = aName;
            CType = aCType;
        }
    }

    // Convenience for pattern matching. Allows us to construct an expression
    // of a pair of CTypes and match both at once.
    public class TupleCType : CType
    {
        public CType First { get; set; }
        public CType Second { get; set; }
        public CType[] Items { get; private set; }
        public override string ToString()
        {
            return String.Format("({0})", String.Join(", ", Items.Select(x => x.ToString())));
        }
        protected override void ConstructDeclaration(List<string> aPrefix, List<string> aSuffix)
        {
            throw new NotImplementedException();
        }

        public TupleCType(params CType[] aItems)
        {
            Items = aItems;
        }
    }

    class CTypeTreeWalker : IPatternTreeWalker<CType, CType>
    {
        public IEnumerable<CType> PatternChildren(CType aNode)
        {
            var pointerType = aNode as PointerCType;
            var arrayType = aNode as ArrayCType;
            var namedType = aNode as NamedCType;
            var functionType = aNode as FunctionCType;
            var structType = aNode as StructCType;
            var enumType = aNode as EnumCType;
            var variableType = aNode as VariableCType;
            var tupleType = aNode as TupleCType;

            if (pointerType != null)
            {
                yield return pointerType.BaseType;
                yield break;
            }
            if (arrayType != null)
            {
                yield return arrayType.BaseType;
                yield break;
            }
            if (namedType != null)
            {
                yield break;
            }
            if (functionType != null)
            {
                foreach (var arg in functionType.Arguments)
                {
                    yield return arg.CType;
                }
                yield return functionType.ReturnType;
                yield break;
            }
            if (structType != null)
            {
                foreach (var field in structType.Fields)
                {
                    yield return field.CType;
                }
                yield break;
            }
            if (enumType != null)
            {
                yield break;
            }
            if (variableType != null)
            {
                if (variableType.CType != null)
                {
                    foreach (var item in PatternChildren(variableType.CType))
                    {
                        yield return item;
                    }
                }
                yield break;
            }
            if (tupleType != null)
            {
                foreach (var item in tupleType.Items)
                {
                    yield return item;
                }
                yield break;
            }
        }

        public IEnumerable<CType> TreeChildren(CType aNode)
        {
            return PatternChildren(aNode);
        }

        public string Variable(CType aNode)
        {
            VariableCType variableType = aNode as VariableCType;
            return variableType != null ? variableType.Name : null;
        }

        public bool NodeMatch(CType aPatternNode, CType aTreeNode)
        {
            if (aPatternNode == null && aTreeNode == null) return true;
            if (aPatternNode == null || aTreeNode == null) return false;
            VariableCType variableType = aPatternNode as VariableCType;
            if (variableType != null)
            {
                if (variableType.CType == null) return true;
                return NodeMatch(variableType.CType, aTreeNode);
            }
            if (aPatternNode.GetType() != aTreeNode.GetType())
            {
                return false;
            }
            NamedCType namedPatternType = aPatternNode as NamedCType;
            NamedCType namedTreeType = aTreeNode as NamedCType;
            if (namedPatternType != null)
            {
                Debug.Assert(namedTreeType != null); // Cannot be null as we already checked they have the same type.
                if (namedPatternType.Name != namedTreeType.Name)
                {
                    return false;
                }
            }
            return true;
        }
    }

    class CTypeFactory
    {
        public CType Ptr(CType aBaseType) { return new PointerCType(aBaseType); }
        public CType Type(string aTypeName) { return new NamedCType(aTypeName); }
        public CType Array(int aDimension, CType aBaseType) { return new ArrayCType(aDimension, aBaseType); }
        public CType Array(CType aBaseType) { return new ArrayCType(null, aBaseType); }
        public CType Variable(string aVariableName) { return new VariableCType(aVariableName); }
        public CType Pair(CType aFirst, CType aSecond) { return new TupleCType(aFirst, aSecond); }
        public CType Tuple(params CType[] aItems) { return new TupleCType(aItems); }
    }

    static class ExtensionMethods
    {
        public static PatternMatcher<CType, CType> MatchToPattern(this CType aCType, CType aPattern)
        {
            PatternMatcher<CType, CType> matcher = new PatternMatcher<CType, CType>(new CTypeTreeWalker(), aCType);
            matcher.Match(aPattern);
            return matcher;
        }
    }

    class PatternMatcher<TPatternNode, TTreeNode>
    {
        readonly IPatternTreeWalker<TPatternNode, TTreeNode> iWalker;
        TTreeNode Value { get; set; }
        public Dictionary<string, TTreeNode> BoundVariables { get; private set; }
        public bool IsMatch { get; set; }
        public PatternMatcher(IPatternTreeWalker<TPatternNode, TTreeNode> aWalker, TTreeNode aValue)
        {
            iWalker = aWalker;
            Value = aValue;
        }

        public bool Match(TPatternNode aPattern)
        {
            Dictionary<string, TTreeNode> boundVariables;
            if (TryMatch(aPattern, Value, out boundVariables))
            {
                BoundVariables = boundVariables;
                return IsMatch = true;
            }
            BoundVariables = null;
            return IsMatch = false;
        }

        public int FirstMatch(params TPatternNode[] aPatterns)
        {
            for (int i = 0; i != aPatterns.Length; ++i)
            {
                if (Match(aPatterns[i])) return i;
            }
            return -1;
        }

        bool TryMatch(TPatternNode aPattern, TTreeNode aTree, out Dictionary<string, TTreeNode> aBoundVariables)
        {
            //Dictionary<string, TTreeNode> boundVariables;
            string variable = iWalker.Variable(aPattern);
            if (!iWalker.NodeMatch(aPattern, aTree))
            {
                aBoundVariables = null;
                return false;
            }
            var patternChildren = iWalker.PatternChildren(aPattern).ToList();
            var treeChildren = iWalker.TreeChildren(aTree).ToList();
            if (patternChildren.Count != treeChildren.Count)
            {
                aBoundVariables = null;
                return false;
            }
            var boundVariables = new Dictionary<string, TTreeNode>();
            if (variable != null)
            {
                boundVariables.Add(variable, aTree);
            }
            for (int i = 0; i != patternChildren.Count; ++i)
            {
                Dictionary<string, TTreeNode> recursiveVariables;
                bool recursiveResult = TryMatch(patternChildren[i], treeChildren[i], out recursiveVariables);
                if (!recursiveResult)
                {
                    aBoundVariables = null;
                    return false;
                }
                foreach (var kvp in recursiveVariables)
                {
                    boundVariables[kvp.Key] = kvp.Value;
                }
            }
            aBoundVariables = boundVariables;
            return true;
        }
    }


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
            return aNativeTypeName;
        }

        class PinvokeArgumentMapping
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
        }

        class ManagedArgumentMapping
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
        }

        /*
        IEnumerable<ManagedArgumentMapping> GetArgumentMarshalTypes(List<Declaration> aNativeArguments, CType aReturnType)
        {
            List<IArgumentTransformer> transformers = new List<IArgumentTransformer>{
                new StringReturnTransformer(),
                new StringArgumentTransformer(),
                new RefArgumentTransformer(),
                new TrivialArgumentTransformer(),
            };
            var treeWalker = new CTypeTreeWalker();
            Func<CType,PatternMatcher<CType, CType>> match = pattern =>
                new PatternMatcher<CType,CType>(
                    treeWalker,
                    pattern);
            var f = new CTypeFactory();
            for (int i = 0; i != aNativeArguments.Count; ++i)
            {
                Declaration currentArg = aNativeArguments[i];
                Declaration nextArg = i + 1 < aNativeArguments.Count ? aNativeArguments[i + 1] : null;

                foreach (var transformer in transformers)
                {
                    if (transformer.CanApply(currentArg, nextArg, aReturnType))
                    {
                        transformer.Apply(currentArg, nextArg, aReturnType, aAssembler);
                    }
                }
                CType argType = currentArg.CType;
                var pointerType = argType as PointerCType;
                var namedType = argType as NamedCType;
                var arrayType = argType as ArrayCType;

                Func<??,string, ManagedArgumentMapping> makeTrivialMapping = (x,y) => 
                    {

                    };
                Dictionary<string, CType> boundVariables;
                var matcher = match(
                    f.Pair(
                        currentArg.CType,
                        nextArg == null ? null : nextArg.CType));
                if (matcher.Match(
                    f.Pair(
                        f.Ptr(f.Variable("T")),
                        f.Type("int")
                    )))
                {
                    Debug.Assert(nextArg != null); // It matched ("int") so it can't be null.

                    // Pointer to T, followed by int.
                    if (nextArg.Name == "num_" + currentArg.Name)
                    {
                        // Array-pair.
                        CType nativeElementType = matcher.BoundVariables["T"];
                        var elementMatcher = match(nativeElementType);
                        if (elementMatcher.Match(f.Ptr(f.Type("int"))))
                        {
                            // Array of pointers to int.
                            
                        }
                        else if (elementMatcher.Match(f.Ptr(f.Ptr(f.Variable("T2", new NamedType)))))
                        {
                            // Array of pointers to pointers to T2.
                        }
                        
                        CSharpType 
                        yield return new ManagedArgumentMapping(
                            currentArg.Name,
                            new CSharpType(""),
                            new PinvokeArgumentMapping(
                                currentArg,
                                currentArg.Name,
                                new CSharpType("IntPtr")),
                            new PinvokeArgumentMapping(
                                nextArg,
                                nextArg.Name,
                                new CSharpType("int")));
                            

                        i += 1;
                        continue;
                    }
                    // Fall through and look for a different match.
                }

                
                else if (matcher.Match(f.Ptr(f.Type(new PointerCType(new NamedCType(""))))
                else if (matcher.Match(f.Ptr(f.Variable("T"))))
                {
                    // T*.

                }
                if (pointerType != null)
                {
                    var functionType = pointerType.BaseType as FunctionCType;
                    if (functionType != null)
                    {
                        // Delegate
                        // TODO: Derive delegate name from declaration name?
                        yield return new ManagedArgumentMapping
                        {
                            ManagedName = currentArg.Name,
                            ManagedType = new CSharpType(currentArg.Name)
                        };

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
                        if (typeName == "size_t") return new CSharpType("IntPtr");
                        return new CSharpType("/* ??? / IntPtr"); // TODO: Out of band warning
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
                            return new CSharpType("UIntPtr");
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
                    case "size_t":
                        return new CSharpType("UIntPtr");
                    case "sp_uint64":
                        return new CSharpType("UIntPtr");
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

        public string GenerateCSharpWrappingMethod(string aIndent, string aFunctionName, string aHandleName, FunctionCType aFunctionType)
        {
            if (!aFunctionName.StartsWith(aHandleName+"_"))
            {
                return aIndent + "// Bad method " + aFunctionName + "\n";
            }
            var methodName = PascalCase(SplitName(aFunctionName.Substring(aHandleName.Length+1)));
            

            var enumNames = new Dictionary<string, string> { { "sp_error", "SpotifyError" } }; // FIXME

            var transformers = new List<IArgumentTransformer>{
                new ThisPointerArgumentTransformer{HandleType = aHandleName},
                new StringReturnTransformer(),
                new StringArgumentTransformer(),
                new TrivialArgumentTransformer(),
                new RefArgumentTransformer(),
                new TrivialReturnTransformer(enumNames),
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
            while (nativeFunction.CurrentParameter != null || !assembler.HasReturnType)
            {
                var transformer = transformers.FirstOrDefault(x=>x.Apply(nativeFunction, assembler));
                if (transformer == null)
                {
                    return aIndent + String.Format("// Skipped function '{0}'.\n", aFunctionName);
                }
                assembler.NextArgument();
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

        string DefaultManagedEnumName(string aNativeName)
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
                        attribute = aIndent + SingleIndent + attribute + "\n";
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