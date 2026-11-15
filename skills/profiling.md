# Profiling

How to find and fix slow C# in this plugin. It covers CPU, memory, allocations, locks, and file reads and writes. Every other language is out of scope.

## When to Profile

Profile only when there is a reason. Never profile to look for something to improve.

Start when one of these is true.

* A user or a test reports that something is slow, uses too much memory, or delays sign in or playback.
* A change adds or alters a loop or a lookup on a path that runs for every user or every request, such as applying groups, the scheduled tasks, sign in through the authentication provider, or the anonymous invite routes.
* A number in a test or a task run grew from one release to the next.

Without one of these, do not profile. Code that has no symptom and no change is not a problem.

## The Tiers

Profiling has three tiers. Start at Tier 1. Go up a tier only when the one below could not answer the question. Most issues end at Tier 1 or Tier 2.

| Tier | Answers | Budget | Needs a test server |
| :--- | :--- | :--- | :--- |
| 1. Time One Method | Is this code slow by itself | 10 minutes | No |
| 2. Trace One Task | Which code dominates one slow task or route | 30 minutes | Yes |
| 3. Go Deeper | Why memory grows, or what a second path costs | Ask first | Yes |

* **Tier 1** is a temporary timing test over `TestSupport` and `FakeUserServer`. It needs no server.
* **Tier 2** is one CPU trace and counters of one task or route on a test server. Pick the one path the issue points at, and seed the server once with the [recipe](#seeding-a-test-server).
* **Tier 3** is a heap dump, a larger seed, or a second path. Stop and ask before you start it.

### The Budget

* Each tier has a time budget. When it runs out, stop and report what you have, even when it is inconclusive.
* Profile one path per issue. Never sweep every path "to be sure".
* Stop as soon as the goal is met or the cause is proven.
* Write the tier, the path, and the stop reason in the report.

## The Rule

Measure first, change second, measure again. A change made for speed is not done until a number shows it is faster and the tests still pass.

* Never optimize from a guess. A hot path you cannot show is not a hot path.
* Never trade correctness, a safety limit, or a [security](code.md#security) rule for speed.
* Keep the simplest code that meets the goal. Stop when the goal is met.

## Goals

State the goal before you measure. A goal names the operation, the data size, and the limit.

| Good | Bad |
| :--- | :--- |
| **Apply group permissions** over 2,000 members finishes in under 10 seconds | Make apply faster |
| `GET /Invite/{token}/Info` answers 50 requests a second under 50 MB | Reduce memory |

The plugin runs inside Jellyfin on the user's server. Treat its CPU, memory, and disk as shared. A task that is fast but delays sign in or playback has failed.

## Check These First

These causes are common. Check each against the code before you measure anything.

| Check | Usual cause |
| :--- | :--- |
| A loop that asks Jellyfin the same question for every user, such as reading the config or every user's policy again | The answer does not change inside a run, so ask once per run |
| A policy or configuration written back for a member whose values already match | The write and its events cost the same as a real change |
| The invite status file read from disk on every request | The cache keyed on the file's write time is bypassed |
| A config save inside a loop | Each save rewrites the whole XML, so save once after the loop |
| A log string built in a hot loop | The string is built even when the level is off |

If the code shows the cause, a Tier 1 test is enough to prove it.

## Seeding a Test Server

Tier 2 needs data that is big enough to show the problem and no bigger.

* Use a test server, never one real users depend on, since applying a group changes its members.
* Seed once with users and groups at the size the goal names, such as 2,000 users in five groups. Never reseed between runs.
* Write down the data size in the report.
* Never build a bigger seed to prove a scaling claim. Do that at Tier 3, after asking.

## Tools

Use only the tools Microsoft ships for .NET. They need the .NET 10 SDK, the same as the repo, so check it first.

```bash
dotnet --list-sdks
```

Install the tools once. Update them with `dotnet tool update --global` and the same name.

```bash
dotnet tool install --global dotnet-trace
```

```bash
dotnet tool install --global dotnet-counters
```

```bash
dotnet tool install --global dotnet-gcdump
```

The tools install to `~/.dotnet/tools`. On macOS that folder can be missing from `PATH`, because the dotnet installer writes the literal text `~/.dotnet/tools` to `/etc/paths.d`, and a shell does not expand it. Add this line to `~/.zshrc` and open a new shell.

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
```

Check that each tool runs.

```bash
dotnet-trace --version
```

| Question | Tool |
| :--- | :--- |
| Which methods use the CPU | `dotnet-trace` |
| Is the process healthy over time | `dotnet-counters` |
| What holds the memory | `dotnet-gcdump` |
| How fast is this one method | A temporary test with `Stopwatch` and `GC.GetAllocatedBytesForCurrentThread()` |
| How long did this run take | A `Stopwatch` in a temporary test, or `dotnet test` timing |

A trace is a `.nettrace` file, which `dotnet-trace` writes by default. Open it in PerfView on Windows, or turn it into a speedscope file and open that at [speedscope.app](https://www.speedscope.app). Pass `--format Speedscope` to `collect` to get both files in one step, or convert a trace later.

```bash
dotnet-trace convert run.nettrace --format Speedscope -o run
```

This writes `run.speedscope.json`. Keep every trace, dump, and test output outside the repository.

## Tier 1: Time One Method

A method with no I/O is best measured alone.

* Write a temporary xUnit test in the test project that times the method with `Stopwatch` and reads `GC.GetAllocatedBytesForCurrentThread()` before and after. Take the difference on the same thread, since the counter covers only the current thread. Write the numbers with `ITestOutputHelper`, and read them with `--logger "console;verbosity=detailed"`. Never commit the test.
* Build the services over `TestSupport`, and the users over `FakeUserServer`, at the size the goal names.
* Build and run in `Release`, with `dotnet test -c Release`, and without a debugger.
* Warm up first. Call the method a few times before you measure, so the JIT is not in the number.
* Run it many times and take the median. One run proves nothing.
* Run it before and after the change, on the same machine, with nothing else busy.

To see where the time goes inside the method, trace the test host. Make the test loop for at least 10 seconds, so a trace has time to attach. Run the tests in one shell and attach from another.

```bash
dotnet test -c Release --filter FullyQualifiedName~MyTimingTest
```

```bash
pgrep -f testhost
```

```bash
dotnet-trace collect --process-id <pid> --duration 00:00:08 --format Speedscope -o ~/Desktop/method.nettrace
```

For what holds the memory, take a heap dump of the same process, and read it.

```bash
dotnet-gcdump collect --process-id <pid> -o ~/Desktop/method.gcdump
```

```bash
dotnet-gcdump report ~/Desktop/method.gcdump
```

## Tier 2: Trace One Task

The plugin runs in Jellyfin's process, so attach to that process on a test server that runs on this machine. Trace one task or one route, the one the issue points at.

1. Build in `Release` with `./build.sh`, install the zip on the test server, and restart it.
2. Find the Jellyfin process.

    ```bash
    dotnet-trace ps
    ```

3. Record a CPU trace while the slow task or route runs. Use the `dotnet-sampled-thread-time` profile, since the default profile makes a large trace.

    ```bash
    dotnet-trace collect --process-id <pid> --profile dotnet-sampled-thread-time --duration 00:01:00 -o ~/Desktop/run.nettrace
    ```

4. Start the task from **Dashboard > Scheduled Tasks**, or call the route, while the trace runs.
5. Convert the trace and read it in speedscope.

    ```bash
    dotnet-trace convert ~/Desktop/run.nettrace --format Speedscope -o ~/Desktop/run
    ```

6. For counters, record while the run goes, and read allocation, collections, the thread pool queue, and lock contentions.

    ```bash
    dotnet-counters collect --process-id <pid> --counters System.Runtime --format json --duration 00:01:00 -o ~/Desktop/counters
    ```

Keep every trace, dump, and output outside the repository.

## Tier 3: Go Deeper

Stop and ask before you start Tier 3. Say what Tier 2 left unanswered and what you will do.

* Take a heap dump in the middle of the run and after it.
* Seed the test server larger, to the size the issue needs.
* Trace a second path only when the first trace points at it.

## Reading a Trace

* **Inclusive time** is a method plus everything it calls. **Exclusive time** is the method alone. Start from the largest inclusive plugin frame and walk down to the largest exclusive one.
* A frame in `System.Threading` or a wait is time spent blocked. Find what it waits on, since more CPU will not help.
* Many small frames in the allocator or the GC point to allocation pressure. Read the allocation counters, then find the allocating call.
* Time spent in a `Task` that never shows CPU is waiting on I/O. Count the calls before you tune one call.

## Common Fixes

Try these in order. Each is cheaper and safer than the next.

1. **Do less.** Skip a member whose values already match the group. Stop a pass at the first answer.
2. **Do it once.** Hoist work out of a loop. Cache a result for the length of one run, never across runs unless it is invalidated.
3. **Do it in bulk.** One config save for a batch of changes, not one per user.
4. **Do it together.** Run independent calls with `ConcurrentTaskRunner` from Base, with a limit that leaves Jellyfin room.
5. **Allocate less.** Use `Span<T>`, `StringBuilder`, pooled buffers, and `static` lambdas where a trace shows allocation pressure. Never do this without a number.
6. **Stream, never buffer.** Read a file or a response as a stream. Never load a large file into memory.

### Stored Data

* Read the plugin configuration through `Plugin.Instance` under its lock, and copy what you keep.
* Save the configuration once per change, never inside a loop over users.
* Keep the invite status file's cache, which rereads the file only when its write time changes.
* A change to what is stored or where is a [stop and ask](../starthere.md#stop-and-ask) item.

### Async and Threads

* Never block on a task with `.Result` or `.Wait()`. It starves the thread pool.
* Pass the `CancellationToken` through, so a stopped run stops working.
* Hold a lock for the shortest time, and never across an `await`.
* Bound every queue and every degree of parallelism.

### Logging

* Use the `ILogger` templates in [code.md](code.md#c-conventions). Never build the message with string interpolation.
* A log in a hot loop costs even when the level is off. Move it out of the loop or guard it with `IsEnabled`.

## Proving the Result

1. Measure the same scenario before and after, with the same data and the same machine. Record both numbers.
2. Run the tests for every layer the change touches, per [testing.md](testing.md#layers). A faster wrong answer is a bug.
3. Check the affected tab and task on the test server once more.
4. Check that memory and the thread count stay flat across a long run.
5. Report the numbers in the commit message or the report, as before and after for the same operation.

## What Not to Do

* Never ship a profiler, a timing test, a `Stopwatch`, an allocation counter, or a timing log in the plugin.
* Never commit trace files, gcdumps, or timing output.
* Never raise a timeout or lower a retry count to hide a slow call.
* Never remove an administrator exemption, a rate limit, a check, or a guard to gain speed.
* Never profile a Debug build. Use `Release`.
* Never change a [stop and ask](../starthere.md#stop-and-ask) item without approval, even when it is the cause.

## Checklist

* [ ] The reason to profile is a symptom, a change to a hot path, or a number that grew.
* [ ] The tier, the path, and the stop reason are in the report.
* [ ] The goal names the operation, the data size, and the limit.
* [ ] A trace, a counter, or a timed test shows where the time or memory goes.
* [ ] The fix is the simplest one that meets the goal.
* [ ] Before and after numbers exist for the same operation and data.
* [ ] A `Release` build was measured.
* [ ] The tests for every touched layer pass.
* [ ] No profiler code, trace file, timing test, or output is in the diff.
* [ ] Anything on the stop and ask list, such as a change to stored data, was approved first.
