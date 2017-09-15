﻿using Hazel;
using Hazel.Udp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SM64O
{
    public partial class Form1 : Form
    {
        public static ConnectionListener listener = null;
        public static Connection connection = null;
        public static Client[] playerClient = new Client[23];

        private ListBox _dynamicChatbox;
        private List<string> _bands = new List<string>();

        private bool _chatEnabled = true;
        private int _updateRate = 16;

        private IEmulatorAccessor _memory;
        private const int MinorVersion = 3;
        private const int MajorVersion = 1;

        private const int HandshakeDataLen = 28;
        private const int MaxChatLength = 24;

        private UPnPWrapper _upnp;

        public Form1()
        {
            listener = null;
            connection = null;
            playerClient = new Client[23];

            _upnp = new UPnPWrapper();
            _upnp.Initialize();

            InitializeComponent();

            string[] fileEntries = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "/Patches/");

            foreach (var file in fileEntries)
            {
                byte[] buffer = File.ReadAllBytes(file);

                //File.Copy(AppDomain.CurrentDomain.BaseDirectory + "/Patches/" + Path.GetFileName(file), AppDomain.CurrentDomain.BaseDirectory + "/Ressources/" + Path.GetFileName(file));

                if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "/Ressources/" + Path.GetFileName(file)))
                {
                    File.Delete(AppDomain.CurrentDomain.BaseDirectory + "/Ressources/" + Path.GetFileName(file));
                }

                for (int i = 0; i < buffer.Length; i += 4)
                {
                    byte[] newBuffer = buffer.Skip(i).Take(4).ToArray();
                    newBuffer = newBuffer.Reverse().ToArray();
                    using (var fs = new FileStream(AppDomain.CurrentDomain.BaseDirectory + "/Ressources/" + Path.GetFileName(file), FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        fs.Seek(i, SeekOrigin.Current);
                        fs.Write(newBuffer, 0, newBuffer.Length);
                    }
                }

                if (File.Exists("bans.txt"))
                {
                    foreach (var line in File.ReadAllLines("bans.txt"))
                    {
                        _bands.Add(line);
                    }
                }

            }

            // TODO: Change this according to OS
            _memory = new WindowsEmulatorAccessor();

            this.Text = string.Format("SM64 Online Tool v{0}.{1}", MajorVersion, MinorVersion);
        }

        private void die(string msg)
        {
            MessageBox.Show(null, msg, "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }

        private string getRomName()
        {
            string romname = null;
            byte[] buffer = new byte[64];

            switch (comboBox1.Text)
            {
                case "Project64":
                    // Super Mario 64 (u) - Project 64 v2.3.3
                    string windowName = _memory.WindowName;


                    for (int i = windowName.Length - 1; i >= 0; i--)
                    {
                        if (windowName[i] == '-')
                        {
                            romname = windowName.Substring(0, i).Trim();
                            break;
                        }
                    }
                    break;
                case "Mupen64":
                    string wndname = _memory.WindowName;

                    for (int i = wndname.Length - 1; i >= 0; i--)
                    {
                        if (wndname[i] == '-')
                        {
                            romname = wndname.Substring(0, i).Trim();
                            break;
                        }
                    }
                    break;
                case "Nemu64":
                    _memory.ReadMemoryAbs(_memory.MainModuleAddress + 0x3C8A10C, buffer, buffer.Length);

                    romname = Encoding.ASCII.GetString(buffer, 0, Array.IndexOf(buffer, (byte)0));
                    break;
            }

            return romname;
        }

        private void sendAllChat(string message)
        {
            string name = "HOST";

            if (!string.IsNullOrWhiteSpace(usernameBox.Text))
                name = usernameBox.Text;

            if (message.Length > MaxChatLength)
                message = message.Substring(0, MaxChatLength);

            if (name.Length > MaxChatLength)
                name = name.Substring(0, MaxChatLength);

            byte[] messageBytes = Encoding.ASCII.GetBytes(message);
            byte[] usernameBytes = Encoding.ASCII.GetBytes(name);


            byte[] payload = new byte[1 + messageBytes.Length + 1 + usernameBytes.Length];

            payload[0] = (byte)messageBytes.Length;

            Array.Copy(messageBytes, 0, payload, 1, messageBytes.Length);

            payload[messageBytes.Length + 1] = (byte)usernameBytes.Length;

            Array.Copy(usernameBytes, 0, payload, 1 + messageBytes.Length + 1, usernameBytes.Length);

            if (listener != null)
            {
                for (int i = 0; i < playerClient.Length; i++)
                    if (playerClient[i] != null)
                        playerClient[i].SendBytes(PacketType.ChatMessage, payload, SendOption.Reliable, origin: "sendAllChat");
            }
            else
            {
                connection.SendBytes(PacketType.ChatMessage, payload, SendOption.Reliable, origin: "sendAllChat");
            }


            if (_chatEnabled && listener != null)
            {
                Characters.setMessage(message, _memory);

                if (_dynamicChatbox != null)
                {
                    _dynamicChatbox.Items.Insert(0, string.Format("{0}: {1}", "HOST", message));

                    if (_dynamicChatbox.Items.Count > 10)
                        _dynamicChatbox.Items.RemoveAt(10);
                }
            }
        }

        private void sendChatTo(string message, Connection conn)
        {
            string name = "HOST";

            if (!string.IsNullOrWhiteSpace(usernameBox.Text))
                name = usernameBox.Text;

            if (message.Length > MaxChatLength)
                message = message.Substring(0, MaxChatLength);

            if (name.Length > MaxChatLength)
                name = name.Substring(0, MaxChatLength);

            byte[] messageBytes = Encoding.ASCII.GetBytes(message);
            byte[] usernameBytes = Encoding.ASCII.GetBytes(name);


            byte[] payload = new byte[1 + messageBytes.Length + 1 + usernameBytes.Length];

            payload[0] = (byte) messageBytes.Length;
            
            Array.Copy(messageBytes, 0, payload, 1, messageBytes.Length);

            payload[messageBytes.Length + 1] = (byte) usernameBytes.Length;

            Array.Copy(usernameBytes, 0, payload, 1 + messageBytes.Length + 1, usernameBytes.Length);

            conn.SendBytes(PacketType.ChatMessage, payload, SendOption.Reliable, origin: "sendChatTo");
        }
        
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                if (comboBox1.Text == "Project64")
                {
                    _memory.Open("Project64");
                    if (_memory.BaseAddress == 0)
                    {
                        die("Your version of Project64 is unsupported. Please use version 2.3");
                        return;
                    }
                }

                if (comboBox1.Text == "Nemu64")
                {
                    _memory.Open("Nemu64");
                }
                if (comboBox1.Text == "Mupen64")
                {
                    _memory.Open("Mupen64");
                }
            }
            catch (IndexOutOfRangeException)
            {
                die("Your emulator is not running!");
                return;
            }

            try
            {
                if (checkBox1.Checked)
                {
                    int port = (int) numericUpDown2.Value;

                    if (_upnp.UPnPAvailable)
                    {
                        // TODO: Add info to toolstrip
                        _upnp.AddPortRule(port, false, "SM64O");
                        textBox5.Text = _upnp.GetExternalIp();
                    }


                    panel2.Enabled = true;
                    listener = new UdpConnectionListener(new NetworkEndPoint(IPAddress.Any, port));
                    listener.NewConnection += NewConnectionHandler;
                    listener.Start();
                    
                    insertChatBox();

                    playerCheckTimer.Start();

                    Characters.setMessage("server created", _memory);
                }
                else
                {
                    byte[] payload = new byte[HandshakeDataLen];
                    payload[0] = (byte)MinorVersion;
                    payload[1] = (byte)this.comboBox2.SelectedIndex;
                    payload[2] = (byte) MajorVersion;

                    string username = usernameBox.Text;

                    if (string.IsNullOrWhiteSpace(username))
                    {
                        username = getRandomUsername();
                        usernameBox.Text = username;
                    }

                    usernameBox.Enabled = false;
                    
                    byte[] usernameBytes = Encoding.ASCII.GetBytes(username);
                    int len = usernameBytes.Length;
                    if (len > 24) // Arbitrary max length
                        len = 24;

                    payload[3] = (byte) len;
                    Array.Copy(usernameBytes, 0, payload, 4, len);

                    IPAddress target = null;
                    bool isIp6 = false;

                    string text = textBox5.Text.Trim();

                    if (!IPAddress.TryParse(text, out target))
                    {
                        // Maybe DNS?
                        try
                        {
                            var dns = Dns.GetHostEntry(text);
                            if (dns.AddressList.Length > 0)
                                target = dns.AddressList[0];
                            else throw new SocketException();
                        }
                        catch (SocketException ex)
                        {
                            MessageBox.Show(this,
                                "Could not connect to server:\n" + ex.Message);
                            Application.Exit();
                            return;
                        }
                    }

                    isIp6 = target.AddressFamily == AddressFamily.InterNetworkV6;

                    NetworkEndPoint endPoint = new NetworkEndPoint(target, (int) numericUpDown2.Value, isIp6 ? IPMode.IPv6 : IPMode.IPv4);
                    connection = new UdpClientConnection(endPoint);
                    connection.DataReceived += DataReceived;
                    connection.Connect(payload, 3000);
                    connection.Disconnected += ConnectionOnDisconnected;

                    playersOnline.Text = "Chat Log:";
                }
            }
            catch (HazelException ex)
            {
                string msg = "Could not connect/start server!";
                if (ex.InnerException != null)
                    msg = "Could not connect/start server:\n" + ex.InnerException.Message;
                die(msg);
                return;
            }
            catch (Exception ex)
            {
                // TODO: add logging 
                die("Could not connect/start server:\n" + ex.Message + "\n\nMore info:\n" + ex);
                return;
            }

            checkBox1.Enabled = false;
            /*
            Thread tick = new Thread(tick_thread);
            tick.IsBackground = true;
            tick.Start();
            */

            timer1_Tick();
            button1.Enabled = false;

            numericUpDown1.Enabled = true;

            chatBox.Enabled = true;
            button3.Enabled = true;
            numericUpDown2.Enabled = false;

            textBox5.ReadOnly = true;

            comboBox1.Enabled = false;
            //comboBox2.Enabled = false;

            checkBox2.Enabled = true;

            button4.Enabled = true;

            Characters.setCharacter(comboBox2.SelectedItem.ToString(), _memory);

            loadPatches();

            toolStripStatusLabel1.Text = "Loaded ROM " + getRomName();

            if (checkBox1.Checked)
            {
                writeValue(new byte[] { 0x00, 0x00, 0x00, 0x01 }, 0x365FFC);
            }

            Settings sets = new Settings();

            sets.LastIp = checkBox1.Checked ? "" : textBox5.Text;
            sets.LastPort = (int) numericUpDown2.Value;
            sets.Username = usernameBox.Text;

            sets.LastEmulator = comboBox1.SelectedIndex;
            sets.LastCharacter = comboBox2.SelectedIndex;

            Settings.Save(sets, "settings.xml");
        }

        private void loadPatches()
        {
            string[] fileEntries = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "/Ressources/");

            foreach (var file in fileEntries)
            {
                string fname = Path.GetFileName(file);
                int offset;

                if (int.TryParse(fname, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset))
                {
                    byte[] buffer = File.ReadAllBytes(file);
                    _memory.WriteMemory(offset, buffer, buffer.Length);
                }
                else if (fname.Contains('.'))
                {
                    // Treat as Regex
                    int separator = fname.LastIndexOf(".", StringComparison.Ordinal);
                    string regexPattern = fname.Substring(0, separator);
                    string address = fname.Substring(separator + 1, fname.Length - separator - 1);
                    string romname = getRomName();

                    regexPattern = regexPattern.Replace("@", "\\");
                    bool invert = false;

                    if (regexPattern[0] == '!')
                    {
                        regexPattern = regexPattern.Substring(1);
                        invert = true;
                    }

                    if (romname == null)
                        continue;

                    bool isMatch = Regex.IsMatch(romname, regexPattern,
                        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

                    if ((isMatch && !invert) || (!isMatch && invert))
                    {
                        offset = int.Parse(address, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                        byte[] buffer = File.ReadAllBytes(file);
                        _memory.WriteMemory(offset, buffer, buffer.Length);
                    }
                }

                
            }
        }

        private void ConnectionOnDisconnected(object sender, DisconnectedEventArgs disconnectedEventArgs)
        {
            removeAllPlayers();
            die("You have been disconnected!");
        }

        private void NewConnectionHandler(object sender, NewConnectionEventArgs e)
        {
            try
            {
                if (_bands.Contains(e.Connection.EndPoint.ToString()))
                {
                    sendChatTo("banned", e.Connection);
                    e.Connection.Close();
                    return;
                }

                for (int i = 0; i < playerClient.Length; i++)
                {
                    if (playerClient[i] == null)
                    {
                        playerClient[i] = new Client(e.Connection);
                        playerClient[i].Id = i;
                        playerClient[i].Name = "anon";

                        e.Connection.DataReceived += DataReceivedHandler;
                        e.Connection.Disconnected += client_Disconnected;

                        int playerIDB = i + 2;
                        byte[] playerID = new byte[] {(byte) playerIDB};
                        Thread.Sleep(500);
                        playerClient[i].SendBytes(PacketType.MemoryWrite, playerID, origin: "NewConnectionHandler");
                        string character = "custom ";

                        if (e.HandshakeData != null && e.HandshakeData.Length > 3)
                        {
                            byte[] handshakeData = new byte[HandshakeDataLen];

                            Array.Copy(e.HandshakeData, 3, handshakeData, 0, Math.Min(e.HandshakeData.Length - 3, HandshakeDataLen));

                            if (handshakeData.Length >= 2)
                            {
                                byte verIndex = handshakeData[0];
                                byte charIndex = handshakeData[1];
                                playerClient[i].MinorVersion = verIndex;
                                playerClient[i].CharacterId = charIndex;
                                character = getCharacterName(charIndex);
                            }
                            if (e.HandshakeData.Length >= 3)
                            {
                                playerClient[i].MajorVersion = handshakeData[2];
                            }
                            if (e.HandshakeData.Length >= 4)
                            {
                                byte usernameLen = handshakeData[3];
                                string name = Encoding.ASCII.GetString(handshakeData, 4, usernameLen);
                                playerClient[i].Name = name;
                            }

                            playerClient[i].CharacterName = character;
                        }

                        listBox1.Items.Add(playerClient[i]);

                        string msg = string.Format("{0} joined", playerClient[i].Name);
                        if (msg.Length > MaxChatLength)
                            msg = msg.Substring(0, 24);

                        sendAllChat(msg);
                        return;
                    }
                }

                // Server is full
                sendChatTo("server is full", e.Connection);
                e.Connection.Close();
            }
            finally
            {
                playersOnline.Text = "Players Online: " + playerClient.Count(c => c != null) + "/" + playerClient.Length;
            }
        }

        private void client_Disconnected(object sender, DisconnectedEventArgs e)
        {
            Connection conn = (Connection)sender;
            for (int i = 0; i < playerClient.Length; i++)
            {
                if (playerClient[i] != null && playerClient[i].Connection == conn)
                {
                    removePlayer(i);
                    break;
                }
            }
        }

        private void removePlayer(int player)
        {
            if (player == -1 || playerClient[player] == null) return;

            string msg = string.Format("{0} left", playerClient[player].Name);
            if (msg.Length > MaxChatLength)
                msg = msg.Substring(0, 24);
            
            listBox1.Items.Remove(playerClient[player]);

            playerClient[player].Connection.DataReceived -= DataReceivedHandler;
            playerClient[player] = null;

            playersOnline.Text = "Players Online: " + playerClient.Count(c => c != null) + "/" + playerClient.Length;

            const int playersPositionsStart = 0x36790C;
            const int playerPositionsSize = 0x100;
            // 0xc800
            byte[] buffer = new byte[] { 0x00, 0x00, 0x00, 0xFD };
            _memory.WriteMemory(playersPositionsStart + playerPositionsSize * player, buffer, buffer.Length);

            sendAllChat(msg);
        }

        private void DataReceivedHandler(object sender, Hazel.DataReceivedEventArgs e)
        {
            var conn = (Connection) sender;

            int id = getClient(conn);
            if (id != -1)
                playerClient[id].LastUpdate = DateTime.Now;

            ReceivePacket(conn, e.Bytes);

            e.Recycle();
        }

        private void DataReceived(object sender, Hazel.DataReceivedEventArgs e)
        {
            if (e.Bytes.Length == 0)
                return;

            ReceivePacket((Connection) sender, e.Bytes);
            e.Recycle();
        }

        private void ReceivePacket(Connection sender, byte[] data)
        {
            NetworkLogger.Singleton.Value.LogIncomingPacket(data, "ReceivePacket");

            PacketType type = (PacketType) data[0];
            byte[] payload = data.Skip(1).ToArray();

            switch (type)
            {
                case PacketType.MemoryWrite:
                    ReceiveRawMemory(payload);
                    break;
                case PacketType.ChatMessage:
                    ReceiveChatMessage(payload);
                    break;
                case PacketType.CharacterSwitch:
                    byte newCharacter = payload[0];
                    string newCharName = getCharacterName(newCharacter);
                    int id = getClient(sender);

                    if (id != -1)
                    {
                        playerClient[id].CharacterId = newCharacter;
                        playerClient[id].CharacterName = newCharName;

                        //int oldPos = listBox1.Items.IndexOf(playerClient[id]);
                        listBox1.Refresh();
                    }

                    break;
            }
        }

        private void ReceiveRawMemory(byte[] data)
        {
            if (data.Length == 1)
            {
                _memory.WriteMemory(0x367703, data, data.Length);
                return;
            }

            int offset = BitConverter.ToInt32(data, 0);
            if (offset < 0x365000 || offset > 0x365000 + 8388608) // Only allow 8 MB N64 RAM addresses
                return; //  Kaze: retrict it to 80365ff0 to 80369000

            byte[] buffer = new byte[data.Length - 4];
            data.Skip(4).ToArray().CopyTo(buffer, 0);
            
            _memory.WriteMemory(offset, buffer, buffer.Length);
        }

        private void ReceiveChatMessage(byte[] data)
        {
            if (connection == null) // We are the host
                for (int i = 0; i < Form1.playerClient.Length; i++)
                {
                    if (Form1.playerClient[i] != null)
                        Form1.playerClient[i].SendBytes(PacketType.ChatMessage, data, origin: "chatResend");
                }

            if (!_chatEnabled) return;

            string message = "";
            string sender = "";

            int msgLen = data[0];
            message = Encoding.ASCII.GetString(data, 1, msgLen);
            int nameLen = data[msgLen + 1];
            sender = Encoding.ASCII.GetString(data, msgLen + 2, nameLen);

            Characters.setMessage(message, _memory);

            if (listener == null) // We're not the host
            {
                listBox1.Items.Insert(0, string.Format("{0}: {1}", sender, message));
                
                if (listBox1.Items.Count > 10)
                    listBox1.Items.RemoveAt(10);
            }

            if (_dynamicChatbox != null)// We're the host
            {
                _dynamicChatbox.Items.Insert(0, string.Format("{0}: {1}", sender, message));

                if (_dynamicChatbox.Items.Count > 10)
                    _dynamicChatbox.Items.RemoveAt(10);
            }
        }

        public void writeValue(byte[] buffer, int offset)
        {
            buffer = buffer.Reverse().ToArray();
            _memory.WriteMemory(offset, buffer, buffer.Length);
        }

        private void timer1_Tick()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    sendAllBytes();

                    await Task.Delay(_updateRate);
                }
            });
        }

        // not touching this
        public void sendAllBytes()
        {
            int freeRamLength = getRamLength(0x367400);

            int[] offsetsToReadFrom = new int[freeRamLength];
            int[] offsetsToWriteToLength = new int[freeRamLength];
            int[] offsetsToWriteTo = new int[freeRamLength];

            byte[] originalBuffer = new byte[freeRamLength];
            _memory.ReadMemory(0x367400, originalBuffer, originalBuffer.Length);

            byte[] buffer = originalBuffer;

            for (int i = 0; i < freeRamLength; i += 12)
            {
                buffer = buffer.Skip(0 + i).Take(4).ToArray();
                long wholeAddress = BitConverter.ToInt32(buffer, 0);
                wholeAddress -= 0x80000000;
                offsetsToReadFrom[i + 0] = (int)wholeAddress;
                buffer = originalBuffer;

                buffer = buffer.Skip(4 + i).Take(4).ToArray();
                int wholeAddress1 = BitConverter.ToInt32(buffer, 0);
                offsetsToWriteToLength[i + 4] = (int)wholeAddress1;
                buffer = originalBuffer;

                buffer = buffer.Skip(8 + i).Take(4).ToArray();
                long wholeAddress2 = BitConverter.ToInt32(buffer, 0);
                wholeAddress2 -= 0x80000000;
                offsetsToWriteTo[i + 8] = (int)wholeAddress2;

                buffer = originalBuffer;

                if (listener != null)
                {
                    for (int p = 0; p < playerClient.Length; p++)
                    {
                        if (playerClient[p] != null)
                            readAndSend(offsetsToReadFrom[i], offsetsToWriteTo[i + 8], offsetsToWriteToLength[i + 4], playerClient[p]);
                    }
                }
                else
                    readAndSend(offsetsToReadFrom[i], offsetsToWriteTo[i + 8], offsetsToWriteToLength[i + 4], connection);

            }
        }

        public int getRamLength(int offset)
        {
            for (int i = 0; i < 0x1024; i += 4)
            {
                byte[] buffer = new byte[4];

                _memory.ReadMemory(offset + i, buffer, buffer.Length);
                buffer = buffer.Reverse().ToArray();

                if (BitConverter.ToString(buffer) == "00-00-00-00")
                {
                    return i;
                }
            }
            return 0;
        }

        public void readAndSend(int offsetToRead, int offsetToWrite, int howMany, Connection conn)
        {
            byte[] buffer = new byte[howMany];
            byte[] writeOffset = BitConverter.GetBytes(offsetToWrite);

            _memory.ReadMemory(offsetToRead, buffer, buffer.Length);

            byte[] finalOffset = new byte[howMany + 4];
            writeOffset.CopyTo(finalOffset, 0);
            buffer.CopyTo(finalOffset, 4);
            conn.SendBytes(PacketType.MemoryWrite, finalOffset, origin: "readAndSend");

            Debug.WriteLine("Sending packet of length " + howMany);
        }

        private void textBox5_TextChanged(object sender, EventArgs e)
        {
            if (textBox5.Text != "")
            {
                button1.Enabled = true;
            }
            else
            {
                button1.Enabled = false;
            }
            if (textBox5.Text == "toad")
            {
                toadTimer.Enabled = true;
                PictureBox box = new PictureBox();
                box.Image = SM64O.Properties.Resources.toad;
                box.SizeMode = PictureBoxSizeMode.StretchImage;
                box.Size = new Size(this.Size.Width, this.Size.Height);

                foreach (Control control in this.Controls)
                {
                    control.Text = "toad";
                    control.Enabled = false;
                    control.Visible = false;
                }

                this.Controls.Add(box);

                IntPtr Hicon = SM64O.Properties.Resources.toad.GetHicon();
                Icon newIcon = Icon.FromHandle(Hicon);

                this.Icon = newIcon;
                this.Text = "toad";

                System.Media.SoundPlayer player = new System.Media.SoundPlayer(SM64O.Properties.Resources.herewego);
                player.Play();
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            _updateRate = (int)numericUpDown1.Value;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                textBox5.Text = "";
                usernameBox.Text = "";
                usernameBox.Enabled = false;

                button1.Enabled = true;

                button1.Text = "Create Server!";
                usernameBox.Enabled = false;
                panel2.Enabled = true;
                textBox5.ReadOnly = true;
                button1.Enabled = true;

                if (_upnp.UPnPAvailable)
                    textBox5.Text = _upnp.GetExternalIp();
                else textBox5.Text = "";
            }
            else
            {
                usernameBox.Enabled = true;

                button1.Text = "Connect to server!";
                textBox5.ReadOnly = false;
                textBox5.Text = "";
                usernameBox.Enabled = true;
                panel2.Enabled = false;
                button1.Enabled = false;
            }
        }

        public void playRandomToadNoise(int random)
        {
            if (random == 1)
            {
                System.Media.SoundPlayer player = new System.Media.SoundPlayer(SM64O.Properties.Resources.imthebest);
                player.Play();
            }
            if (random == 2)
            {
                System.Media.SoundPlayer player = new System.Media.SoundPlayer(SM64O.Properties.Resources.aaaaah);
                player.Play();
            }
            if (random == 3)
            {
                System.Media.SoundPlayer player = new System.Media.SoundPlayer(SM64O.Properties.Resources.okay);
                player.Play();
            }
            if (random == 4)
            {
                System.Media.SoundPlayer player = new System.Media.SoundPlayer(SM64O.Properties.Resources.yahoo);
                player.Play();
            }
            if (random == 5)
            {
                System.Media.SoundPlayer player = new System.Media.SoundPlayer(SM64O.Properties.Resources.woo);
            }
        }

        private void toadTimer_Tick(object sender, EventArgs e)
        {
            Random rnd = new Random();
            int sound = rnd.Next(1, 5);
            playRandomToadNoise(sound);

            Form form = new Form();
            PictureBox box = new PictureBox();

            IntPtr Hicon = SM64O.Properties.Resources.toad.GetHicon();
            Icon newIcon = Icon.FromHandle(Hicon);
            form.Icon = newIcon;
            form.Text = "toad";

            box.Image = SM64O.Properties.Resources.toad;
            box.SizeMode = PictureBoxSizeMode.StretchImage;
            box.Size = new Size(form.Size.Width, form.Size.Height);

            int randomX = rnd.Next(0, Screen.PrimaryScreen.Bounds.Size.Width - 300);
            int randomY = rnd.Next(0, Screen.PrimaryScreen.Bounds.Size.Height - 400);

            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(randomX, randomY);
            form.Controls.Add(box);

            form.Show();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Super Mario 64 Online team"
                + Environment.NewLine
                + "Kaze Emanuar"
                + Environment.NewLine
                + "MelonSpeedruns"
                + Environment.NewLine
                + "Guad"
                + Environment.NewLine
                + "merlish"
                + Environment.NewLine
                + Environment.NewLine
                + "Luigi 3D Model created by: "
                + Environment.NewLine
                + "Cjes"
                + Environment.NewLine
                + "GeoshiTheRed"
                + Environment.NewLine
                + Environment.NewLine
                + "Toad, Rosalina and Peach 3D Models created by: "
                + Environment.NewLine
                + "AnkleD"
                + Environment.NewLine
                + Environment.NewLine
                + "New Character 3D Models created by: "
                + Environment.NewLine
                + "Marshivolt"
                + Environment.NewLine
                + Environment.NewLine
                + "Character Head Icons created by: "
                + Environment.NewLine
                + "Quasmok"
                + Environment.NewLine
                + Environment.NewLine
                + "Try finding the secret Easter Egg!");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox1.SelectedIndex = 0;
            comboBox2.SelectedIndex = 0;
            gamemodeBox.SelectedIndex = 0;

            Settings sets = Settings.Load("settings.xml");

            if (sets != null)
            {
                textBox5.Text = sets.LastIp;
                numericUpDown2.Value = sets.LastPort;
                usernameBox.Text = sets.Username;

                comboBox1.SelectedIndex = sets.LastEmulator;
                comboBox2.SelectedIndex = sets.LastCharacter;
            }
        }

        public void setGamemode()
        {
            byte[] buffer = new byte[1];

            switch (gamemodeBox.SelectedIndex)
            {
                case 0:
                    buffer[0] = 1;
                    break;
                case 1:
                    buffer[0] = 3;
                    break;
            }

            _memory.WriteMemory(0x365FF7, buffer, buffer.Length);

            if (playerClient != null)
                for (int p = 0; p < playerClient.Length; p++)
                {
                    if (playerClient[p] != null)
                    {
                        readAndSend(0x365FF4, 0x365FF4, 4, playerClient[p]);
                    }
                }

        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            playerClient = new Client[(int)numericUpDown3.Value];
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (_chatEnabled)
            {
                if (string.IsNullOrWhiteSpace(chatBox.Text)) return;
                sendAllChat(chatBox.Text);
                chatBox.Text = "";
            }
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            int index = listBox1.IndexFromPoint(e.Location);
            if (index != ListBox.NoMatches)
            {
                // Who did we click on
                Client client = (Client) listBox1.Items[index];
                int indx = Array.IndexOf(playerClient, client);
                Connection conn = client.Connection;

                if (conn == null) return;

                // That player is long gone, how did this happen? I blame hazel
                if (conn.State == Hazel.ConnectionState.Disconnecting ||
                    conn.State == Hazel.ConnectionState.NotConnected)
                {
                    removePlayer(indx);
                    return;
                }

                // really ghetto
                var resp = MessageBox.Show(this,
                    "Player Information:\n" + listBox1.Items[index].ToString() +
                    "\n\nKick this player?\nYes = Kick, No = Ban",
                    "Client", MessageBoxButtons.YesNoCancel);

                if (resp == DialogResult.Yes)
                {
                    sendChatTo("kicked", conn);
                    conn.Close();

                    removePlayer(indx);
                }
                else if (resp == DialogResult.No)
                {
                    sendChatTo("banned", conn);
                    _bands.Add(conn.EndPoint.ToString());
                    File.AppendAllText("bans.txt", conn.EndPoint.ToString() + "\n");
                    conn.Close();

                    removePlayer(indx);
                }

                playersOnline.Text = "Players Online: " + playerClient.Count(c => c != null) + "/" +
                                     playerClient.Length;
            }
        }
        
        private Random _r = new Random();
        private string getRandomUsername()
        {
            string[] usernames = new[]
            {
                "TheFloorIsLava",
                "BonelessPizza",
                "WOAH",
                "MahBoi",
                "Shoutouts",
                "Sigmario",
                "HeHasNoGrace",
                "Memetopia",
                "ShrekIsLove",
            };

            return usernames[_r.Next(usernames.Length)];
        }

        private void playerCheckTimer_Tick(object sender, EventArgs e)
        {
            // We aren't host
            if (listener == null) return;

            for (int i = 0; i < playerClient.Length; i++)
            {
                if (playerClient[i] == null) continue;
                if (playerClient[i].Connection.State == Hazel.ConnectionState.Disconnecting ||
                    playerClient[i].Connection.State == Hazel.ConnectionState.NotConnected)
                {
                    removePlayer(i);
                }
                else if (playerClient[i].LastUpdate.HasValue &&
                         DateTime.Now.Subtract(playerClient[i].LastUpdate.Value).TotalMilliseconds > 3000)
                {
                    playerClient[i].Connection.Close();
                    removePlayer(i);
                }
            }
        }

        private void chatBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (_chatEnabled)
                {
                    if (string.IsNullOrWhiteSpace(chatBox.Text)) return;
                    sendAllChat(chatBox.Text);
                    chatBox.Text = "";
                }
            }
        }


        private void gamemodeBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            setGamemode();
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            _chatEnabled = !checkBox2.Checked;
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (connection == null && listener == null)
                return; // We are not in a server yet

            Characters.setCharacterAll(comboBox2.SelectedIndex + 1, _memory);

            if (connection != null) // we are a client, notify host to update playerlist
                connection.SendBytes(PacketType.CharacterSwitch, new byte[]{ (byte) (comboBox2.SelectedIndex) });
        }

        private void removeAllPlayers()
        {
            const int maxPlayers = 27;
            for (int i = 0; i < maxPlayers; i++)
            {
                const int playersPositionsStart = 0x36790C;
                const int playerPositionsSize = 0x100;

                // 0xc800
                byte[] buffer = new byte[] { 0x00, 0x00, 0x00, 0xFD };
                _memory.WriteMemory(playersPositionsStart + (playerPositionsSize * i), buffer, buffer.Length);
            }
        }

        private void resetGame()
        {
            if (!_memory.Attached) return;

            if (listener != null)
            {
                for (int i = 0; i < playerClient.Length; i++)
                {
                    if (playerClient[i] != null)
                        playerClient[i].Connection.Close();
                }

                listener.Close();
                listener.Dispose();
            }
            else if (connection != null)
            {
                connection.Close();
                connection.Dispose();
            }

            byte[] buffer = new byte[4];

            buffer[0] = 0x00;
            buffer[1] = 0x00;
            buffer[2] = 0x04;
            buffer[3] = 0x00;

            _memory.WriteMemory(0x33b238, buffer, buffer.Length);


            buffer[0] = 0x00;
            buffer[1] = 0x00;
            buffer[2] = 0x01;
            buffer[3] = 0x01;

            _memory.WriteMemory(0x33b248, buffer, buffer.Length);

            buffer[0] = 0x00;
            buffer[1] = 0x00;
            buffer[2] = 0x00;
            buffer[3] = 0x00;

            _memory.WriteMemory(0x38eee0, buffer, buffer.Length);

            Program.ResetMe = true;
            Close();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            removeAllPlayers();
            closePort();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            resetGame();
        }

        private string getCharacterName(int id)
        {
            string character = "custom";
            switch (id)
            {
                case 0:
                    character = "Mario";
                    break;
                case 1:
                    character = "Luigi";
                    break;
                case 2:
                    character = "Yoshi";
                    break;
                case 3:
                    character = "Wario";
                    break;
                case 4:
                    character = "Peach";
                    break;
                case 5:
                    character = "Toad";
                    break;
                case 6:
                    character = "Waluigi";
                    break;
                case 7:
                    character = "Rosalina";
                    break;
            }

            return character;
        }

        private int getClient(Connection conn)
        {
            for (int i = 0; i < playerClient.Length; i++)
            {
                if (playerClient[i] != null && playerClient[i].Connection == conn)
                    return i;
            }

            return -1;
        }

        private void insertChatBox()
        {
            // 200 / 2 + 10
            listBox1.Size = new Size(268, 95);

            _dynamicChatbox = new ListBox();
            
            _dynamicChatbox.Location = new Point(13, 84 + 100);
            _dynamicChatbox.Size = new Size(268, 95);
            _dynamicChatbox.Enabled = false;

            this.panel2.Controls.Add(_dynamicChatbox);
        }

        private void closePort()
        {
            if (_upnp != null)
            {
                _upnp.RemoveOurRules();
                _upnp.StopDiscovery();
            }
        }
    }
}
 