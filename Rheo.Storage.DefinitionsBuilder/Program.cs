using Rheo.Storage.DefinitionsBuilder.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

// Analyze a file
//var results = TridFileAnalyzer.AnalyzeFile("test.pdf", definitionsBlock);

//foreach (var result in results.Take(5))
//{
//    Console.WriteLine($"{result.Percentage:F1}% ({result.Definition.Extension}) {result.Definition.FileType}");
//}

// Display Banner
if (args.Length == 0)
{
    var font = FigletFont.Load(Configuration.FigletFont);
    AnsiConsole.Write(new FigletText(font, "Rheo").Centered().Color(Color.Green));
    AnsiConsole.Write(new Align(new Text(Configuration.ProductName, new Style(Color.Yellow)), HorizontalAlignment.Center));
    var rule = new Rule($"[yellow]v{Configuration.Version}[/]");
    AnsiConsole.Write(rule);
    Console.WriteLine();
}

// Initialize app container
var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName(Configuration.ExeName);
    config.SetApplicationVersion(Configuration.ProductName + " " + Configuration.Version);
    config.AddCommand<PackageCommand>("pack")
        .WithDescription("Compile Rheo File Definition Package using the data source")
        .WithExample("pack")
        .WithExample("pack", @"Data\TrID\triddefs.trd")
        .WithExample("pack", @"Data\TrID\triddefs.trd", "-o Output");

#if DEBUG
    config.PropagateExceptions();
#endif
});
app.Run(args);

if (args.Length == 0)
{
    // Interactive mode
    Console.WriteLine();
    Console.WriteLine("Type 'exit' to quit.");
    while (true)
    {
        Console.WriteLine();
        Console.Write("> ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
            continue;
        else if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }
        else
        {
            app.Run(input.Split(' '));
        }
    }
}
