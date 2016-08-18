﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using System.Diagnostics;
using System.Threading;
using System.IO;
using Discord.Commands;
using System.Reflection;

namespace DCBot
{
    class Program
    {
        static void Main(string[] args)
        {
            new Program().Start();
        }

        private bool audioPlaying = false; //This seems like a dirty hack.
        private bool IsRunningOnMono { get; } = (Type.GetType("Mono.Runtime") != null);

        DiscordClient _client = new DiscordClient(x => { x.LogLevel = LogSeverity.Info; });
        /// <summary>
        /// Method to start the bot.
        /// </summary>
        public void Start()
        {
            Initializer config = new Initializer().run();
            Queue<Command> audioQueue = new Queue<Command>();

            _client.Log.Message += (s, e) => Console.WriteLine($"[{e.Severity}] {e.Source}: {e.Message}");

            _client.UsingAudio(x => // Opens an AudioConfigBuilder so we can configure our AudioService
            {
                x.Mode = AudioMode.Outgoing; // Tells the AudioService that we will only be sending audio
            });

            //var _vService = _client.GetService<AudioService>();

            _client.UsingCommands(x =>
            {
                x.PrefixChar = config.CommandChar;
                x.HelpMode = HelpMode.Public;
            });

            createCommands(config, _client, audioQueue);

            createFileSystemWatcher(config);

            _client.ExecuteAndWait(async () =>
            {
                Console.WriteLine("Connecting to Discord...");
                try { await _client.Connect(config.TOKEN); }
                catch (Discord.Net.HttpException e)
                {
                    Console.WriteLine("Unable to connect to Discord. Have you registered a new bot? Is your token correct? \nPress enter to exit");
                    Console.ReadLine();
                    Environment.Exit(0);
                }
                catch (Exception e) { Console.WriteLine(e); }
                Console.WriteLine("Connected!");
            });
        }

        /// <summary>
        /// Create a FileSystemWatcher to watch for changes in the config file to trigger a reload of the commands and audio
        /// </summary>
        /// <param name="config">Instance of Initializer class</param>
        /// <returns>FileSystemWatcher for the config file</returns>
        private FileSystemWatcher createFileSystemWatcher(Initializer config)
        {
            FileSystemWatcher fsw = new FileSystemWatcher();
            fsw.Path = Directory.GetCurrentDirectory();
            fsw.NotifyFilter = NotifyFilters.LastWrite;
            fsw.Filter = "config.json";
            fsw.Changed += new FileSystemEventHandler(OnChange);
            fsw.EnableRaisingEvents = true;
            return fsw;
        }

        /// <summary>
        /// Called by the FileSystemWatcher when a change in the config file is detected.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        public void OnChange(object source, FileSystemEventArgs e)
        {
            // Because of how Discord.Net is structured (as far as I understand. I could be wrong) it is impossible to remove commands once added.
            // Therefore, if a change is detected in the config file, restart the whole application.
            // Messy, but it works.

            _client.Log.Info(null, "Change in config detected; restarting...");
            var fileName = Assembly.GetExecutingAssembly().Location;
            Process.Start(fileName);
            Environment.Exit(0);
        }

        /// <summary>
        /// Create the commands from the commands in the config file for Discord.Net
        /// </summary>
        private void createCommands(Initializer config, DiscordClient _client, Queue<Command> audioQueue)
        {
            foreach (Audio command in config.commands)
            {
                CommandBuilder cb = _client.GetService<CommandService>().CreateCommand(command.Command);
                if (command.Alias != null) { cb.Alias(command.Alias); }
                if (command.Description != null) { cb.Description(command.Description); }
                cb.Do(e =>
               {
                   if (e.User.VoiceChannel != null)
                   {
                       _client.Log.Info(null, command.Path);
                       addAudioToQueue(command.Path, e.User.VoiceChannel, audioQueue);
                       if (!audioPlaying) { sendAudioQueue(audioQueue); }
                       Console.WriteLine(string.Format("Received command: {1} from: {0} in {2} on {3}", e.User, command.Command, e.Channel, e.Server));
                   }

               });
            }
            _client.GetService<CommandService>().CreateCommand("ayy")
                .Description("Test if the bost is receiving messages")
                .Do(e =>
                {
                    e.Channel.SendMessage("lmao");
                    _client.Log.Info("Ayy received", null);
                });
        }

        /// <summary>
        /// Send audio to the current _vClient channel
        /// </summary>
        /// <param name="path">Path of the .wav file to send</param>
        private void send(string path, Command current, IAudioClient _vClient)
        {
            int blockSize = 3840; // The size of bytes to read per frame; 1920 for mono
            try
            {
                using (FileStream f = File.Open(path, FileMode.Open))
                {
                    byte[] buffer = new byte[blockSize];
                    while (f.Read(buffer, 0, buffer.Length) > 0)
                    {
                        _vClient.Send(buffer, 0, buffer.Length);
                    }

                    _vClient.Wait();
                }
            }
            catch (FileNotFoundException e) { Console.WriteLine(string.Format("Could not find file; ensure {0} is correct path", path)); }
            catch (Exception e) { Console.WriteLine(e); }
        }

        private void addAudioToQueue(string path, Channel voiceChannel, Queue<Command> audioQueue)
        {
            audioQueue.Enqueue(new Command(path, voiceChannel));
        }

        private async void sendAudioQueue(Queue<Command> audioQueue)
        {
            IAudioClient _vClient = null;
            while (audioQueue.Count > 0)
            {
                audioPlaying = true;
                Command current = audioQueue.Dequeue();
                try { _vClient = await current.VoiceChannel.JoinAudio(); } catch (Exception e) { Console.WriteLine(e); }
                send(current.Path, current, _vClient);
            }
            await _vClient.Disconnect();
            audioPlaying = false;
        }

        /// <summary>
        /// Represents a single command from a user to add to the audio queue.
        /// </summary>
        class Command
        {
            public string Path { get; }
            public Channel VoiceChannel { get; }
            public Command(string path, Channel voiceChannel)
            {
                Path = path;
                VoiceChannel = voiceChannel;
            }
        }
    }
}
