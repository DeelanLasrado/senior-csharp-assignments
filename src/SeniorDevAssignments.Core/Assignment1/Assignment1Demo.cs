namespace SeniorDevAssignments.Core.Assignment1;

public static class Assignment1Demo
{
    /// <summary>
    /// Required input order from the assignment brief.
    /// Cancel for Order 1 sits immediately after Create for Order 1 in the queue,
    /// which is exactly the race that parallel workers would otherwise mishandle.
    /// </summary>
    public static IReadOnlyList<OrderMessage> SampleMessages { get; } =
    [
        new(1, "Create"),
        new(2, "Create"),
        new(1, "Cancel"), // must wait for Order 1 Create
        new(3, "Create"),
        new(2, "Update")
    ];

    public static async Task<IReadOnlyList<string>> RunAsync()
    {
        Console.WriteLine();
        Console.WriteLine("========== ASSIGNMENT 1: Asynchronous Race Condition ==========");
        Console.WriteLine("3 parallel workers + per-OrderId sequential gates");
        Console.WriteLine();

        await using var dispatcher = new MessageDispatcher(workerCount: 3);

        // Start consumers first, then enqueue — mirrors a live bus with warm workers.
        var runTask = dispatcher.RunAsync();
        await dispatcher.EnqueueAsync(SampleMessages);
        await runTask;

        Console.WriteLine();
        Console.WriteLine("Expected: Order 2 & 3 overlap with Order 1; Order 1 Cancel starts only after Order 1 Create finishes.");
        return dispatcher.Log;
    }
}
