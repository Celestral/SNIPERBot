using Azure.Storage.Blobs;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;
using SNIPERBot.Utils;
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
        private DiscordSocketClient _client;
        private static SocketGuild _guild;
        private LoggingService _loggingService;
        private readonly BlobClient _blobClient;

        /// <summary>
        /// Reads existing projects from file during startup
        /// </summary>
        /// <param name="client"></param>
        public ProjectCommandModule(DiscordSocketClient client, LoggingService loggingService, BlobClient blobClient)
        {
            if (blobClient.Exists() && !_projects.Any())
            {
                _projects = JsonConvert.DeserializeObject<List<Project>>(blobClient.DownloadContent().Value.Content.ToString());
            }
            _client = client;
            _loggingService = loggingService;
            _blobClient = blobClient;
            _guild = _client.GetGuild(Settings.GuildId);
        }
        #region Slash Commands

        /// <summary>
        /// Responds with a basic list of all projects
        /// </summary>
        /// <returns></returns>
        [SlashCommand("get-projects", "Gets the list for all registered projects and their embed URL")]
        public async Task GetProjects([Choice("All", "all"), Choice("Not yet minted", "unminted")] string MintStatus)
        {
            await Context.Interaction.RespondAsync("Difficult question. Thinking about it, will come back to you shortly...", ephemeral: true);

            if (_projects.Count != 0)
            {
                EmbedBuilder builder = new EmbedBuilder()
                    .WithTitle(MintStatus == "unminted" ? "Projects that haven't minted yet" : "All projects");

                ISocketMessageChannel channel = (ISocketMessageChannel) _guild.GetChannel(Settings.EmbedsChannelID);

                foreach (var project in _projects)
                {
                    if (MintStatus == "unminted" && project.IsMinted == true)
                    {
                        continue;
                    }
                    var embedMessage = await channel.GetMessageAsync(project.EmbedID);
                    var embedURL = embedMessage.GetJumpUrl();

                    EmbedFieldBuilder field = new EmbedFieldBuilder()
                        .WithName(project.Name)
                        .WithValue($"[Details]({embedURL})");

                    builder.AddField(field);
                }

                await Context.Interaction.FollowupAsync(embed: builder.Build(), ephemeral: true);
            }
            else
            {
                await Context.Interaction.FollowupAsync("No projects have been added yet", ephemeral: true);
            }
        }

        /// <summary>
        /// Responds with a Modal form for adding a new project
        /// </summary>
        /// <returns></returns>

        //[RequireRole(Settings.ProjectManagerRole)]
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
        
        [RequireRole(Settings.QueenRole)]
        [ComponentInteraction("delete-button")]
        private async Task DeleteProjectThroughButton()
        {
            int projectID = GetProjectIDFromEmbed(Context);

            await DeferAsync();
            var isConfirmed = await InteractionUtility.ConfirmAsync(_client, Context.Channel, TimeSpan.FromSeconds(10), "Are you sure you want to delete " + _projects.FirstOrDefault(x => x.Id == projectID).Name + "?");

            if (isConfirmed)
            {
                DeleteProject(projectID, (SocketMessageComponent) Context.Interaction);                
            }
        }

        /// <summary>
        /// Gives the project role
        /// </summary>
        /// <returns></returns>
        [ComponentInteraction("role-button")]
        private async Task AssignRole()
        {
            int projectID = GetProjectIDFromEmbed(Context);

            var project = _projects.FirstOrDefault(x => x.Id == projectID);

            await GiveOrRemoveRole(project.RoleID, Context.Interaction.User.Id);

            await DeferAsync();
        }

        /// <summary>
        /// Edit a project's information, (optional) role and channel name, and replaces the embed
        /// </summary>
        /// <returns></returns>
        
        ///[RequireRole(Settings.ProjectManagerRole)]
        [ComponentInteraction("edit-button")]
        private async Task EditProject()
        {
            var projectId = GetProjectIDFromEmbed(Context);

            await Context.Interaction.RespondWithModalAsync<ProjectModal>($"edit_project-{projectId}", RequestOptions.Default, builder => ModifyModal(builder, projectId));
        }

        #endregion

        #region Modal Interactions

        /// <summary>
        /// Creates a new Project according to the filled in modal and posts an embed with the details in the projects channel
        /// </summary>
        /// <param name="modal"></param>
        /// <returns></returns>
        [ModalInteraction("project_modal")]
        public async Task ModalResponse(ProjectModal modal)
        {
            var project = await CreateNewProject(modal);

            var embed = CreateProjectEmbed(project);
            var buttons = CreateProjectEmbedButtons();            

            ISocketMessageChannel socketMessageChannel = (ISocketMessageChannel)_guild.GetChannel(Settings.EmbedsChannelID);
            var message = await socketMessageChannel.SendMessageAsync(embed: embed.Build(), components: buttons.Build());

            project.EmbedID = message.Id;

            _projects.Add(project);

            SaveProjects();

            Task.Run(() => _loggingService.LogAsync(LogType.ProjectAdded, Context, project));
            await Context.Interaction.RespondAsync(project.Name + $" has been added to the Projects list. Find the embed [here]({message.GetJumpUrl()})", ephemeral: true);
        }

        /// <summary>
        /// Edits a Project according to the filled in modal and replaces its embed with the details in the projects channel
        /// </summary>
        /// <param name="id"></param>
        /// <param name="modal"></param>
        /// <returns></returns>
        [ModalInteraction("edit_project-*")]
        public async Task EditModalResponse(int id, ProjectModal modal)
        {
            var projectId = id;

            var project = _projects.FirstOrDefault(x => x.Id == Convert.ToInt32(projectId));

            var oldProjectDetails = CopyProjectDetails(project);

            project = EditProjectDetails(project, modal);

            SaveProjects();

            var embed = CreateProjectEmbed(project);
            var buttons = CreateProjectEmbedButtons();

            var channel = _guild.GetChannel(Settings.EmbedsChannelID);

            ISocketMessageChannel socketMessageChannel = (ISocketMessageChannel)channel;
            var message = await socketMessageChannel.ModifyMessageAsync(project.EmbedID, (x => { x.Embed = embed.Build(); x.Components = buttons.Build(); })); //await socketMessageChannel.GetMessageAsync(project.EmbedID); //SendMessageAsync(embed: embedBuiler.Build(), components: buttons.Build());

            if (oldProjectDetails.Name != project.Name)
            {
                var role = _guild.GetRole(project.RoleID);
                var projectChannel = _guild.GetChannel(project.ChannelID);

                await role.ModifyAsync(x => x.Name = project.Name);
                await projectChannel.ModifyAsync(x => x.Name = project.Name);
            }

            Task.Run(() => _loggingService.LogAsync(LogType.ProjectEdited, Context, project, oldProjectDetails));
            await DeferAsync();
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// Overwrites the json file
        /// </summary>
        private void SaveProjects()
        {
            var json = JsonConvert.SerializeObject(_projects, Formatting.Indented);
            _blobClient.Upload(BinaryData.FromString(json), overwrite: true);
        }

        /// <summary>
        /// Gets ProjectID from Embed (footer) data
        /// </summary>
        /// <param name="Context"></param>
        /// <returns>project id</returns>
        private int GetProjectIDFromEmbed(SocketInteractionContext Context)
        {
            var socketMessageComponent = (SocketMessageComponent)Context.Interaction;
            var embed = socketMessageComponent.Message.Embeds.FirstOrDefault();
            int projectID = int.Parse(embed.Footer.Value.Text);

            return projectID;
        }

        private Project CopyProjectDetails(Project project)
        {
            var copiedProject = new Project();
            var projectType = typeof(Project);

            foreach (var prop in projectType.GetProperties())
            {
                prop.SetValue(copiedProject, prop.GetValue(project));
            }
            return copiedProject;
        }

        /// <summary>
        /// Creates a new role for the Project
        /// </summary>
        /// <param name="projectName"></param>
        /// <returns>role id</returns>
        private async Task<ulong> CreateProjectRole(string projectName)
        {
            var role = await _guild.CreateRoleAsync(projectName);
            return role.Id;
        }

        /// <summary>
        /// Gives role if user doesn't have role yet, removes role if user does have role
        /// </summary>
        /// <param name="roleID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Create new Project (with role and channel) from the modal data
        /// </summary>
        /// <param name="modal"></param>
        /// <returns>project</returns>
        private async Task<Project> CreateNewProject(ProjectModal modal)
        {
            var project = new Project();
            project.Id = _projects.Any() ? _projects.Max(x => x.Id) + 1 : 1;
            project.Name = modal.Name;
            project.IsMinted = modal.IsMinted.ToLower().Equals("y") ? true : false;
            project.Description = modal.Description;
            project.Twitter = modal.Twitter;
            project.Discord = modal.Discord;
            project.RoleID = await CreateProjectRole(project.Name);
            project.ChannelID = await CreateProjectChannel(project.Name, project.RoleID);

            return project;
        }

        /// <summary>
        /// Edits project from the modal data
        /// </summary>
        /// <param name="modal"></param>
        /// <returns>project</returns>
        private Project EditProjectDetails(Project project, ProjectModal modal)
        {
            project.Name = modal.Name;
            project.IsMinted = modal.IsMinted.ToLower().Equals("y") ? true : false;
            project.Description = modal.Description;
            project.Twitter = modal.Twitter;
            project.Discord = modal.Discord;

            return project;
        }

        /// <summary>
        /// Creates a new channel for the Project, which is hidden by default and viewable only by SNIPERbot and people with the project role
        /// </summary>
        /// <param name="projectName"></param>
        /// <param name="roleID"></param>
        /// <returns>channel id</returns>
        private async Task<ulong> CreateProjectChannel(string projectName, ulong roleID)
        {
            var role = _guild.GetRole(roleID);
            var channel = await _guild.CreateTextChannelAsync(projectName, x => { x.CategoryId = Settings.ProjectCategoryID; });

            var bot_permissions = new OverwritePermissions();
            bot_permissions = bot_permissions.Modify(viewChannel: PermValue.Allow, manageChannel: PermValue.Allow, manageRoles: PermValue.Allow);

            var channelRole_permissions = new OverwritePermissions();
            channelRole_permissions = channelRole_permissions.Modify(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, addReactions: PermValue.Allow, readMessageHistory: PermValue.Allow);

            await channel.AddPermissionOverwriteAsync(_guild.GetRole(Settings.SniperBotRole), bot_permissions);
            await channel.AddPermissionOverwriteAsync(_guild.GetRole(Settings.EveryoneRole), OverwritePermissions.DenyAll(channel));
            await channel.AddPermissionOverwriteAsync(role, channelRole_permissions);

            return channel.Id;
        }

        /// <summary>
        /// Deletes role, channel and Project data itself
        /// </summary>
        /// <param name="projectID"></param>
        /// <param name="embedMessage"></param>
        /// <returns></returns>
        private async Task DeleteProject(int projectID, SocketMessageComponent embedMessage)
        {
            var project = _projects.FirstOrDefault(x => x.Id == projectID);

            var projectName = project.Name;

            var role = _guild.GetRole(project.RoleID);
            var channel = _guild.GetChannel(project.ChannelID);

            try
            {
                await role.DeleteAsync();
                await channel.DeleteAsync();

                _projects.Remove(project);

                SaveProjects();

                await embedMessage.Message.DeleteAsync();
                var followUp = await embedMessage.FollowupAsync($"Succesfully deleted {projectName}", ephemeral: true);
                Task.Run(() => _loggingService.LogAsync(LogType.ProjectDeleted, Context, project));

                await Task.Delay(5000);
                followUp.DeleteAsync();


            }
            catch (Exception e)
            {
                var followUp = await embedMessage.FollowupAsync($"Something went wrong trying to delete {projectName}" + e.Message, ephemeral: true);

                await Task.Delay(10000);
                followUp.DeleteAsync();
            }
        }

        /// <summary>
        /// The buttons for each project embed
        /// </summary>
        /// <returns></returns>
        private ComponentBuilder CreateProjectEmbedButtons()
        {
            var buttons = new ComponentBuilder()
                    .WithButton("Role", "role-button", ButtonStyle.Primary, new Emoji("✨"))
                    .WithButton("Edit", "edit-button", ButtonStyle.Secondary)
                    .WithButton("Delete", "delete-button", ButtonStyle.Danger);

            return buttons;
        }

        /// <summary>
        /// The project embed
        /// </summary>
        /// <param name="project"></param>
        /// <returns></returns>
        private EmbedBuilder CreateProjectEmbed(Project project)
        {
            var footer = new EmbedFooterBuilder()
                .WithText(project.Id.ToString());

            var embedBuilder = new EmbedBuilder()
                        .WithTitle(project.Name)
                        .WithDescription(project.Description)
                        .WithColor(Color.Blue)
                        .WithFooter(footer)
                        .WithCurrentTimestamp()
                        .WithAuthor(Context.User);

            var mintedField = new EmbedFieldBuilder()
                .WithName("Minted?")
                .WithValue(project.IsMinted ? "Yes" : "No");
            embedBuilder.AddField(mintedField);

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

            return embedBuilder;
        }

        /// <summary>
        /// Prefills the ProjectModal, used for editing an existing project
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="projectId"></param>
        private void ModifyModal(ModalBuilder obj, int projectId)
        {
            var project = _projects.First(x => x.Id == projectId);
            var projectModal = new ProjectModal();
            projectModal.Name = project.Name;
            projectModal.IsMinted = project.IsMinted ? "Y" : "N";
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
                    RequiredInputAttribute requiredAttribute = (RequiredInputAttribute)prop.GetCustomAttributes(typeof(RequiredInputAttribute), false)[0];
                    componentBuilder.Required = requiredAttribute.IsRequired;

                    ModalTextInputAttribute customIDAttribute = (ModalTextInputAttribute)prop.GetCustomAttributes(typeof(ModalTextInputAttribute), false)[0];
                    componentBuilder.CustomId = customIDAttribute.CustomId;
                    componentBuilder.Style = customIDAttribute.Style;
                    componentBuilder.Placeholder = customIDAttribute.Placeholder;
                    componentBuilder.MaxLength = customIDAttribute.MaxLength;

                    InputLabelAttribute inputLabelAttribute = (InputLabelAttribute)prop.GetCustomAttributes(typeof(InputLabelAttribute), false)[0];
                    componentBuilder.Label = inputLabelAttribute.Label;

                    componentBuilder.Value = (string)prop.GetValue(projectModal);
                    obj.AddTextInput(componentBuilder);
                }
            }
            obj.Build();
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

        [ModalTextInput("project_minted", placeholder: "Y/N", maxLength: 1)]
        [RequiredInput(true)]
        [InputLabel("Minted?")]
        public string IsMinted { get; set; }

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
