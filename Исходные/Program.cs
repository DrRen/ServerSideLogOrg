using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace ServerSideLogOrg {
    class Program {
        private static readonly Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static readonly List < Socket > clientSockets = new List < Socket > ();
        private
        const int BUFFER_SIZE = 2048;
        private
        const int PORT = 3333;
        private static readonly byte[] buffer = new byte[BUFFER_SIZE];
        private static bool fileState;
        private static string fileName;
        private static string Username;
        private static string Password;
        private static string Subject;
        private static string BodyText;
        private static string FromAddress;
        private static string Name;
        private static string Recipient;
        private static string Port;
        private static string Host;

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int HIDE = 0;
        const int SHOW = 5;

        static void Main(string[] args) {
            var handle = GetConsoleWindow();
            if (args.Length>0)
            if (args[0].Equals("-hide")) ShowWindow(handle, HIDE);
            Console.Title = "Server";
            SetupServer();
            Console.ReadLine(); // When we press enter close everything
            CloseAllSockets();
        }

        private static void SetupServer() {
            Console.WriteLine("Setting up server...");
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, PORT));
            serverSocket.Listen(0);
            serverSocket.BeginAccept(AcceptCallback, null);
            Console.WriteLine("Server setup complete");
        }

        private static void CloseAllSockets() {
            foreach(Socket socket in clientSockets) {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }

            serverSocket.Close();
        }

        private static void AcceptCallback(IAsyncResult AR) {
            Socket socket;

            try {
                socket = serverSocket.EndAccept(AR);
            } catch (ObjectDisposedException) // I cannot seem to avoid this (on exit when properly closing sockets)
            {
                return;
            }

            clientSockets.Add(socket);
            socket.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, socket);
            Console.WriteLine("Client connected, waiting for request...");
            serverSocket.BeginAccept(AcceptCallback, null);
        }
        public static void SendMail(string smtpServer, string from, string password,
        string mailto, string caption, string message, string username, string attachFile = null)
        {
            try
            {
                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(from, username);
                mail.To.Add(new MailAddress(mailto));
                mail.Subject = caption;
                mail.Body = message;
                if (!string.IsNullOrEmpty(attachFile))
                    mail.Attachments.Add(new Attachment(attachFile));
                SmtpClient client = new SmtpClient();
                client.Host = smtpServer;
                client.Port = 587;
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(from.Split('@')[0], password);
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Send(mail);
                mail.Dispose();
            }
            catch (Exception e)
            {
                throw new Exception("Mail.Send: " + e.Message);
            }
        }

        private static void ReceiveCallback(IAsyncResult AR) {
            Socket current = (Socket) AR.AsyncState;
            int received;

            try {
                received = current.EndReceive(AR);
                byte[] recBuf = new byte[received];
                Array.Copy(buffer, recBuf, received);
                string text = Encoding.Default.GetString(recBuf);
                if (!fileState)
                    Console.Write("Received Text: ");

                if (fileState) {
                    if (text.ToLower() == "file finish")
                    {
                        fileState = false;
                        Console.WriteLine("File received");
                    }
                    else {
                        File.AppendAllText(fileName, text);
                    }
                }
                else if (text.ToLower() == "exit") // Client wants to exit gracefully
                {
                    Console.WriteLine(text);
                    // Always Shutdown before closing
                    current.Shutdown(SocketShutdown.Both);
                    current.Close();
                    clientSockets.Remove(current);
                    Console.WriteLine("Client disconnected");
                    return;
                } else if (text.ToLower().Split(':')[0] == "getfile") {
                    fileState = true;
                    fileName = text.ToLower().Split(':')[1];
                    if (File.Exists(fileName)) File.Delete(fileName);
                    Console.WriteLine("File receive started");
                    
                }
                else if (text.ToLower() == "send mail")
                {
                    SendMail(Host, Username, Password, Recipient, Subject, BodyText, Name, fileName);
                }
                else {
                    String[] parsed1 = text.Split(';');
                    foreach(var s in parsed1) {
                        var key = s.Split(':')[0];
                        var value = s.Split(':')[1];
                        Console.WriteLine(key == "Password" ? key + " --> ***********" : key + " --> " + value);
                        switch (key)
                        {
                            case "Host":
                                Host = value;
                                break;
                            case "Username":
                                Username = value;
                                break;
                            case "Port":
                                Port = value;
                                break;
                            case "Password":
                                Password = value;
                                break;
                            case "Subject":
                                Subject = value;
                                break;
                            case "BodyText":
                                BodyText = value;
                                break;
                            case "FromAddress":
                                FromAddress = value;
                                break;
                            case "Name":
                                Name = value;
                                break;
                            case "Recipient":
                                Recipient = value;
                                break;
                            default:
                                break;
                        }
                    }
                    Console.WriteLine("Got send data");
                }

                current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
            } catch (Exception) {
                Console.WriteLine("Client forcefully disconnected");
                // Don't shutdown because the socket may be disposed and its disconnected anyway.
                current.Close();
                clientSockets.Remove(current);
                return;
            }


        }
    }
}