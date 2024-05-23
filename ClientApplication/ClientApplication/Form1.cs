using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.MonthCalendar;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolBar;

namespace ClientApplication
{
    public partial class Form1 : Form
    {
        TcpClient client;
        NetworkStream ns;
        StreamReader sr;
        StreamWriter sw;
        private byte[] data = new byte[1024];
        private int size = 1024;
        private string selectedImagePath;
        private bool connectionStatus = false;

        public Form1()
        {
            InitializeComponent();
        }

        //connect
        private async void button3_Click(object sender, EventArgs e)
        {
            client = new TcpClient();
            try
            {
                await client.ConnectAsync("127.0.0.1", 9050);
                ns = client.GetStream();
                sr = new StreamReader(ns);
                sw = new StreamWriter(ns);
                richTextBox1.Text = "Connected to Server IP: " + client.Client.RemoteEndPoint.ToString()+"\n";
                button2.Enabled = true;
                button3.Enabled = false;
                button4.Enabled = true;
                connectionStatus = true;
                ReceiveData();
            }
            catch (Exception ex)
            {
                richTextBox3.Text = "";
                richTextBox3.Text = "Error connecting: " + ex.Message;
            }
        }
        //send
        private async void button2_Click(object sender, EventArgs e)
        {
            if (client == null || !client.Connected)
            {
                richTextBox3.Text = "Not connected to server.";
                return;
            }

            try
            {
                if (pictureBox1.Image == null && richTextBox1.Text != null)
                {
                    // Send text
                    string message = richTextBox2.Text;
                    byte[] header = Encoding.ASCII.GetBytes("text:");
                    byte[] messageBytes = Encoding.ASCII.GetBytes(message);
                    byte[] dataToSend = new byte[header.Length + messageBytes.Length];
                    Buffer.BlockCopy(header, 0, dataToSend, 0, header.Length);
                    Buffer.BlockCopy(messageBytes, 0, dataToSend, header.Length, messageBytes.Length);
                    await ns.WriteAsync(dataToSend, 0, dataToSend.Length);

                    richTextBox1.Text = "Me (client): " + message + "\n";
                    richTextBox1.AppendText(Environment.NewLine);
                    richTextBox2.Text = string.Empty;
                }
                else if (pictureBox1.Image != null)
                {
                    // Send image
                    if (!string.IsNullOrEmpty(selectedImagePath))
                    {
                        byte[] header = Encoding.ASCII.GetBytes("image:");
                        byte[] imageData = File.ReadAllBytes(selectedImagePath);

                        // Combine the header, size, and image data into a single byte array
                        byte[] dataToSend = new byte[header.Length + sizeof(int) + imageData.Length];

                        // Copy the header to the dataToSend array
                        Buffer.BlockCopy(header, 0, dataToSend, 0, header.Length);

                        // Copy the size of the image to the dataToSend array after the header
                        Buffer.BlockCopy(BitConverter.GetBytes(imageData.Length), 0, dataToSend, header.Length, sizeof(int));

                        // Copy the image data to the dataToSend array after the header and size
                        Buffer.BlockCopy(imageData, 0, dataToSend, header.Length + sizeof(int), imageData.Length);

                        // Send the combined data (header, size, and image data) to the server
                        await ns.WriteAsync(dataToSend, 0, dataToSend.Length);
                        ns.Flush();

                        richTextBox1.Text = "SendImage successful \n";
                    }
                }
                else
                {
                    richTextBox3.Text = "";
                    richTextBox3.Text = "There is nothing to send";
                }
            }
            catch (Exception ex)
            {
                richTextBox3.Text = "";
                richTextBox3.Text = "Error sending data: " + ex.Message;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (client != null && client.Connected)
            {
                try
                {
                    connectionStatus = false;
                    ns.Close();
                    sr.Close();
                    sw.Close();
                    client.Close();
                    client = null;
                    richTextBox1.Text = "Disconnected";
                    button2.Enabled = false;
                    button4.Enabled = false;
                    button3.Enabled = true;
                }
                catch (Exception ex)
                {
                    richTextBox3.Text = "";
                    richTextBox3.Text = "Error disconnecting: " + ex.Message;
                }
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            if (client == null || !client.Connected)
            {
                richTextBox3.Text = "";
                richTextBox3.Text = "Not connected to server.";
                return;
            }

            try
            {
                string fileName = listBox1.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(fileName))
                {
                    richTextBox3.Text = "Please select a file.";
                    return;
                }

                // Marking the data as text
                byte[] header = Encoding.ASCII.GetBytes("text:");
                byte[] fileNameBytes = Encoding.ASCII.GetBytes(fileName);
                byte[] dataToSend = new byte[header.Length + fileNameBytes.Length];
                Buffer.BlockCopy(header, 0, dataToSend, 0, header.Length);
                Buffer.BlockCopy(fileNameBytes, 0, dataToSend, header.Length, fileNameBytes.Length);
                richTextBox1.Text = Encoding.ASCII.GetString(dataToSend);
                await sw.BaseStream.WriteAsync(dataToSend, 0, dataToSend.Length);
                await sw.BaseStream.FlushAsync();
            }
            catch (Exception ex)
            {
                richTextBox3.Text = "";
                richTextBox3.Text = "Error receiving file: " + ex.Message;
            }
        }


        private void button5_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.gif)|*.jpg;*.jpeg;*.png;*.gif|All files (*.*)|*.*",
                FilterIndex = 1
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                selectedImagePath = openFileDialog.FileName;
                pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
                pictureBox1.Image = new Bitmap(openFileDialog.FileName, false);
            }
        }


       
        private async void ReceiveData()
        {
            while (connectionStatus)
            {
                try
                {
                    // Read the initial data chunk
                    int bytesRead = await ns.ReadAsync(data, 0, size);
                    if (bytesRead == 0)
                    {
                        connectionStatus = false;
                        richTextBox1.Invoke((MethodInvoker)delegate {
                            richTextBox1.Text = "Server is Disconnected";
                        });
                        button2.Invoke((MethodInvoker)delegate { button2.Enabled = false; });
                        button4.Invoke((MethodInvoker)delegate { button4.Enabled = false; });
                        button3.Invoke((MethodInvoker)delegate { button3.Enabled = true; });
                        client.Close();
                        return;
                    }

                    string receivedData = Encoding.ASCII.GetString(data, 0, bytesRead).Trim();

                    // Check if the received data contains a header indicating its type
                    if (receivedData.StartsWith("file:"))
                    {
                        // Handle file data
                        string fileName = receivedData.Substring(5); // Remove the header
                        ReceiveFile(fileName);
                    }
                    else if (receivedData.StartsWith("image:"))
                    {
                        string fileName = receivedData.Substring(6);
                        ReceiveImage(fileName);
                    }
                    else if (receivedData.StartsWith("text:"))
                    {
                        string file = receivedData.Substring(5);
                        if (file.Contains("|"))
                        {
                            string[] fileNames = file.Split('|');

                            richTextBox1.Invoke((MethodInvoker)delegate {
                                richTextBox1.AppendText($"\nReceiving {fileNames.Length} file names from the server...");
                            });

                            foreach (string fileName in fileNames)
                            {
                                listBox1.Invoke((MethodInvoker)delegate
                                {
                                    listBox1.Items.Add(fileName); // Add file name to list box
                                });
                            }

                            richTextBox1.Invoke((MethodInvoker)delegate {
                                richTextBox1.AppendText($"\nAll {fileNames.Length} file names received successfully.");
                            });
                        }
                        else
                        {
                            richTextBox1.Invoke((MethodInvoker)delegate {
                                richTextBox1.AppendText(file);
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    richTextBox3.Invoke((MethodInvoker)delegate {
                        richTextBox3.Text = "";
                        richTextBox3.Text = "Error receiving data: " + ex.Message;
                    });
                    connectionStatus = false;
                }
            }
        }

        private void ReceiveFile(string fileName)
        {
            // Handle file data
            richTextBox1.Invoke((MethodInvoker)delegate {
                richTextBox1.AppendText($"\nReceived file: {fileName}");
            });
        }

        private async void ReceiveImage(string fileName)
        {
            
            try
            {
                byte[] sizeData = new byte[4];
                await ns.ReadAsync(sizeData, 0, sizeData.Length);
                int imageSize = BitConverter.ToInt32(sizeData, 0);
                byte[] imageData = File.ReadAllBytes(selectedImagePath);
                int totalBytesRead = 0;
                while (totalBytesRead < imageSize)
                {
                    int bytesRead = await ns.ReadAsync(imageData, totalBytesRead, imageSize - totalBytesRead);
                    totalBytesRead += bytesRead;
                }
                using (MemoryStream memoryStream = new MemoryStream(imageData))
                {
                    pictureBox1.Image = Image.FromStream(memoryStream);
                }
            }
            catch (Exception ex)
            {
                // Handle exception
                richTextBox3.Invoke((MethodInvoker)delegate {
                    richTextBox3.Text = "";
                    richTextBox3.Text = "Error receiving image data: " + ex.Message;
                });
            }
        }


    }
}

