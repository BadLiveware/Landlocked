namespace Landlocked;

[Flags]
public enum FileSystemAccess : ulong
{
    None = 0,
    Execute = 1UL << 0,
    WriteFile = 1UL << 1,
    ReadFile = 1UL << 2,
    ReadDirectory = 1UL << 3,
    RemoveDirectory = 1UL << 4,
    RemoveFile = 1UL << 5,
    MakeCharacterDevice = 1UL << 6,
    MakeDirectory = 1UL << 7,
    MakeRegularFile = 1UL << 8,
    MakeUnixSocket = 1UL << 9,
    MakeFifo = 1UL << 10,
    MakeBlockDevice = 1UL << 11,
    MakeSymbolicLink = 1UL << 12,
    Refer = 1UL << 13,
    Truncate = 1UL << 14,
    IoctlDevice = 1UL << 15,
    ResolveUnixSocket = 1UL << 16,

    Read = ReadFile | ReadDirectory,
    Create = MakeCharacterDevice | MakeDirectory | MakeRegularFile | MakeUnixSocket |
             MakeFifo | MakeBlockDevice | MakeSymbolicLink,
    Remove = RemoveDirectory | RemoveFile,
    ContentAndHierarchyMutation = WriteFile | Remove | Create | Refer | Truncate,
    AllKnown = Execute | Read | ContentAndHierarchyMutation | IoctlDevice | ResolveUnixSocket,
}
