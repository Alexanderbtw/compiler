using System;
using System.IO;

using Antlr4.Runtime;

namespace Compiler.Frontend;

public class Program
{
    private static void Main(string[] args)
    {
        var program = ReadAllInput("main.minl");
        Try(program);
    }

    private static void Try(string input)
    {
        var str = new AntlrInputStream(input);
        Console.WriteLine(input);
        var lexer = new MiniLangLexer(str);
        var tokens = new CommonTokenStream(lexer);
        var parser = new MiniLangParser(tokens);
        var listenerLexer = new ErrorListener<int>();
        var listenerParser = new ErrorListener<IToken>();
        lexer.AddErrorListener(listenerLexer);
        parser.AddErrorListener(listenerParser);
        MiniLangParser.ProgramContext tree = parser.program();
        if (listenerLexer.HadError || listenerParser.HadError)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("error in parse");
        }

        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("parse completed");
        }
    }

    private static string ReadAllInput(string fn)
    {
        string input = File.ReadAllText(fn);
        return input;
    }
}
