﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCBot
{
    /// <summary>
    /// Represents a command as described in the config file
    /// </summary>
    class Audio
    {
        /// <summary>
        /// Command user will type to play soundbite
        /// </summary>
        public string Command { get; }
        public string[] Paths { get; set; }
        public string[] Alias { get; set; }
        public string Description { get; set; }
        public Audio(string command, string[] path, string[] alias = null, string description = null)
        {
            Paths = path;
            Command = command;
            Alias = alias;
            Description = description;
        }

        public override string ToString()
        {
            return string.Format("{0} ([{1}]): {2}: {3}", Command, Alias, Description, Paths);
        }
    }
}
