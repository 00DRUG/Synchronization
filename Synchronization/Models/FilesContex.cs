
namespace Synchronization.Models;

private static string EnsureTrailingSlash(string path)
{
    path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
}
