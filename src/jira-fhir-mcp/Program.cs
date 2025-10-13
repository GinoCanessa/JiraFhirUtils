
using System.CommandLine;
using System.Text.RegularExpressions;
using jira_fhir_mcp.Tools;
using jira_fhir_mcp.Services;
using JiraFhirUtils.Common;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;

namespace jira_fhir_mcp;

public abstract partial class Program
{
    private static int _retVal = 0;

    [GeneratedRegex("(http[s]*:\\/\\/.*(:\\d+)*)")]
    private static partial Regex InputUrlFormatRegex();

    /// <summary>Main entry-point for this application.</summary>
    /// <param name="args">An array of command-line argument strings.</param>
    public static async Task<int> Main(string[] args)
    {
        // set up our configuration (command line > environment > appsettings.json)
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>()
            .Build();

        CliOptions cliOptions = new CliOptions();

        RootCommand root = new RootCommand("JIRA FHIR MCP Server");

        // Create commands from CliOptions.Commands so the list is defined in one place
        foreach ((string name, Command cmd) in CliOptions.Commands)
        {
            // set the handlers for each command
            switch (name)
            {
                case CliMcpHttpXmlCommand.CommandName:
                    cmd.SetAction((ParseResult pr) => mcpHttpCommandHandler(pr, configuration));
                    break;
            }

            root.Add(cmd);
        }

        ParseResult pr = root.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        await pr.InvokeAsync();

        return _retVal;
    }
    
    private static async Task mcpHttpCommandHandler(ParseResult pr, IConfiguration configuration)
    {
        if (pr.CommandResult.Command is not CliMcpHttpXmlCommand mcpCommand)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            _retVal = 1;
            return;
        }

        CliConfig config = new(mcpCommand.CommandCliOptions, pr, configuration);

        try
        {
            // update configuration to make sure listen url is properly formatted
            Match match = InputUrlFormatRegex().Match(config.PublicUrl);
            string publicUrl = match.ToString();

            if (publicUrl.EndsWith('/'))
            {
                publicUrl = config.PublicUrl.Substring(0, config.PublicUrl.Length - 1);
            }
            
            if (config.PublicUrl != publicUrl)
            {
                Console.WriteLine($"Updating PublicUrl from '{config.PublicUrl}' to '{publicUrl}'");
                config = config with { PublicUrl = publicUrl };
            }

            WebApplicationBuilder? builder = null;

            // when packaging as a dotnet tool, we need to do some directory shenanigans for the static content root
            string root = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location ?? AppContext.BaseDirectory) ?? string.Empty;
            if (!string.IsNullOrEmpty(root))
            {
                string? staticWebRoot = FileUtils.FindRelativeDir(root, "staticwebassets", false);
                string? wwwRoot = FileUtils.FindRelativeDir(root, "wwwroot", false);

                if ((!string.IsNullOrEmpty(staticWebRoot)) && Directory.Exists(staticWebRoot))
                {
                    builder = WebApplication.CreateBuilder(new WebApplicationOptions()
                    {
                        WebRootPath = staticWebRoot,
                    });
                }
                else if ((!string.IsNullOrEmpty(wwwRoot)) && Directory.Exists(wwwRoot))
                {
                    builder = WebApplication.CreateBuilder(new WebApplicationOptions()
                    {
                        WebRootPath = wwwRoot,
                    });
                }
            }

            
            // if we didn't find a web root, use the default
            builder ??= WebApplication.CreateBuilder();

            string appCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "fhir-jira-mcp-key-store");
            if (!Directory.Exists(appCacheDir))
            {
                Directory.CreateDirectory(appCacheDir);
            }
            
            StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
            builder.WebHost.UseStaticWebAssets();

            builder.Services.AddDataProtection()
                .SetApplicationName("fhir-jira-mcp")
                .PersistKeysToFileSystem(new DirectoryInfo(appCacheDir));
            builder.Services.AddCors();

            // add our configuration
            builder.Services.AddSingleton(config);

            // add database service
            builder.Services.AddSingleton<DatabaseService>();

            ToolProcessor toolProcessor = new();
            builder.Services.AddSingleton<ToolProcessor>(toolProcessor);

            // add MCP services
            builder.Services.AddMcpServer()
                .WithHttpTransport()
                .WithListToolsHandler(toolProcessor.HandleListToolsRequest)
                .WithCallToolHandler(toolProcessor.HandleCallToolRequest);
            
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddControllers();
            builder.Services.AddHttpClient();

            builder.Services.AddAntiforgery();

            string localUrl = $"http://*:{config.Port}";

            builder.WebHost.UseUrls(localUrl);

            WebApplication app = builder.Build();

            // we want to essentially disable CORS
            app.UseCors(b => b
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders([ "Content-Location", "Location", "Etag", "Last-Modified" ]));

            app.UseStaticFiles();

            app.UseRouting();
            app.UseAntiforgery();
            app.MapControllers();

            // this is developer tooling - always respond with as much detail as we can
            app.UseDeveloperExceptionPage();

            // Initialize DatabaseService
            DatabaseService databaseService = app.Services.GetRequiredService<DatabaseService>();
            databaseService.Initialize();

            // map our MCP services, use a /mcp prefix to avoid collisions with other services (e.g., UI)
            app.MapMcp("/mcp");
            
            // run the server
            _ = app.StartAsync();

            await app.WaitForShutdownAsync();

            _retVal = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error serving MCP: {ex.Message}");
            _retVal = ex.HResult;
        }
    }
}
