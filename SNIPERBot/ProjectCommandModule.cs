using Discord;
using Discord.Interactions;
using Discord.Rest;
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
        [SlashCommand("get-projects", "Gets the list for all registered projects and their embed URL")]
        public async Task GetProjects()
        {
            if (_projects.Count != 0)
            {
                EmbedBuilder builder = new EmbedBuilder()
                    .WithTitle("Projects currently available");

                ISocketMessageChannel channel = (ISocketMessageChannel) _guild.GetChannel(Settings.ProjectsChannelID);

                foreach (var project in _projects)
                {
                    var embedMessage = await channel.GetMessageAsync(project.EmbedID);
                    var embedURL = embedMessage.GetJumpUrl();

                    //builder.Description += $"[{project.Name}]({embedURL})\n";

                    EmbedFieldBuilder field = new EmbedFieldBuilder()
                        .WithName(project.Name)
                        .WithValue($"[Details]({embedURL})");

                    builder.AddField(field);
                }

                await Context.Interaction.RespondAsync(embed: builder.Build(), ephemeral: true);
            }
            else
            {
                await Context.Interaction.RespondAsync("No projects have been added yet", ephemeral: true);
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
        /// Deletes a project, role, channel and embed through a button attached to the embed
        /// </summary>
        /// <returns></returns>
        [ComponentInteraction("delete-button")]
        private async Task DeleteProjectThroughButton()
        {
            // Get the project data, role and channel sockets
            var component = (SocketMessageComponent)Context.Interaction;
            var embed = component.Message.Embeds.FirstOrDefault();
            int projectID = int.Parse(embed.Footer.Value.Text);

            await DeferAsync();
            var isConfirmed = await InteractionUtility.ConfirmAsync(_client, Context.Channel, TimeSpan.FromSeconds(10), "Are you sure you want to delete " + embed.Title + "?");

            if (isConfirmed)
            {
                DeleteProject(projectID, component);                
            }
        }

        [ComponentInteraction("role-button")]
        private async Task AssignUpdateRole()
        {
            var component = (SocketMessageComponent)Context.Interaction;
            var embed = component.Message.Embeds.FirstOrDefault();
            int projectID = int.Parse(embed.Footer.Value.Text);

            var project = _projects.FirstOrDefault(x => x.Id == projectID);

            await GiveOrRemoveRole(project.RoleID, component.User.Id);

            await DeferAsync();
        }

        // test createhiddenchannel with queen role
        [ComponentInteraction("edit-button")]
        private async Task EditProject()
        {
            var component = (SocketMessageComponent)Context.Interaction;
            var embed = component.Message.Embeds.FirstOrDefault();
            var projectId = embed?.Footer?.Text ?? "-1";

            await Context.Interaction.RespondWithModalAsync<ProjectModal>($"edit_project-{projectId}", RequestOptions.Default, builder => ModifyModal(builder, projectId));
        }

        private void ModifyModal(ModalBuilder obj, string projectId)
        {
            var project = _projects.First(x => x.Id == Convert.ToInt32(projectId));
            var projectModal = new ProjectModal();
            projectModal.Name = project.Name;
            projectModal.Description = project.Description;
            projectModal.Twitter = project.Twitter;
            projectModal.Discord = project.Discord;

            var projectModalType = typeof(ProjectModal);
            var listofmembers = new Dictionary<string, object>();

            obj.Components = new ModalComponentBuilder();
            foreach (var prop in projectModalType.GetProperties())
            {
                if (prop.Name != "Title")
                {
                    var attributes = prop.CustomAttributes.ToArray();
                    var componentBuilder = new TextInputBuilder();
                    RequiredInputAttribute requiredAttribute = (RequiredInputAttribute) prop.GetCustomAttributes(typeof(RequiredInputAttribute), false)[0];
                    componentBuilder.Required = requiredAttribute.IsRequired;

                    ModalTextInputAttribute customIDAttribute = (ModalTextInputAttribute) prop.GetCustomAttributes(typeof(ModalTextInputAttribute), false)[0];
                    componentBuilder.CustomId = customIDAttribute.CustomId;
                    componentBuilder.Style = customIDAttribute.Style;
                    componentBuilder.Placeholder = customIDAttribute.Placeholder;
                    componentBuilder.MaxLength = customIDAttribute.MaxLength;

                    InputLabelAttribute inputLabelAttribute = (InputLabelAttribute)prop.GetCustomAttributes(typeof(InputLabelAttribute), false)[0];
                    componentBuilder.Label = inputLabelAttribute.Label;

                    componentBuilder.Value = (string) prop.GetValue(projectModal);
                    obj.AddTextInput(componentBuilder);
                }
            }
            obj.Build();
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
            var projectChannel = await CreateHiddenChannel(modal.Name, role);

            var project = new Project();
            project.Id = _projects.Any() ? _projects.Max(x => x.Id) + 1 : 1;
            project.Name = modal.Name;
            project.Description = modal.Description;
            project.Twitter = modal.Twitter;
            project.Discord = modal.Discord;
            project.RoleID = role.Id;
            project.ChannelID = projectChannel.Id;

            var buttons = new ComponentBuilder()
                    .WithButton("Role", "role-button", ButtonStyle.Primary, new Emoji("✨"))
                    .WithButton("Edit", "edit-button", ButtonStyle.Secondary)
                    .WithButton("Delete", "delete-button", ButtonStyle.Danger);

            var footer = new EmbedFooterBuilder()
                .WithText(project.Id.ToString());

            var embedBuilder = new EmbedBuilder()
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
                embedBuilder.AddField(twitterField);
            }

            var discordField = new EmbedFieldBuilder()
            .WithName("Discord")
            .WithIsInline(true);

            if (!string.IsNullOrEmpty(project.Discord))
            {
                discordField.WithValue(project.Discord);
                embedBuilder.AddField(discordField);
            }

            var channel = _guild.GetChannel(Settings.ProjectsChannelID);

            ISocketMessageChannel socketMessageChannel = (ISocketMessageChannel)channel;
            var message = await socketMessageChannel.SendMessageAsync(embed: embedBuilder.Build(), components: buttons.Build());
            project.EmbedID = message.Id;

            _projects.Add(project);

            var json = JsonConvert.SerializeObject(_projects);
            File.WriteAllText("projects.json", json);

            await DeferAsync();
        }

        [ModalInteraction("edit_project-*")]
        public async Task EditModalResponse(string id, ProjectModal modal)
        {
            var projectId = Convert.ToInt32(id);

            var project = _projects.First(x => x.Id == Convert.ToInt32(projectId));

            var previousName = project.Name;

            project.Name = modal.Name;
            project.Description = modal.Description;
            project.Twitter = modal.Twitter;
            project.Discord = modal.Discord;

            var json = JsonConvert.SerializeObject(_projects);
            File.WriteAllText("projects.json", json);

            var buttons = new ComponentBuilder()
                    .WithButton("Role", "role-button", ButtonStyle.Primary, new Emoji("✨"))
                    .WithButton("Edit", "edit-button", ButtonStyle.Secondary)
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
            var message = await socketMessageChannel.ModifyMessageAsync(project.EmbedID, (x => { x.Embed = embedBuiler.Build(); x.Components = buttons.Build(); })); //await socketMessageChannel.GetMessageAsync(project.EmbedID); //SendMessageAsync(embed: embedBuiler.Build(), components: buttons.Build());

            if (previousName != project.Name)
            {
                var role = _guild.GetRole(project.RoleID);
                var projectChannel = _guild.GetChannel(project.ChannelID);

                await role.ModifyAsync(x => x.Name = project.Name);
                await projectChannel.ModifyAsync(x => x.Name = project.Name);
            }

            await DeferAsync();
        }

        private async Task<RestTextChannel> CreateHiddenChannel(string projectName, IRole projectRole)
        {
            var channel = await _guild.CreateTextChannelAsync(projectName, x => { x.CategoryId = Settings.TestCategoryID; });

            var bot_permissions = new OverwritePermissions();
            bot_permissions = bot_permissions.Modify(viewChannel: PermValue.Allow, manageChannel: PermValue.Allow, manageRoles: PermValue.Allow);

            var regular_permissions = new OverwritePermissions();
            regular_permissions = regular_permissions.Modify(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, addReactions: PermValue.Allow, readMessageHistory: PermValue.Allow);

            await channel.AddPermissionOverwriteAsync(_guild.GetRole(Settings.SniperBotRole), bot_permissions);
            await channel.AddPermissionOverwriteAsync(_guild.GetRole(Settings.EveryoneRole), OverwritePermissions.DenyAll(channel));
            await channel.AddPermissionOverwriteAsync(projectRole, regular_permissions);

            return channel;
        }
        #endregion

        #region Helper Methods
        private async Task GiveOrRemoveRole(ulong roleID, ulong userID)
        {
            var role = _guild.GetRole(roleID);
            var member = _guild.Users.First(x => x.Id == userID);
            if (member.Roles.FirstOrDefault(x => x.Id == roleID) != null)
            {
                await member.RemoveRoleAsync(role);
            }
            else
            {
                await member.AddRoleAsync(role);
            }
        }

        private async Task DeleteProject(int projectID, SocketMessageComponent embedMessage)
        {
            var project = _projects.FirstOrDefault(x => x.Id == projectID);

            var role = _guild.GetRole(project.RoleID);
            var channel = _guild.GetChannel(project.ChannelID);

            try
            {
                await role.DeleteAsync();
                await channel.DeleteAsync();

                _projects.Remove(project);


                var json = JsonConvert.SerializeObject(_projects);
                File.WriteAllText("projects.json", json);

                await embedMessage.Message.DeleteAsync();
            }
            catch (Exception e)
            {
                await embedMessage.RespondAsync("Something went wrong trying to delete the project: " + e.Message, ephemeral: true);
            }
        }
            #endregion
        }

    public class ProjectModal : IModal
    {
        public string Title => "Add a new project";
        [ModalTextInput("project_name", placeholder: "Project name", maxLength: 20)]
        [RequiredInput(true)]
        [InputLabel("Project Name")]
        public string Name { get; set; }

        [ModalTextInput("project_description", style: TextInputStyle.Paragraph, placeholder: "Optional description of project", maxLength: 500)]
        [RequiredInput(false)]
        [InputLabel("Description")]
        public string Description { get; set; }

        [ModalTextInput("project_twitter", placeholder: "Twitter link", maxLength: 40)]
        [RequiredInput(false)]
        [InputLabel("Twitter")]
        public string Twitter { get; set; }

        [ModalTextInput("project_discord", placeholder: "Discord link", maxLength: 40)]
        [RequiredInput(false)]
        [InputLabel("Discord")]
        public string Discord { get; set; }
    }
}
