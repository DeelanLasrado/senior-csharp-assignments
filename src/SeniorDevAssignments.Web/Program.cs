using System.Text;
using SeniorDevAssignments.Core.Assignment1;
using SeniorDevAssignments.Core.Assignment2;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Content(HomeHtml(), "text/html"));

app.MapGet("/api/assignment1", async () =>
{
    var originalOut = Console.Out;
    var buffer = new StringWriter();
    Console.SetOut(buffer);
    try
    {
        var log = await Assignment1Demo.RunAsync();
        return Results.Json(new
        {
            assignment = 1,
            title = "Asynchronous Race Condition",
            summary = "3 workers process different OrderIds in parallel; same OrderId is strictly sequential.",
            log
        });
    }
    finally
    {
        Console.SetOut(originalOut);
    }
});

app.MapGet("/api/assignment2", () =>
{
    var originalOut = Console.Out;
    var buffer = new StringWriter();
    Console.SetOut(buffer);
    try
    {
        var results = Assignment2Demo.Run();
        return Results.Json(new
        {
            assignment = 2,
            title = "Resilient Schema Parser",
            results = results.Select(r => new
            {
                r.CriticalData.CustomerId,
                r.CriticalData.SubscriptionStatus,
                r.CriticalData.TotalAmount,
                r.SchemaDriftDetected,
                r.SchemaDriftWarning,
                r.RawCorruptedNodeJson
            })
        });
    }
    finally
    {
        Console.SetOut(originalOut);
    }
});

app.MapPost("/api/assignment2/process", (ProcessRequest body) =>
{
    var processor = new WebhookProcessor();
    var result = processor.ProcessPayload(body.Json);
    return Results.Json(result);
});

app.Run();

static string HomeHtml()
{
    var sb = new StringBuilder();
    sb.Append("""
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="utf-8" />
      <meta name="viewport" content="width=device-width, initial-scale=1" />
      <title>Senior C# Assignments</title>
      <style>
        :root {
          --ink: #12263a;
          --paper: #f7f3eb;
          --accent: #0f766e;
          --line: #d6cfc3;
          --warn: #9a3412;
        }
        * { box-sizing: border-box; }
        body {
          margin: 0;
          font-family: "Segoe UI", "Helvetica Neue", sans-serif;
          color: var(--ink);
          background:
            radial-gradient(circle at 10% 0%, #e8f3f1 0%, transparent 40%),
            radial-gradient(circle at 90% 10%, #f3e9df 0%, transparent 35%),
            var(--paper);
          min-height: 100vh;
        }
        main { max-width: 920px; margin: 0 auto; padding: 2.5rem 1.25rem 4rem; }
        h1 { font-size: clamp(1.8rem, 4vw, 2.6rem); margin: 0 0 .4rem; letter-spacing: -0.02em; }
        .lede { color: #40566d; margin-bottom: 1.75rem; max-width: 42rem; }
        .actions { display: flex; flex-wrap: wrap; gap: .75rem; margin-bottom: 1.25rem; }
        button {
          border: 0; background: var(--accent); color: white; font-weight: 600;
          padding: .7rem 1.1rem; border-radius: 6px; cursor: pointer;
        }
        button.secondary { background: #334155; }
        button:disabled { opacity: .6; cursor: wait; }
        pre {
          background: #102033; color: #e2e8f0; padding: 1rem 1.1rem;
          border-radius: 8px; overflow: auto; min-height: 220px;
          white-space: pre-wrap; word-break: break-word; font-size: .86rem;
          border: 1px solid var(--line);
        }
        .meta { font-size: .9rem; color: #526579; margin-bottom: .75rem; }
        a { color: var(--accent); }
      </style>
    </head>
    <body>
      <main>
        <h1>Senior C# Developer Assignments</h1>
        <p class="lede">
          Live demo of Assignment 1 (per-OrderId sequential messaging with parallel workers)
          and Assignment 2 (resilient webhook parsing under schema drift).
        </p>
        <div class="actions">
          <button id="run1">Run Assignment 1</button>
          <button id="run2" class="secondary">Run Assignment 2</button>
        </div>
        <p class="meta">APIs: <a href="/api/assignment1">/api/assignment1</a> · <a href="/api/assignment2">/api/assignment2</a></p>
        <pre id="out">Click a button to run a demo…</pre>
      </main>
      <script>
        const out = document.getElementById('out');
        async function run(path, btn) {
          btn.disabled = true;
          out.textContent = 'Running ' + path + ' …';
          try {
            const res = await fetch(path);
            const data = await res.json();
            out.textContent = JSON.stringify(data, null, 2);
          } catch (e) {
            out.textContent = 'Error: ' + e;
          } finally {
            btn.disabled = false;
          }
        }
        document.getElementById('run1').onclick = (e) => run('/api/assignment1', e.target);
        document.getElementById('run2').onclick = (e) => run('/api/assignment2', e.target);
      </script>
    </body>
    </html>
    """);
    return sb.ToString();
}

internal sealed record ProcessRequest(string Json);
