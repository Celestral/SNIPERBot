using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;

public class Program
{
    DiscordSocketClient client;

    static void Main(string[] args) => new Program().RunAsync(args).GetAwaiter().GetResult();

    async Task RunAsync(string[] args)
    {
        var config = new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildPresences
        };

        client = new DiscordSocketClient(config);

        client.Log += async (msg) =>
        {
            await Task.CompletedTask;
            Console.WriteLine(msg);
        };

        var token = File.ReadAllText("token.txt");

        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();

        client.Ready += Client_Ready;
        client.SlashCommandExecuted += SlashCommandHandler;

        await Task.Delay(-1);
    }

    public async Task Client_Ready()
    {

        var addProjectCommand = new SlashCommandBuilder()
        .WithName("add-project")
        .WithDescription("Add a project to the tag roles list!")
        .AddOption("project", ApplicationCommandOptionType.String, "The name of the project", isRequired: true);

        var deleteProjectCommand = new SlashCommandBuilder()
        .WithName("delete-project")
        .WithDescription("Delete a project from the tag roles list")
        .AddOption("project", ApplicationCommandOptionType.String, "The project you want to delete", isRequired: true);

        try
        {
            await client.CreateGlobalApplicationCommandAsync(addProjectCommand.Build());
            await client.CreateGlobalApplicationCommandAsync(deleteProjectCommand.Build());
        }
        catch (ApplicationCommandException e)
        {
            var json = JsonConvert.SerializeObject(e.Errors, Formatting.Indented);
            Console.WriteLine(json);
        }
    }

    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        switch (command.Data.Name)
        {
            case "add-project":
                await AddProject(command);
                break;
            case "delete-project":
                await DeleteProject(command);
                break;
        }
    }


    #region Command Implementations

    private async Task AddProject(SocketSlashCommand command)
    {
        
       throw  new NotImplementedException();

    }

    private async Task DeleteProject(SocketSlashCommand command)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region Helper methods

    #endregion
}