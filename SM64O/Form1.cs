﻿using Hazel;
using Hazel.Tcp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

#pragma warning disable CS0618 // Type or member is obsolete
namespace SM64O
{
    public partial class Form1 : Form
    {

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesWritten);

        const int PROCESS_WM_READ = 0x0010;

        public IntPtr processHandle;
        public int baseAddress = 0;

        public static ConnectionListener listener;
        public static Connection connection = null;
        public static Connection[] playerClient = new Connection[23];

        private List<string> _bands = new List<string>();

        private const int VERSION = 3;

        public Form1()
        {
            InitializeComponent();

            string[] fileEntries = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "/Patches/");

            foreach (var file in fileEntries)
            {
                int offset = Convert.ToInt32(Path.GetFileName(file), 16);

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

        }

        private void die(string msg)
        {
            MessageBox.Show(this, msg, "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }


        private void sendAllChat(string message)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(message);
            byte[] mainArray = new byte[bytes.Length + 4];
            byte[] outputArray = new byte[bytes.Length + 4];

            bytes.CopyTo(mainArray, 0);
            int count = 0;
            while (count < mainArray.Length)
            {
                byte[] array = mainArray.Skip<byte>(count).Take<byte>(4).Reverse<byte>().ToArray<byte>();
                Array.Copy(array, 0, outputArray, count, Math.Min(4, array.Length));
                count += 4;
            }

            byte[] payload = new byte[outputArray.Length + 4];
            Array.Copy(BitConverter.GetBytes(3569284), 0, payload, 0, 4);
            Array.Copy(outputArray, 0, payload, 4, outputArray.Length);

            byte[] aux = new byte[8];

            Array.Copy(BitConverter.GetBytes(3569280), 0, aux, 0, 4);

            if (connection == null)
            {
                for (int i = 0; i < Form1.playerClient.Length; i++)
                {
                    if (Form1.playerClient[i] != null)
                    {
                        Form1.playerClient[i].SendBytes(payload);
                    }
                }
            }
            else
            {
                connection.SendBytes(payload);
            }

            Thread.Sleep(100);

            if (connection == null)
            {
                for (int i = 0; i < Form1.playerClient.Length; i++)
                {
                    if (Form1.playerClient[i] != null)
                    {
                        Form1.playerClient[i].SendBytes(aux);
                    }
                }
            }
            else
            {
                connection.SendBytes(aux);
            }

            Characters.setMessage(message, processHandle, baseAddress);
        }

        private void sendChatTo(string message, Connection conn)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(message);
            byte[] mainArray = new byte[bytes.Length + 4];
            byte[] outputArray = new byte[bytes.Length + 4];

            bytes.CopyTo(mainArray, 0);
            int count = 0;
            while (count < mainArray.Length)
            {
                byte[] array = mainArray.Skip<byte>(count).Take<byte>(4).Reverse<byte>().ToArray<byte>();
                Array.Copy(array, 0, outputArray, count, Math.Min(4, array.Length));
                count += 4;
            }

            byte[] payload = new byte[outputArray.Length + 4];
            Array.Copy(BitConverter.GetBytes(3569284), 0, payload, 0, 4);
            Array.Copy(outputArray, 0, payload, 4, outputArray.Length);

            byte[] aux = new byte[8];

            Array.Copy(BitConverter.GetBytes(3569280), 0, aux, 0, 4);

            conn.SendBytes(payload);
            Thread.Sleep(100);
            conn.SendBytes(aux);
        }


        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                if (comboBox1.Text == "Project64")
                {
                    Process process = Process.GetProcessesByName("Project64")[0];

                    baseAddress = ReadWritingMemory.GetBaseAddress("Project64", 4096, 4);
                    textBox1.Text = baseAddress.ToString("X");

                    processHandle = OpenProcess(0x1F0FFF, true, process.Id);
                }

