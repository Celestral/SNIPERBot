using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using SNIPERBot.Utils;
using System.Reflection;

namespace SNIPERBot
{
    public class LoggingService
    {
        private static DiscordSocketClient _client;
        private static ISocketMessageChannel _loggingChannel;

        public LoggingService(DiscordSocketClient client)
        {
            _client = client;
            _loggingChannel = (ISocketMessageChannel)_client.GetGuild(Settings.GuildId).GetChannel(Settings.LoggingChannelID);
        }

        public Task LogAsync(LogType logType, SocketInteractionContext context, Project project, Project? previousDetails = null)
        {
            switch (logType)
            {
                case LogType.ProjectAdded:
                    LogProjectAdded(context, project);
                    break;
                case LogType.ProjectDeleted:
                    LogProjectDeleted(context, project);
                    break;
                case LogType.ProjectEdited:
                    LogProjectEdited(context, project, previousDetails);
                    break;
                default:
                    break;
            }
            return Task.CompletedTask;
        }

        private async Task LogProjectAdded(SocketInteractionContext context, Project project)
        {
            var embed = new EmbedBuilder()
                .WithTitle("PROJECT ADDED")
                .WithDescription("A project has been added by " + context.User + " with the following details")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp();

            var projectType = typeof(Project);

            foreach (PropertyInfo prop in projectType.GetProperties())
            {
                var propertyValue = prop.GetValue(project);

                var field = new EmbedFieldBuilder()
                    .WithName(prop.Name)
                    .WithValue(!string.IsNullOrEmpty(propertyValue.ToString()) ? propertyValue : "Not filled in");

                embed.AddField(field);
            }

            await _loggingChannel.SendMessageAsync(embed: embed.Build(), allowedMentions: AllowedMentions.None);
        }

        private async Task LogProjectDeleted(SocketInteractionContext context, Project project)
        {
            var embed = new EmbedBuilder()
                .WithTitle("PROJECT DELETION")
                .WithDescription("A project has been deleted by " + context.User + " with the following details")
                .WithColor(Color.Red)
                .WithCurrentTimestamp();

            var projectType = typeof(Project);

            foreach (PropertyInfo prop in projectType.GetProperties())
            {
                var propertyValue = prop.GetValue(project);

                var field = new EmbedFieldBuilder()
                    .WithName(prop.Name)
                    .WithValue(!string.IsNullOrEmpty(propertyValue.ToString()) ? propertyValue : "Not filled in");

                embed.AddField(field);
            }

            await _loggingChannel.SendMessageAsync(embed: embed.Build(), allowedMentions: AllowedMentions.All);
        }

        private async Task LogProjectEdited(SocketInteractionContext context, Project newDetails, Project previousDetails)
        {
            var embed = new EmbedBuilder()
                .WithTitle("PROJECT EDITED")
                .WithDescription("A project has been edited by " + context.User.Mention + " with the following changes")
                .WithColor(Color.Orange)
                .WithCurrentTimestamp();

            var projectType = typeof(Project);

            foreach (PropertyInfo prop in projectType.GetProperties())
            {
                if (!String.Equals(prop.GetValue(newDetails).ToString(), prop.GetValue(previousDetails).ToString()))
                {
                    var oldValue = prop.GetValue(previousDetails);
                    var newValue = prop.GetValue(newDetails);

                    var previousField = new EmbedFieldBuilder()
                        .WithName(prop.Name + ": previous value")
                        .WithValue(!string.IsNullOrEmpty(oldValue.ToString()) ? oldValue : "Not filled in")
                        .WithIsInline(true);

                    var newField = new EmbedFieldBuilder()
                        .WithName(prop.Name + ": new value")
                        .WithValue(!string.IsNullOrEmpty(newValue.ToString()) ? newValue : "Not filled in")
                        .WithIsInline(true);

                    var spacerField = new EmbedFieldBuilder();

                    embed.AddField("\u200b", '\u200b');
                    embed.AddField(previousField);
                    embed.AddField(newField);
                }
            }

            await _loggingChannel.SendMessageAsync(embed: embed.Build(), allowedMentions: AllowedMentions.All);
        }
    }

    public enum LogType { ProjectAdded, ProjectDeleted, ProjectEdited }
}
