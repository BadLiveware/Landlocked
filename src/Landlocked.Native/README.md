# Landlocked.Native

Direct .NET bindings for the Linux Landlock userspace ABI on x86-64 and ARM64.

The exported syscall numbers are specific to those supported architectures. This package is for callers that need
syscall-level control. Methods expose kernel-shaped arguments and return values,
do not own file descriptors, and do not coordinate Landlock domains across CLR threads. Read `errno` with
`Marshal.GetLastPInvokeError()` immediately after a failing call.

Most applications should use `Landlocked`; use `Landlocked.LowLevel` when kernel concepts are desirable but safe
resource ownership and explicit C# results are still required.