                if (comboBox1.Text == "Nemu64")
                {
                    Process process = Process.GetProcessesByName("Nemu64")[0];

                    baseAddress = ReadWritingMemory.GetBaseAddress("Nemu64", 4096, 4);
                    textBox1.Text = baseAddress.ToString("X");

                    processHandle = OpenProcess(0x1F0FFF, true, process.Id);
                }

                if (comboBox1.Text == "Mupen64")
                {
                    Process process = Process.GetProcessesByName("Mupen64")[0];

                    baseAddress = ReadWritingMemory.GetBaseAddress("Mupen64", 4096, 4);
                    textBox1.Text = baseAddress.ToString("X");

                    processHandle = OpenProcess(0x1F0FFF, true, process.Id);
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
                    listener = new TcpConnectionListener(IPAddress.Any, (int) numericUpDown2.Value);
                    listener.NewConnection += NewConnectionHandler;
                    listener.Start();

                    miniGame1.Enabled = true;
                    miniGame3.Enabled = true;
                }
                else
                {
                    byte[] payload = new byte[2];
                    payload[0] = VERSION;
                    payload[1] = (byte)this.comboBox2.SelectedIndex;

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
                    connection = new TcpConnection(endPoint);
                    connection.DataReceived += DataReceived;
                    connection.Connect(payload, 3000);
                    connection.Disconnected += ConnectionOnDisconnected;
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

            timer1.Enabled = true;
            button1.Enabled = false;
            textBox5.Enabled = false;
            numericUpDown1.Enabled = true;

            comboBox1.Enabled = false;
            comboBox2.Enabled = false;

            Characters.setUsername(processHandle, baseAddress);
            Characters.setCharacter(comboBox2.SelectedItem.ToString(), processHandle, baseAddress);

            string[] fileEntries = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "/Ressources/");

            foreach (var file in fileEntries)
            {
                int offset = Convert.ToInt32(Path.GetFileName(file), 16);

                int bytesWritten = 0;

                byte[] buffer = File.ReadAllBytes(file);
                WriteProcessMemory((int)processHandle, baseAddress + offset, buffer, buffer.Length, ref bytesWritten);
            }

