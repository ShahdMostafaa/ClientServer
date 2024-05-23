using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolBar;
using System.Collections;
using System.Threading;
using System.IO.Compression;

namespace ServerApplication
{
    public partial class Form1 : Form
    {
        TcpListener server;
        TcpClient client;
        NetworkStream ns;
        StreamReader sr;
        StreamWriter sw;
        private byte[] data = new byte[1024];
        private int size = 1024;
        string folderloc;
        private string selectedImagePath;
        public Form1()
        {
            InitializeComponent();
            
        }
        
        
        private void Form1_Load(object sender, EventArgs e)
        {

        }
        //connect
        private async void button3_Click(object sender, EventArgs e)
        {
            try
            {
                server = new TcpListener(IPAddress.Any, 9050);
                server.Start();
                richTextBox3.Text = "Waiting for a client";

                client = await server.AcceptTcpClientAsync();
                ns = client.GetStream();
                sr = new StreamReader(ns);
                sw = new StreamWriter(ns) { AutoFlush = true };

                richTextBox1.Text = "Connected to: " + client.Client.RemoteEndPoint.ToString() + "\n";
                button2.Enabled = true;
                richTextBox3.Text = "client";
                string welcomeMessage = "Server: Welcome to my server";

                // Prepend a header indicating the type of data
                string header = "text:";
                byte[] headerBytes = Encoding.ASCII.GetBytes(header);

                // Convert the welcome message to bytes
                byte[] message = Encoding.ASCII.GetBytes(welcomeMessage);

                // Combine the header and message bytes into a single byte array
                byte[] dataToSend = new byte[headerBytes.Length + message.Length];
                Buffer.BlockCopy(headerBytes, 0, dataToSend, 0, headerBytes.Length);
                Buffer.BlockCopy(message, 0, dataToSend, headerBytes.Length, message.Length);

                // Send the data asynchronously
                await ns.WriteAsync(dataToSend, 0, dataToSend.Length);

                ReceiveData();
            }
            catch (Exception ex)
            {
                richTextBox3.Text = "";
                richTextBox3.Text = "Error starting server: " + ex.Message;
            }
        }
        //send
        private async void button2_Click(object sender, EventArgs e)
        {
            try
            {
                if(pictureBox1.Image == null && richTextBox1.Text != null)
                {
                    // Prepare the message with the "text:" header
                    string message = "Server: " + richTextBox2.Text;
                    richTextBox2.Text = string.Empty;
                    string header = "text:";
                    byte[] headerBytes = Encoding.ASCII.GetBytes(header);
                    byte[] messageBytes = Encoding.ASCII.GetBytes(message);
                    byte[] dataToSend = new byte[headerBytes.Length + messageBytes.Length];
                    Buffer.BlockCopy(headerBytes, 0, dataToSend, 0, headerBytes.Length);
                    Buffer.BlockCopy(messageBytes, 0, dataToSend, headerBytes.Length, messageBytes.Length);

                    await ns.WriteAsync(dataToSend, 0, dataToSend.Length);

                    richTextBox1.Text = "Me (Server): " + message + "\n";
                    //richTextBox1.AppendText(Environment.NewLine);
                }
                else if (pictureBox1.Image != null)
                {
                    // Send image
                    if (!string.IsNullOrEmpty(selectedImagePath))
                    {
                        byte[] header = Encoding.ASCII.GetBytes("image:");
                        byte[] imageData = File.ReadAllBytes(selectedImagePath);
                        byte[] dataToSend = new byte[header.Length + imageData.Length];
                        Buffer.BlockCopy(header, 0, dataToSend, 0, header.Length);
                        Buffer.BlockCopy(imageData, 0, dataToSend, header.Length, imageData.Length);
                        await ns.WriteAsync(dataToSend, 0, dataToSend.Length);
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

        private async void ReceiveData()
        {
            string[] fileExtensions = { ".txt", ".pdf", ".doc", ".jpg", ".png", ".gif" };
            try
            {
                while (true)
                {
                    int bytesRead = await ns.ReadAsync(data, 0, size);
                    if (bytesRead == 0)
                    {
                        richTextBox1.Invoke((MethodInvoker)delegate {
                            richTextBox1.Text = "Client disconnected";
                        });
                        ns.Close();
                        sr.Close();
                        sw.Close();
                        client.Close();
                        return;
                    }

                    string receivedData = Encoding.ASCII.GetString(data, 0, bytesRead).Trim();
                    string[] parts = receivedData.Split(new char[] { ':' }, 2);
                    string dataType = parts[0];
                    string dataContent = parts[1];

                    if (dataType == "text")
                    {
                        if (dataContent.Length > 3 && dataContent[1] == ':' && dataContent[2] == '\\') HandleFolderPath(dataContent);
                        else if (dataContent.EndsWith(".txt") || dataContent.EndsWith(".pdf") || dataContent.EndsWith(".doc"))
                        {
                            richTextBox1.Text = "skip";
                            SendTextFile(dataContent);
                            richTextBox1.Text = dataContent;
                        }
                        else if (dataContent.EndsWith(".jpg") || dataContent.EndsWith(".png") || dataContent.EndsWith(".gif")) SendImage(dataContent);
                        else
                        {
                            richTextBox1.Invoke((MethodInvoker)delegate
                            {
                                richTextBox1.Text += $"Client: {dataContent}\n";
                            });
                        }
                    }
                    else if (dataType == "image")
                    {
                        try
                        {
                            richTextBox1.Text += "Image ready";
                            byte[] sizeData = new byte[4];

                            try
                            {
                                // Read the size of the image data
                                await ns.ReadAsync(sizeData, 0, sizeData.Length);
                                int imageSize = BitConverter.ToInt32(sizeData, 0);

                                // Initialize variables for receiving image data
                                byte[] imageData = new byte[imageSize];
                                int totalBytesRead = 0;

                                // Continue reading until all image data is received
                                while (totalBytesRead < imageSize)
                                {
                                    richTextBox1.AppendText("reading...");
                                    bytesRead = await ns.ReadAsync(imageData, totalBytesRead, imageSize - totalBytesRead);
                                    if (bytesRead == 0)
                                    {
                                        // Connection closed prematurely or no data available
                                        throw new IOException("Connection closed prematurely or no data available.");
                                    }
                                    totalBytesRead += bytesRead;
                                    richTextBox1.AppendText($"Bytes read: {bytesRead}, Total bytes read: {totalBytesRead}");
                                }

                                // Check if all expected bytes have been read
                                if (totalBytesRead != imageSize)
                                {
                                    throw new IOException("Incomplete data received.");
                                }

                                richTextBox1.AppendText("done");
                                pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;

                                using (MemoryStream memoryStream = new MemoryStream(imageData))
                                {
                                    richTextBox1.AppendText("Displaying image...");
                                    pictureBox1.Image = Image.FromStream(memoryStream);
                                }
                            }
                            catch (Exception ex)
                            {
                                richTextBox1.AppendText($"Error receiving data: {ex.Message}");
                            }
                            finally
                            {
                                richTextBox1.AppendText("Completed");
                            }


                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("An error occurred while receiving and displaying the image: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    
                }
               
            }
            catch (Exception ex)
            {
                richTextBox3.Invoke((MethodInvoker)delegate {
                    richTextBox3.Text = "";
                    richTextBox3.Text = "Error receiving data: " + ex.Message;
                });
            }
          

        }

        private void HandleFolderPath(string folderPath)
        {
            try
            {
                folderloc = folderPath;
                // Prepend a header indicating the type of data (text)
                string header = "text:";
                byte[] headerBytes = Encoding.ASCII.GetBytes(header);

                richTextBox1.Invoke((MethodInvoker)delegate {
                    richTextBox1.AppendText($"The needed location name is: {folderPath}\n");
                });

                if (Directory.Exists(folderPath))
                {
                    string[] files = Directory.GetFiles(folderPath);
                    string fileNames = string.Join("|", files.Select(Path.GetFileName));

                    // Combine the header and data into a single byte array
                    byte[] dataToSend = new byte[headerBytes.Length + Encoding.ASCII.GetByteCount(fileNames)];
                    Buffer.BlockCopy(headerBytes, 0, dataToSend, 0, headerBytes.Length);
                    Encoding.ASCII.GetBytes(fileNames, 0, fileNames.Length, dataToSend, headerBytes.Length);

                    // Send the data
                    ns.Write(dataToSend, 0, dataToSend.Length);
                    ns.Flush();

                    richTextBox1.Invoke((MethodInvoker)delegate {
                        richTextBox1.AppendText($"Sent file names: {fileNames}\n");
                    });
                }
                else
                {
                    // Send directory not found message
                    string errorMessage = $"Directory '{folderPath}' not found.";
                    byte[] errorMessageBytes = Encoding.ASCII.GetBytes(errorMessage);

                    // Combine the header and error message into a single byte array
                    byte[] dataToSend = new byte[headerBytes.Length + errorMessageBytes.Length];
                    Buffer.BlockCopy(headerBytes, 0, dataToSend, 0, headerBytes.Length);
                    Buffer.BlockCopy(errorMessageBytes, 0, dataToSend, headerBytes.Length, errorMessageBytes.Length);

                    // Send the data
                    ns.Write(dataToSend, 0, dataToSend.Length);
                    ns.Flush();

                    richTextBox3.Invoke((MethodInvoker)delegate {
                        richTextBox3.Text = "";
                        richTextBox3.Text = errorMessage;
                    });
                }
            }
            catch (Exception ex)
            {
                richTextBox3.Invoke((MethodInvoker)delegate {
                    richTextBox3.Text = "";
                    richTextBox3.Text = "Error sending file names: " + ex.Message;
                });
            }
        }


        private void SendTextFile(string fileName)
        {
            string filePath = folderloc + "\\" + fileName;
            byte[] fileBytes = File.ReadAllBytes(filePath);
            richTextBox1.Text += filePath;
            // Prepend a generic file header
            string header = "file:";
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);

            // Combine the header and file content into a single byte array
            byte[] dataToSend = new byte[headerBytes.Length + fileBytes.Length];
            Buffer.BlockCopy(headerBytes, 0, dataToSend, 0, headerBytes.Length);
            Buffer.BlockCopy(fileBytes, 0, dataToSend, headerBytes.Length, fileBytes.Length);

            // Send the data
            ns.Write(dataToSend, 0, dataToSend.Length);
            ns.Flush();

            richTextBox1.Text += $"File '{fileName}' sent successfully.";
        }


        private void SendImage(string fileName)
        {
            try
            {
                byte[] imageData = File.ReadAllBytes(folderloc + "\\" + fileName);
                byte[] sizeData = BitConverter.GetBytes(imageData.Length);

                // Prepend a header indicating the type of data
                string header = "image:";
                byte[] headerBytes = Encoding.ASCII.GetBytes(header);

                // Combine the header, size data, and image data into a single byte array
                byte[] dataToSend = new byte[headerBytes.Length + sizeData.Length + imageData.Length];
                Buffer.BlockCopy(headerBytes, 0, dataToSend, 0, headerBytes.Length);
                Buffer.BlockCopy(sizeData, 0, dataToSend, headerBytes.Length, sizeData.Length);
                Buffer.BlockCopy(imageData, 0, dataToSend, headerBytes.Length + sizeData.Length, imageData.Length);

                // Send the data
                ns.WriteAsync(dataToSend, 0, dataToSend.Length);
                ns.Flush();

            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button4_Click(object sender, EventArgs e)
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
    }
}



