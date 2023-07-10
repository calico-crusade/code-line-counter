using CodeLineCounter.Core;

return await new ServiceCollection()
    .AddSerilog()
    .Cli(c =>
    {
        c.Add<LineCounterVerb, LineCounterVerbOptions>();
    });