            if (checkBox1.Checked)
            {
                writeValue(new byte[] { 0x00, 0x00, 0x00, 0x01 }, 0x365FFC);
            }
        }

        private void ConnectionOnDisconnected(object sender, DisconnectedEventArgs disconnectedEventArgs)
        {
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
                        playerClient[i] = e.Connection;
                        e.Connection.DataReceived += DataReceivedHandler;
                        e.Connection.Disconnected += client_Disconnected;

                        int playerIDB = i + 2;
                        byte[] playerID = new byte[] {(byte) playerIDB};
                        Thread.Sleep(500);
                        playerClient[i].SendBytes(playerID);
                        string character = "Unk Char";
                        string vers = "Default Client";

                        if (e.HandshakeData != null && e.HandshakeData.Length >= 2)
                        {
                            byte verIndex = e.HandshakeData[0];
                            byte charIndex = e.HandshakeData[1];
                            vers = "v" + verIndex;
                            switch (charIndex)
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

                        }

                        listBox1.Items.Add(string.Format("[{0}] {1} | {2}", e.Connection.EndPoint.ToString(),
                            character, vers));
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
                if (playerClient[i] == conn)
                {
                    Console.WriteLine("player disconnected!");
                    playerClient[i] = null;
                    conn.DataReceived -= DataReceivedHandler;
                    for (int index = 0; index < listBox1.Items.Count; index++)
                    {
                        if (listBox1.Items[index].ToString().Contains(conn.EndPoint.ToString()))
                        {
                            listBox1.Items.RemoveAt(index);
                            break;
                        }
                    }
                    break;
                }
            }

            playersOnline.Text = "Players Online: " + playerClient.Count(c => c != null) + "/" + playerClient.Length;
        }

        private void DataReceivedHandler(object sender, Hazel.DataReceivedEventArgs e)
        {
            ReceiveRawMemory(e.Bytes);

            e.Recycle();
        }

        private void DataReceived(object sender, Hazel.DataReceivedEventArgs e)
        {
            if (e.Bytes.Length == 1)
            {
                int bytesWritten = 0;
                WriteProcessMemory((int)processHandle, baseAddress + 0x367703, e.Bytes, e.Bytes.Length, ref bytesWritten);
            }
            else
            {
                ReceiveRawMemory(e.Bytes);
                e.Recycle();
            }
        }

        private void ReceiveRawMemory(byte[] data)
        {
            int offset = BitConverter.ToInt32(data, 0);
            if (offset < 0x365000 || offset > 0x365000 + 8388608) // Only allow 8 MB N64 RAM addresses
                return;

            int bytesWritten = 0;
            byte[] buffer = new byte[data.Length - 4];
            data.Skip(4).ToArray().CopyTo(buffer, 0);

            WriteProcessMemory((int)processHandle, baseAddress + offset, buffer, buffer.Length, ref bytesWritten);

            if (connection != null) return; // We aren't the host
            if (offset == 3569284 || offset == 3569280) // It's a chat message
            {
                for (int i = 0; i < Form1.playerClient.Length; i++)
                {
                    if (Form1.playerClient[i] != null)
                    {
                        Form1.playerClient[i].SendBytes(data);
                    }
                }
            }
        }

        public void writeValue(byte[] buffer, int offset)
        {
            buffer = buffer.Reverse().ToArray();
            int bytesWritten = 0;
            WriteProcessMemory((int)processHandle, baseAddress + offset, buffer, buffer.Length, ref bytesWritten);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                sendAllBytes(null);
            }
            catch
            {

            }
        }

        // not touching this
        public void sendAllBytes(Connection conn)
        {
            int freeRamLength = getRamLength(0x367400);

            int[] offsetsToReadFrom = new int[freeRamLength];
            int[] offsetsToWriteToLength = new int[freeRamLength];
            int[] offsetsToWriteTo = new int[freeRamLength];

            int bytesRead = 0;
            byte[] originalBuffer = new byte[freeRamLength];
            ReadProcessMemory((int)processHandle, baseAddress + 0x367400, originalBuffer, originalBuffer.Length, ref bytesRead);

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

                if (playerClient != null)
                {
                    if (checkBox1.Checked)
                    {
                        for (int p = 0; p < playerClient.Length; p++)
                        {
                            if (playerClient[p] != null)
                            {
                                readAndSend(offsetsToReadFrom[i], offsetsToWriteTo[i + 8], offsetsToWriteToLength[i + 4], playerClient[p]);
                            }
                        }
                    }
                    else
                    {
                        readAndSend(offsetsToReadFrom[i], offsetsToWriteTo[i + 8], offsetsToWriteToLength[i + 4], connection);
                    }
                }

            }
        }

        public int getRamLength(int offset)
        {
            for (int i = 0; i < 0x1024; i += 4)
            {
                int bytesRead = 0;
                byte[] buffer = new byte[4];

                ReadProcessMemory((int)processHandle, baseAddress + offset + i, buffer, buffer.Length, ref bytesRead);
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
            int bytesRead = 0;
            byte[] buffer = new byte[howMany];
            byte[] writeOffset = BitConverter.GetBytes(offsetToWrite);

            ReadProcessMemory((int)processHandle, baseAddress + offsetToRead, buffer, buffer.Length, ref bytesRead);

            byte[] finalOffset = new byte[howMany + 4];
            writeOffset.CopyTo(finalOffset, 0);
            buffer.CopyTo(finalOffset, 4);
            conn.SendBytes(finalOffset);
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
            timer1.Interval = (int)numericUpDown1.Value;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                button1.Text = "Create Server!";
            } else
            {
                button1.Text = "Connect to server!";
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
            MessageBox.Show("Super Mario 64 Online made by Kaze Emanuar and MelonSpeedruns!"
                + Environment.NewLine
                + Environment.NewLine
                + "Luigi 3D Model created by: "
                + Environment.NewLine
                + "Cjes"
                + Environment.NewLine
                + "GeoshiTheRed"
                + Environment.NewLine
                + Environment.NewLine
                + "Rosalina and Peach 3D Models created by: "
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
        }

        public void setGamemode()
        {
            byte[] buffer = new byte[0];

            if (miniGame1.Checked)
            {
                buffer = new byte[] { 0x01 };
            }

            if (miniGame2.Checked)
            {
                buffer = new byte[] { 0x02 };
            }

            if (miniGame3.Checked)
            {
                buffer = new byte[] { 0x03 };
            }

            if (miniGame4.Checked)
            {
                buffer = new byte[] { 0x04 };
            }

            if (miniGame5.Checked)
            {
                buffer = new byte[] { 0x05 };
            }

            if (miniGame6.Checked)
            {
                buffer = new byte[] { 0x06 };
            }

            int bytesWritten = 0;
            WriteProcessMemory((int)processHandle, baseAddress + 0x365FF7, buffer, buffer.Length, ref bytesWritten);

            if (playerClient != null)
            {
                for (int p = 0; p < playerClient.Length; p++)
                {
                    if (playerClient[p] != null)
                    {
                        readAndSend(baseAddress + 0x365FF4, baseAddress + 0x365FF4, 4, playerClient[p]);
                    }
                }
            }

        }

        private void miniGame1_CheckedChanged(object sender, EventArgs e)
        {
            setGamemode();
        }

        private void miniGame2_CheckedChanged(object sender, EventArgs e)
        {
            setGamemode();
        }

        private void miniGame3_CheckedChanged(object sender, EventArgs e)
        {
            setGamemode();
        }

        private void miniGame4_CheckedChanged(object sender, EventArgs e)
        {
            setGamemode();
        }

        private void miniGame5_CheckedChanged(object sender, EventArgs e)
        {
            setGamemode();
        }

        private void miniGame6_CheckedChanged(object sender, EventArgs e)
        {
            setGamemode();
        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            int newCount = (int) numericUpDown3.Value;

            // Maybe add an option to remove the player cap?
            if (newCount > 23)
                newCount = 23;
            Form1.playerClient = new Connection[newCount];
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(chatBox.Text)) return;
            sendAllChat(chatBox.Text);
            chatBox.Text = "";
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            int index = listBox1.IndexFromPoint(e.Location);
            if (index != ListBox.NoMatches)
            {
                // Who did we click on
                Connection conn =
                    Form1.playerClient.FirstOrDefault(c =>
                    {
                        if (c == null || c.EndPoint == null) return false;
                        return listBox1.Items[index].ToString().Contains(c.EndPoint.ToString());
                    });

                if (conn == null) return;

                // That player is long gone, how did this happen? I blame hazel
                if (conn.State == Hazel.ConnectionState.Disconnecting ||
                    conn.State == Hazel.ConnectionState.NotConnected)
                {
                    int indx = Array.IndexOf(playerClient, conn);
                    conn.DataReceived -= DataReceivedHandler;
                    if (indx != -1)
                        playerClient[indx] = null;
                    listBox1.Items.RemoveAt(index);
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

                    int indx = Array.IndexOf(playerClient, conn);
                    conn.DataReceived -= DataReceivedHandler;
                    if (indx != -1)
                        playerClient[indx] = null;
                    listBox1.Items.RemoveAt(index);
                }
                else if (resp == DialogResult.No)
                {
                    sendChatTo("banned", conn);
                    _bands.Add(conn.EndPoint.ToString());
                    File.AppendAllText("bans.txt", conn.EndPoint.ToString() + "\n");
                    conn.Close();

                    int indx = Array.IndexOf(playerClient, conn);
                    conn.DataReceived -= DataReceivedHandler;
                    if (indx != -1)
                        playerClient[indx] = null;
                    listBox1.Items.RemoveAt(index);
                }

                playersOnline.Text = "Players Online: " + playerClient.Count(c => c != null) + "/" +
                                     playerClient.Length;
            }
        }
    }
}
 