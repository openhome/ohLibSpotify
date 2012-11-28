using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ApiParser
{
    class Program
    {
        static void Main(string[] args)
        {
            var text = File.ReadAllText(args[0]);
            var tokenStream = CHeaderLexer.Lex(text);
            var parser = new HeaderParser(tokenStream);
            var serializer = JsonSerializer.Create(new JsonSerializerSettings{Formatting=Formatting.Indented});
            serializer.Serialize(Console.Out, parser.ParseHeader());

            //Console.WriteLine(JsonConvert.SerializeObject(parser.ParseHeader(), new JsonSerializerSettings{Formatting=Formatting.Indented])));
            /*foreach (var decl in parser.ParseHeader())
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(decl, new JsonSerializerSettings{Formatting = Formatting.Indented});
                Console.WriteLine(json);
                var obj = JsonConvert.DeserializeObject<Declaration>(json, new CTypeConverter());
                var json2 = JsonConvert.SerializeObject(obj, new JsonSerializerSettings { Formatting = Formatting.Indented });
                if (json != json2)
                {
                    Console.WriteLine(json2);
                    throw new Exception("Roundtrip failed!");
                }
                //Console.WriteLine(decl);
            }*/

            //{
            //    Console.WriteLine("({0}, \"{1}\")", token.Type, token.Content.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t"));
            //}
        }
    }
}
