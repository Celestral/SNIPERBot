using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using SNIPERBot;

public class Program
{
    DiscordSocketClient client;
    List<Project> _projects = new List<Project>();

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
        client.ModalSubmitted += Client_ModalSubmitted;
        client.ButtonExecuted += ButtonHandler;

        await Task.Delay(-1);
    }

    public async Task Client_Ready()
    {
        if (File.Exists("projects.json"))
        {
            _projects = JsonConvert.DeserializeObject<List<Project>>(File.ReadAllText("projects.json"));
        }

        var addProjectCommand = new SlashCommandBuilder()
        .WithName("add-project")
        .WithDescription("Add a project to the bot!");

        var getProjectsCommand = new SlashCommandBuilder()
        .WithName("get-projects")
        .WithDescription("Gets the list for all registered projects");

        var deleteProjectCommand = new SlashCommandBuilder()
        .WithName("delete-project")
        .WithDescription("Delete a project from the tag roles list")
        .AddOption("project", ApplicationCommandOptionType.String, "The project you want to delete", isRequired: true);

        try
        {
            await client.CreateGlobalApplicationCommandAsync(getProjectsCommand.Build());
            await client.CreateGlobalApplicationCommandAsync(addProjectCommand.Build());
            await client.CreateGlobalApplicationCommandAsync(deleteProjectCommand.Build());
        }
        catch (ApplicationCommandException e)
        {
            var json = JsonConvert.SerializeObject(e.Errors, Formatting.Indented);
            Console.WriteLine(json);
        }
    }
    public async Task ButtonHandler(SocketMessageComponent component)
    {
        switch (component.Data.CustomId)
        {
            case "delete-button":
                await DeleteProject(component);
                break;
        }
    }

    private async Task Client_ModalSubmitted(SocketModal arg)
    {
        // Get the values of components.
        List<SocketMessageComponentData> components =
            arg.Data.Components.ToList();

        var project = new Project();
        project.Id = _projects.Any() ? _projects.Max(x => x.Id) + 1 : 1;
        project.Name = components
            .First(x => x.CustomId == "project_name").Value;
        project.Description = components
            .First(x => x.CustomId == "project_description").Value;
        project.Twitter = components
            .First(x => x.CustomId == "project_twitter").Value;
        project.Discord = components
            .First(x => x.CustomId == "project_discord").Value;

        _projects.Add(project);

        var json = JsonConvert.SerializeObject(_projects);
        File.WriteAllText("projects.json", json);


        // Build the message to send.
        //string message = "Project " + project.Name + " has been added by " + arg.User.Mention;

        // Specify the AllowedMentions so we don't actually ping everyone.
        AllowedMentions mentions = new AllowedMentions();
        mentions.AllowedTypes = AllowedMentionTypes.Users;

        var buttons = new ComponentBuilder()
                .WithButton("Updates", "update-button", ButtonStyle.Primary, new Emoji("✨"))
                .WithButton("Whitelist", "whitelist-button")
                .WithButton("Delete", "delete-button", ButtonStyle.Danger);

        var twitterField = new EmbedFieldBuilder()
        .WithName("Twitter")
        .WithIsInline(true);

        if (!string.IsNullOrEmpty(project.Twitter))
        {
            twitterField.WithValue(project.Twitter);
        }

        var discordField = new EmbedFieldBuilder()
        .WithName("Discord")
        .WithIsInline(true);

        if (!string.IsNullOrEmpty(project.Discord))
        {
            discordField.WithValue(project.Discord);
        }

        var footer = new EmbedFooterBuilder()
            .WithText(project.Id.ToString());

        var embedBuiler = new EmbedBuilder()
                    .WithTitle(project.Name)
                    .WithDescription(project.Description)
                    //.AddField(twitterField)
                    //.AddField(discordField)
                    .WithColor(Color.Blue)
                    .WithFooter(footer)
                    .WithCurrentTimestamp()
                    .WithAuthor(arg.User);


        var guild = client.GetGuild(990594846563647578);
        var channel = guild.GetChannel(1042928482155499570);

        ISocketMessageChannel socketMessageChannel = (ISocketMessageChannel)channel;
        await socketMessageChannel.SendMessageAsync(embed: embedBuiler.Build(), components: buttons.Build());
        await arg.DeferAsync();
        // Respond to the modal.
        //await arg.RespondAsync(message, allowedMentions: mentions);
    }

    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        switch (command.Data.Name)
        {
            case "get-projects":
                await GetProjects(command);
                break;
            case "add-project":
                await AddProject(command);
                break;
            case "delete-project":
                await DeleteProject(command);
                break;
        }
    }


    #region Command Implementations

    private async Task GetProjects(SocketSlashCommand command)
    {
        var message = "";
        if (_projects.Count != 0)
        {
            foreach (var project in _projects)
            {
                message += project.Name + ", ";
            }

            await command.RespondAsync(message);
        }
        else
        {
            await command.RespondAsync("No projects have been added yet");
        }
    }

    private async Task AddProject(SocketSlashCommand command)
    {

        var modal = new ModalBuilder()
       .WithTitle("Project Details")
       .WithCustomId("project_details")
       .AddTextInput("Name", "project_name", placeholder: "Project name")
       .AddTextInput("Description", "project_description", TextInputStyle.Paragraph,
           "Optional description of project", required: false)
       .AddTextInput("Twitter", "project_twitter", required: false)
       .AddTextInput("Discord", "project_discord", required: false);

        await command.RespondWithModalAsync(modal.Build());

    }

    private async Task DeleteProject(SocketMessageComponent component)
    {
        var embed = component.Message.Embeds.FirstOrDefault();
        int projectID = int.Parse(embed.Footer.Value.Text);

        var project = _projects.FirstOrDefault(x => x.Id == projectID);

        _projects.Remove(project);

        var json = JsonConvert.SerializeObject(_projects);
        File.WriteAllText("projects.json", json);

        await component.Message.DeleteAsync();
    }

    private async Task DeleteProject(SocketSlashCommand command)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region Helper methods

    #endregion
}