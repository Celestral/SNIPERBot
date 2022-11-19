using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIPERBot
{
    public class ProjectCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private static List<Project> _projects = new List<Project>();
        private static DiscordSocketClient _client;
        private static SocketGuild _guild;

        /// <summary>
        /// Reads existing projects from file during startup
        /// </summary>
        /// <param name="client"></param>
        public ProjectCommandModule(DiscordSocketClient client)
        {
            if (File.Exists("projects.json"))
            {
                _projects = JsonConvert.DeserializeObject<List<Project>>(File.ReadAllText("projects.json"));
            }
            _client = client;
            _guild = _client.GetGuild(Settings.GuildId);
        }
        #region Slash Commands

        /// <summary>
        /// Responds with a basic list of all projects
        /// </summary>
        /// <returns></returns>
        [SlashCommand("get-projects", "Gets the list for all registered projects")]
        public async Task GetProjects()
        {
            var message = "";
            if (_projects.Count != 0)
            {
                foreach (var project in _projects)
                {
                    message += project.Name + ", ";
                }

                await Context.Interaction.RespondAsync(message);
            }
            else
            {
                await Context.Interaction.RespondAsync("No projects have been added yet");
            }
        }

        /// <summary>
        /// Responds with a Modal form for adding a new project
        /// </summary>
        /// <returns></returns>
        [SlashCommand("add-project", "Add a project to the bot!")]
        public async Task AddProject()
        {
            await Context.Interaction.RespondWithModalAsync<ProjectModal>("project_modal");
        }

        #endregion

        #region Component Interactions

        /// <summary>
        /// Deletes a project through a button attached to an embed and deletes the embed too
        /// </summary>
        /// <returns></returns>
        [ComponentInteraction("delete-button")]
        private async Task DeleteProjectThroughButton()
        {
            var component = (SocketMessageComponent)Context.Interaction;
            var embed = component.Message.Embeds.FirstOrDefault();
            int projectID = int.Parse(embed.Footer.Value.Text);

            var project = _projects.FirstOrDefault(x => x.Id == projectID);
            var role = _guild.GetRole(project.RoleID);
            var channel = _guild.GetChannel(project.ChannelID);

            //TODO: try+catch
            await role.DeleteAsync();
            await channel.DeleteAsync();

            _projects.Remove(project);

            var json = JsonConvert.SerializeObject(_projects);
            File.WriteAllText("projects.json", json);

            await component.Message.DeleteAsync();
        }

        [ComponentInteraction("update-button")]
        private async Task AssignUpdateRole()
        {
            var component = (SocketMessageComponent)Context.Interaction;
            var embed = component.Message.Embeds.FirstOrDefault();
            int projectID = int.Parse(embed.Footer.Value.Text);

            var project = _projects.FirstOrDefault(x => x.Id == projectID);

            var role = _guild.GetRole(project.RoleID);
            var member = _guild.Users.First(x => x.Id == component.User.Id);
            if (member.Roles.FirstOrDefault(x => x.Id == project.RoleID) != null)
            {
                await member.RemoveRoleAsync(role);
            }
            else
            {
                await member.AddRoleAsync(role);
            }

            await DeferAsync();
        }

        #endregion

        #region Modal Interactions

        /// <summary>
        /// Creates a new Project according to the filled in modal and writes and posts an embed with the details in the projects channel
        /// </summary>
        /// <param name="modal"></param>
        /// <returns></returns>
        [ModalInteraction("project_modal")]
        public async Task ModalResponse(ProjectModal modal)
        {
            var role = await _guild.CreateRoleAsync(modal.Name);
            var projectChannel = await _guild.CreateTextChannelAsync(modal.Name, x => { x.CategoryId = Settings.TestCategoryID; });

            var project = new Project();
            project.Id = _projects.Any() ? _projects.Max(x => x.Id) + 1 : 1;
            project.Name = modal.Name;
            project.Description = modal.Description;
            project.Twitter = modal.Twitter;
            project.Discord = modal.Discord;
            project.RoleID = role.Id;
            project.ChannelID = projectChannel.Id;

            _projects.Add(project);

            var json = JsonConvert.SerializeObject(_projects);
            File.WriteAllText("projects.json", json);

            var buttons = new ComponentBuilder()
                    .WithButton("Updates", "update-button", ButtonStyle.Primary, new Emoji("✨"))
                    .WithButton("Whitelist", "whitelist-button")
                    .WithButton("Delete", "delete-button", ButtonStyle.Danger);

            var footer = new EmbedFooterBuilder()
                .WithText(project.Id.ToString());

            var embedBuiler = new EmbedBuilder()
                        .WithTitle(project.Name)
                        .WithDescription(project.Description)
                        .WithColor(Color.Blue)
                        .WithFooter(footer)
                        .WithCurrentTimestamp()
                        .WithAuthor(Context.User);

            var twitterField = new EmbedFieldBuilder()
            .WithName("Twitter")
            .WithIsInline(true);

            if (!string.IsNullOrEmpty(project.Twitter))
            {
                twitterField.WithValue(project.Twitter);
                embedBuiler.AddField(twitterField);
            }

            var discordField = new EmbedFieldBuilder()
            .WithName("Discord")
            .WithIsInline(true);

            if (!string.IsNullOrEmpty(project.Discord))
            {
                discordField.WithValue(project.Discord);
                embedBuiler.AddField(discordField);
            }

            var channel = _guild.GetChannel(Settings.ProjectsChannelID);

            ISocketMessageChannel socketMessageChannel = (ISocketMessageChannel)channel;
            await socketMessageChannel.SendMessageAsync(embed: embedBuiler.Build(), components: buttons.Build());
            await DeferAsync();
        }
        #endregion

        #region Helper Methods

        #endregion
    }

    public class ProjectModal : IModal
    {
        public string Title => "Add a new project";
        [InputLabel("Project Name")]
        [ModalTextInput("project_name", placeholder: "Project name", maxLength: 20)]
        public string Name { get; set; }

        [RequiredInput(false)]
        [InputLabel("Description")]
        [ModalTextInput("project_description", placeholder: "Optional description of project", maxLength: 500)]
        public string Description { get; set; }

        [RequiredInput(false)]
        [InputLabel("Twitter")]
        [ModalTextInput("project_twitter", placeholder: "Twitter link", maxLength: 40)]
        public string Twitter { get; set; }

        [RequiredInput(false)]
        [InputLabel("Discord")]
        [ModalTextInput("project_discord", placeholder: "Discord link", maxLength: 40)]
        public string Discord { get; set; }
    }
}
