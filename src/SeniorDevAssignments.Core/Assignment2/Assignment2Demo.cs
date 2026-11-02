namespace SeniorDevAssignments.Core.Assignment2;

public static class Assignment2Demo
{
    public const string InputA = """
        {
          "CustomerId": "CUST-999",
          "SubscriptionStatus": "Active",
          "TotalAmount": 150.50,
          "metadata": { "zipCode": 12345, "notes": "New user" }
        }
        """;

    public const string InputB = """
        {
          "CustomerId": "CUST-999",
          "SubscriptionStatus": "Active",
          "TotalAmount": 150.50,
          "metadata": { "zipCode": "12345-678", "notes": ["New user", "VIP"] }
        }
        """;

    public static IReadOnlyList<WebhookProcessResult> Run()
    {
        Console.WriteLine();
        Console.WriteLine("========== ASSIGNMENT 2: Resilient Schema Parser ==========");
        Console.WriteLine();

        var processor = new WebhookProcessor();
        var results = new List<WebhookProcessResult>();

        Console.WriteLine("--- Input A (Standard JSON) ---");
        var resultA = processor.ProcessPayload(InputA);
        PrintResult(resultA);
        results.Add(resultA);

        Console.WriteLine();
        Console.WriteLine("--- Input B (Drifted JSON) ---");
        var resultB = processor.ProcessPayload(InputB);
        PrintResult(resultB);
        results.Add(resultB);

        Console.WriteLine();
        Console.WriteLine("Expected: both return critical fields; Input B also emits Schema Drift Warning (no exception).");
        return results;
    }

    private static void PrintResult(WebhookProcessResult result)
    {
        var c = result.CriticalData;
        Console.WriteLine($"  CustomerId          : {c.CustomerId}");
        Console.WriteLine($"  SubscriptionStatus  : {c.SubscriptionStatus}");
        Console.WriteLine($"  TotalAmount         : {c.TotalAmount}");
        Console.WriteLine($"  SchemaDriftDetected : {result.SchemaDriftDetected}");
        if (result.SchemaDriftDetected)
            Console.WriteLine($"  Warning             : {result.SchemaDriftWarning}");
    }
}
