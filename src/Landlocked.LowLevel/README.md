# Landlocked.LowLevel

Resource-safe, idiomatic .NET access to Linux Landlock rulesets on x86-64 and ARM64.

The package uses the Linux syscall numbering shared by those architectures and rejects other process architectures.
`LandlockApi` captures `errno`, retries interrupted path opens, and returns owned `LandlockRuleset` handles. It stays
close to kernel concepts: callers choose typed filesystem and network access masks, validate ABI compatibility, order
irreversible operations, and decide whether to restrict the calling thread or synchronize all threads.

Use the `Landlocked` package for immutable policies, capability claims, compatibility validation, serialized
process-wide enforcement, and monotonic tightening. Do not mix low-level restriction calls with that package in the
same process: independent Landlock domain management can invalidate its process-wide guarantees.
