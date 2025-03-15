
namespace Synchronization.Models;
class FilesContext
{
    public DirectoryInfo SourceDirectory { get; set; }
    public DirectoryInfo TargetDirectory { get; set; }
    public string SourcePath { get; set; }
    public string TargetPath { get; set; }
    public FilesContext(string sourcePath, string targetPath)
    { 
        SourcePath = EnsureTrailingSlash(sourcePath);
        TargetPath = EnsureTrailingSlash(targetPath);
        SourceDirectory = new DirectoryInfo(this.SourcePath);
        TargetDirectory = new DirectoryInfo(this.TargetPath);
    }
    private static string EnsureTrailingSlash(string path)
    {
        var fullpath = Path.GetFullPath(path);
        return fullpath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? fullpath : fullpath + Path.DirectorySeparatorChar;
    }
}


