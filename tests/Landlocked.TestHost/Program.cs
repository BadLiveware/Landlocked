using Landlocked;

if (args.Length == 0)
{
    Console.Error.WriteLine("A scenario name is required.");
    return 2;
}

var support = Landlock.GetSupport();
if (!support.IsAvailable)
{
    Console.Error.WriteLine(support.Reason);
    return 77;
}

try
{
    switch (args[0])
    {
        case "write-boundary":
            RunWriteBoundary(args[1], args[2]);
            break;
        case "progressive":
            RunProgressiveRestriction(args[1], args[2], args[3], args[4]);
            break;
        case "existing-descriptor":
            RunExistingDescriptor(args[1]);
            break;
        case "thread-sync":
            RunThreadSynchronization(args[1]);
            break;
        case "failed-apply":
            RunFailedApplication(args[1], args[2]);
            break;
        case "symlink-rule":
            RunSymbolicLinkRule(args[1], args[2]);
            break;
        case "concurrent-layers":
            RunConcurrentLayers(args[1], args[2], args[3], args[4]);
            break;
        default:
            Console.Error.WriteLine($"Unknown scenario: {args[0]}");
            return 2;
    }

    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

static void RunWriteBoundary(string writableDirectory, string readOnlyFile)
{
    var policy = LandlockPolicy
        .Handle(FileSystemAccess.ContentAndHierarchyMutation)
        .Allow(writableDirectory, FileSystemAccess.ContentAndHierarchyMutation);

    Landlock.Restrict(policy);

    File.WriteAllText(Path.Combine(writableDirectory, "created.txt"), "inside");
    ExpectAccessDenied(() => File.AppendAllText(readOnlyFile, "outside"));
}

static void RunProgressiveRestriction(
    string broadDirectory,
    string narrowDirectory,
    string broadFile,
    string narrowFile)
{
    var broadPolicy = LandlockPolicy
        .Handle(FileSystemAccess.ContentAndHierarchyMutation)
        .Allow(broadDirectory, FileSystemAccess.ContentAndHierarchyMutation);

    Landlock.Restrict(broadPolicy);
    File.AppendAllText(broadFile, "broad-1\n");
    File.AppendAllText(narrowFile, "narrow-1\n");

    var narrowPolicy = LandlockPolicy
        .Handle(FileSystemAccess.ContentAndHierarchyMutation)
        .Allow(narrowDirectory, FileSystemAccess.ContentAndHierarchyMutation);

    Landlock.Restrict(narrowPolicy);
    ExpectAccessDenied(() => File.AppendAllText(broadFile, "broad-2\n"));
    File.AppendAllText(narrowFile, "narrow-2\n");
}

static void RunExistingDescriptor(string file)
{
    using var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
    Landlock.Restrict(LandlockPolicy.Handle(FileSystemAccess.ContentAndHierarchyMutation));

    using (var writer = new StreamWriter(stream, leaveOpen: true))
    {
        writer.Write("existing-descriptor\n");
        writer.Flush();
    }

    ExpectAccessDenied(() => File.AppendAllText(file, "new-descriptor\n"));
}

static void RunThreadSynchronization(string file)
{
    using var attemptWrite = new ManualResetEventSlim();
    Exception? workerFailure = null;
    var denied = false;

    var worker = new Thread(() =>
    {
        attemptWrite.Wait();
        try
        {
            File.AppendAllText(file, "worker-write\n");
        }
        catch (UnauthorizedAccessException)
        {
            denied = true;
        }
        catch (Exception exception)
        {
            workerFailure = exception;
        }
    });

    worker.Start();
    Landlock.Restrict(LandlockPolicy.Handle(FileSystemAccess.ContentAndHierarchyMutation));
    attemptWrite.Set();
    worker.Join();

    if (workerFailure is not null)
    {
        throw new InvalidOperationException("The worker failed unexpectedly.", workerFailure);
    }

    if (!denied)
    {
        throw new InvalidOperationException("The pre-existing worker thread retained write access.");
    }
}

static void RunFailedApplication(string missingPath, string writableFile)
{
    var policy = LandlockPolicy
        .Handle(FileSystemAccess.ContentAndHierarchyMutation)
        .Allow(missingPath, FileSystemAccess.ContentAndHierarchyMutation);

    try
    {
        Landlock.Restrict(policy);
        throw new InvalidOperationException("The invalid policy unexpectedly succeeded.");
    }
    catch (LandlockException)
    {
    }

    File.AppendAllText(writableFile, "still-unrestricted\n");
}

static void RunSymbolicLinkRule(string symbolicLink, string writableFile)
{
    var policy = LandlockPolicy
        .Handle(FileSystemAccess.ContentAndHierarchyMutation)
        .Allow(symbolicLink, FileSystemAccess.ContentAndHierarchyMutation);

    try
    {
        Landlock.Restrict(policy);
        throw new InvalidOperationException("The symbolic-link policy unexpectedly succeeded.");
    }
    catch (LandlockException)
    {
    }

    File.AppendAllText(writableFile, "symlink-rejected\n");
}

static void RunConcurrentLayers(string firstDirectory, string secondDirectory, string firstFile, string secondFile)
{
    using var start = new ManualResetEventSlim();
    Exception? firstFailure = null;
    Exception? secondFailure = null;

    var firstThread = new Thread(() =>
    {
        start.Wait();
        try
        {
            var policy = LandlockPolicy
                .Handle(FileSystemAccess.ContentAndHierarchyMutation)
                .Allow(firstDirectory, FileSystemAccess.ContentAndHierarchyMutation);
            Landlock.Restrict(policy);
        }
        catch (Exception exception)
        {
            firstFailure = exception;
        }
    });

    var secondThread = new Thread(() =>
    {
        start.Wait();
        try
        {
            var policy = LandlockPolicy
                .Handle(FileSystemAccess.ContentAndHierarchyMutation)
                .Allow(secondDirectory, FileSystemAccess.ContentAndHierarchyMutation);
            Landlock.Restrict(policy);
        }
        catch (Exception exception)
        {
            secondFailure = exception;
        }
    });

    firstThread.Start();
    secondThread.Start();
    start.Set();
    firstThread.Join();
    secondThread.Join();

    if (firstFailure is not null || secondFailure is not null)
    {
        throw new AggregateException(
            "A concurrent restriction failed.",
            new[] { firstFailure, secondFailure }.OfType<Exception>());
    }

    ExpectAccessDenied(() => File.AppendAllText(firstFile, "first\n"));
    ExpectAccessDenied(() => File.AppendAllText(secondFile, "second\n"));
}

static void ExpectAccessDenied(Action action)
{
    try
    {
        action();
    }
    catch (UnauthorizedAccessException)
    {
        return;
    }

    throw new InvalidOperationException("The operation unexpectedly succeeded.");
}
