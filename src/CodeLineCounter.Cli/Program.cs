using CodeLineCounter.Verbs;

return await new ServiceCollection()
    .AddSerilog()
    .Cli(c =>
    {
        c.Add<LineCounterVerb, LineCounterVerbOptions>()
         .Add<GitCounterVerb, GitCounterVerbOptions>();
    });