# Landlocked

Landlocked is a dependency-free .NET library for progressively restricting a Linux process with
[Landlock](https://docs.kernel.org/userspace-api/landlock.html). Each policy layer is applied atomically to every
existing CLR thread. When Landlocked is the process's sole Landlock policy manager, successive calls can only
reduce effective access.

## Requirements

- Linux 7.0 or newer with Landlock enabled (Landlock ABI 8 provides `LANDLOCK_RESTRICT_SELF_TSYNC`)
- x86-64 or ARM64
- .NET 8 or newer

The library refuses to enforce a policy when process-wide thread synchronization is unavailable. It never silently
falls back to restricting only the calling thread.

## Use

```csharp
using Landlocked;

var support = Landlock.GetSupport();
if (!support.IsAvailable)
{
    throw new PlatformNotSupportedException(support.Reason);
}

var workspace = Path.GetFullPath(".");
var workspacePolicy = LandlockPolicy
    .Handle(FileSystemAccess.ContentAndHierarchyMutation)
    .Allow(workspace, FileSystemAccess.ContentAndHierarchyMutation);

Landlock.Restrict(workspacePolicy);
```

After `Restrict` returns, new file-content and hierarchy mutations are allowed beneath `workspace` and denied
elsewhere. Reads remain unaffected because the policy handles only mutation rights.

Apply another layer when the process needs fewer capabilities:

```csharp
var outputDirectory = Path.Combine(workspace, "artifacts");
var outputOnlyPolicy = LandlockPolicy
    .Handle(FileSystemAccess.ContentAndHierarchyMutation)
    .Allow(outputDirectory, FileSystemAccess.ContentAndHierarchyMutation);

Landlock.Restrict(outputOnlyPolicy);
```

Landlock intersects the new layer with the caller's previously applied layers. Landlocked serializes concurrent
calls before applying them process-wide, so a later Landlocked call cannot restore access removed by an earlier
Landlocked call. Policies are immutable: `Allow` returns a new policy instead of modifying the original.

To deny all new operations for a handled access set, apply a policy without path rules:

```csharp
Landlock.Restrict(
    LandlockPolicy.Handle(FileSystemAccess.ContentAndHierarchyMutation));
```

## Support and failures

`Landlock.GetSupport()` reports the detected ABI, supported filesystem rights, availability state, and native error
when relevant. `Landlock.Restrict` throws:

- `PlatformNotSupportedException` when Linux, the architecture, ABI 8 synchronization, or a requested access right
  is unavailable.
- `LandlockException` when a native policy operation fails. It includes the operation and Linux `errno`.
- Argument exceptions when a policy is empty, contains unknown rights, or allows rights it does not handle.

A failed operation does not install that policy layer. The irreversible `no_new_privs` prerequisite and final TSYNC
call run on a disposable enforcement thread: on success TSYNC propagates both process-wide; on failure the helper
thread exits without leaving the original caller partially restricted. Successful restrictions are irreversible and
inherited by child threads and processes.

## Security boundaries

Landlock mediates new access; it does not revoke capabilities already represented by open resources:

- A file descriptor opened before restriction keeps the access established when it was opened. Close obsolete file,
  directory, socket, and device descriptors before tightening a phase.
- Existing network connections remain usable. This version exposes filesystem policies only.
- Policy roots may not contain symbolic links. Landlocked resolves them with `openat2(RESOLVE_NO_SYMLINKS)` and
  fails the policy instead of silently following a link to another hierarchy.
- Rules follow filesystem objects and the existing mount topology, not purely lexical paths. A bind mount beneath
  an allowed root and another bind alias of that hierarchy can carry the same access. Finalize and trust the mount
  namespace before restriction; `RESOLVE_NO_XDEV` cannot eliminate descendant or external aliases and would reject
  useful roots reached through ordinary mount points. OverlayFS layers are independent Landlock hierarchies.
- Do not mix Landlocked with code that directly installs per-thread Landlock domains. ABI 8 TSYNC intentionally
  replaces sibling threads' domains; a less-restricted caller could therefore broaden an independently restricted
  sibling. Process-wide monotonicity requires all policy changes to go through Landlocked.
- Current Landlock ABIs do not mediate every metadata operation. In particular, Linux documents gaps including
  `chmod`, `chown`, extended attributes, timestamps, `flock`, and several `fcntl` operations.
- `ContentAndHierarchyMutation` therefore names the rights Landlock can mediate for file contents and directory-tree
  changes; it is not a claim that every possible filesystem side effect is blocked.
- Landlock complements normal Unix permissions, namespaces, seccomp, and mandatory access-control systems; it does
  not replace them.

## Build and test

```bash
dotnet build Landlocked.slnx
dotnet test Landlocked.slnx
dotnet pack src/Landlocked/Landlocked.csproj -c Release -o artifacts
```

Kernel enforcement tests run in subprocesses because a Landlock domain cannot be removed from a test process. They
cover recursive write containment, progressive and concurrent tightening, failed application, symbolic-link root
rejection, pre-opened descriptors, and TSYNC application to CLR threads created before restriction.
