using CommandLine;
using Synchronization.Models;

namespace Synchronization.Utils;

public static class ParserUtils
{
    public static InputParameters? ParserConsoleArguments(string[] args)
    {
        var parserResult = Parser.Default.ParseArguments<InputParameters>(args);
        if (parserResult.Errors.Any())
        {
            return null;
        }

        return parserResult.Value;
    }
}