using ApiParser;

namespace ManagedApiBuilder.ArgumentTransformers
{
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
}