using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIPERBot
{
    public class Project
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Twitter { get; set; }
        public string Discord { get; set; }

        public string EmbedURL { get; set; }
        public ulong ChannelID { get; set; }
        public ulong RoleID { get; set; }
    }
}
