namespace Landlocked;

public readonly record struct PathAccessRule
{
    internal PathAccessRule(string path, FileSystemAccess allowedAccess)
    {
        Path = path;
        AllowedAccess = allowedAccess;
    }

    public string Path { get; }

    public FileSystemAccess AllowedAccess { get; }
}
