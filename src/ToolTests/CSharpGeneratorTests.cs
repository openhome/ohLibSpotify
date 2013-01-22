using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ApiParser;
using ManagedApiBuilder;
using NUnit.Framework;

namespace ToolTests
{
    public abstract class CSharpGeneratorContext
    {
        protected CSharpGenerator iGenerator;

        [SetUp]
        public void SetUp()
        {
            iGenerator = new CSharpGenerator(
                    new[]{"sp_color", "sp_flavor", "sp_speed"},
                    new[]{"sp_person", "sp_room", "sp_item"},
                    new[]{"sp_file", "sp_device"},
                    new[]{"sp_ready_cb", "sp_finished_cb"},
                    new[]{
                        new ApiStructConfiguration{
                            NativeName="sp_person",
                            ManagedName="SpotifyPerson",
                            ForcePublic=false,
                            SuppressFunctions={"sp_person_difficult_function"}
                        }
                    },
                    new[]{
                        new ApiEnumConfiguration{
                            NativeName = "sp_speed",
                            ManagedName = "SpotifySpeed",
                            NativeConstantPrefix = "SP_SPEED_",
                            ManagedConstantPrefix = "_"
                        },
                        new ApiEnumConfiguration{
                            NativeName = "sp_flavor",
                            ManagedName = "Flavor",
                            NativeConstantPrefix = "SP_FLAVOR_",
                            ManagedConstantPrefix = ""
                        },
                    }
                );
        }
    }

    public class WhenGeneratingAnEmptyStruct : CSharpGeneratorContext
    {
        protected string iResult;

        [SetUp]
        public void GenerateStruct()
        {
            iResult = iGenerator.GenerateStruct("", "sp_room", new StructCType("room_tag"));
        }

        [Test]
        public void TheResultShouldNotBePublic()
        {
            Assert.That(iResult, Is.Not.StringContaining("public"));
        }

        [Test]
        public void TheResultShouldKeepTheSameName()
        {
            Assert.That(iResult, Is.StringContaining("sp_room"));
        }

        [Test]
        public void TheResultShouldHaveNoContents()
        {
            Assert.That(iResult, Is.StringMatching(@"(?s).*\{\s*\}"));
        }
    }

    public class WhenGeneratingAStructWithSomeMembers : CSharpGeneratorContext
    {
        protected string iResult;

        [SetUp]
        public void GenerateStruct()
        {
            iResult = iGenerator.GenerateStruct("", "sp_room",
                new StructCType("room_tag")
                {
                    Fields = {
                        new Declaration{Name="height", CType=new NamedCType("int")},
                        new Declaration{Name="name", CType=new PointerCType(new NamedCType("char"){Qualifiers={"const"}})},
                        new Declaration{Name="capacity", CType=new NamedCType("size_t")},
                        new Declaration{Name="lit", CType=new NamedCType("bool")},
                        new Declaration{Name="blob", CType=new PointerCType(new NamedCType("void"))},
                    }
                });
        }

        [Test]
        public void TheFieldsShouldBeDeclaredInOrder()
        {
            const string anything = ".*";
            Assert.That(iResult, Is.StringMatching(
                "(?s)" +
                anything + "height;" +
                anything + "name;" +
                anything + "capacity;" +
                anything + "lit;" +
                anything + "blob;" +
                anything));
        }

        [Test]
        public void AnIntNativeField_ShouldBe_AnIntManagedField()
        {
            const string anything = ".*";
            Assert.That(iResult, Is.StringMatching(
                "(?s)" + anything + "public int @height;" + anything));
        }

        [Test]
        public void AConstCharStarNativeField_ShouldBe_AnIntPtrManagedField()
        {
            const string anything = ".*";
            Assert.That(iResult, Is.StringMatching(
                "(?s)" + anything + "public IntPtr @name;" + anything));
        }

        [Test]
        public void ASizeTNativeField_ShouldBe_AUIntPtrManagedField()
        {
            const string anything = ".*";
            Assert.That(iResult, Is.StringMatching(
                "(?s)" + anything + "public UIntPtr @capacity;" + anything));
        }

        [Test]
        public void ABoolNativeField_ShouldBe_ABoolManagedFieldWithByteMarshalling()
        {
            const string anything = ".*";
            Func<string, string> esc = x=>x.Replace(".", "\\.").Replace("(", "\\(").Replace(")", "\\)").Replace("[", "\\[").Replace("]","\\]");
            Assert.That(iResult, Is.StringMatching(
                "(?s)" + anything + esc("[MarshalAs(UnmanagedType.I1)]") + @"\s*" + "public bool @lit;" + anything));
        }
    }
    public class WhenGeneratingAnEnumWithSomeConstants : CSharpGeneratorContext
    {
        protected string iResult;

        [SetUp]
        public void GenerateStruct()
        {
            iResult = iGenerator.GenerateEnumDeclaration("", "sp_flavor",
                new EnumCType("flavor_tag")
                {
                    Constants = {
                        new EnumConstant{Name="SP_FLAVOR_MINT", Value=6},
                        new EnumConstant{Name="SP_FLAVOR_STRAWBERRY_AND_ONION", Value=34},
                        new EnumConstant{Name="SP_FLAVOR_BURNT_POTATO", Value=77},
                        new EnumConstant{Name="SP_FLAVOR_TARMAC", Value=4},
                    }
                });
        }

        [Test]
        public void TheEnumShouldBePublic()
        {
            Assert.That(iResult, Is.StringMatching(@"(?s)\s*public.*"));
        }

        [Test]
        public void TheEnumShouldHaveTheCorrectFlavor()
        {
            Assert.That(iResult, Is.StringMatching(@"(?s)\s*public.*"));
        }

        [Test]
        public void TheEnumShouldHaveTheConstantsInOrder()
        {
            Assert.That(iResult, Is.StringMatching(@"(?s).*\{.*Mint.*StrawberryAndOnion.*BurntPotato.*Tarmac.*\}"));
        }

        [TestCase("Mint", "6")]
        [TestCase("StrawberryAndOnion", "34")]
        [TestCase("BurntPotato", "77")]
        [TestCase("Tarmac", "4")]
        public void TheEnumShouldHaveTheCorrectConstantValues(string aName, string aValue)
        {
            const string singleLineMode = "(?s)";
            const string sp = @"\s*";
            const string any = @"\.*";
            Action<string> chk = s => Assert.That(iResult, Is.StringMatching(s));
            chk(singleLineMode + @".*" + aName + sp + "=" + sp + aValue +  sp + "," + any);
        }
    }
}
