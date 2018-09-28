using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TerminalChat
{
    class Program
    {
        public static int port = 6321;
        static TcpListener server;
        static bool serverStarted;
        static bool hasServer;
        static bool isClient = false;

        static List<ServerClient> clients;
        static List<ServerClient> disconnectionList;
        static Thread serverThread;
        static Thread clientThread;
        static Client thisClient;

        static void Main(string[] args)
        {
            AskIfHost();
            if (hasServer){
                serverThread = new Thread (IsServer);
                serverThread.Start();
            }
            if (isClient) {
                clientThread = new Thread(CreateClient);
                clientThread.Start();
            }
            


            Console.ReadLine();
        }

        static void CreateClient() {
            thisClient = new Client ();
            
        }

        static void AskIfHost()
        {
            Console.WriteLine("Welcome to Alec's Chat service.\n" +
                "Would you like to be the host? [Y]es [N]o");
            bool answered = false;
            while (!answered)
            {
                string input = Console.ReadLine();
                //ConsoleKeyInfo result = Console.ReadKey();
                if ((input == "Y") || input == "y")
                {
                    answered = true;
                    hasServer = true;
                }
                else if ((input == "N") || input == "n")
                {
                    answered = true;
                    hasServer = false;
                }
                else
                {
                    Console.WriteLine("Press Y or N on the keyboard.");
                }
            }
        }

        static void IsServer () {
            clients = new List<ServerClient>();
            disconnectionList = new List<ServerClient>();

            try
            {
                server = new TcpListener(IPAddress.Any, port);
                server.Start();

                StartListening();
                serverStarted = true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Socket Error: " + e.Message);
            }

            Update();
        }

        static void Update(){
            try {
                Console.WriteLine("Server started on port : " + port);
                while (true)
                {
                    if (!serverStarted) {
                        return;
                    }
                    foreach (ServerClient c in clients) {// potential error
                        //is client still connected?
                        if (!IsConnected(c.tcp)){
                            c.tcp.Close();
                            disconnectionList.Add(c);
                            continue;
                        }
                        else {
                            NetworkStream s = c.tcp.GetStream();
                            if (s.DataAvailable){
                                StreamReader reader = new StreamReader(s, true);
                                string data = reader.ReadLine();
                                if (data != null) {
                                    OnIncomingData(c, data);
                                }
                            }
                        }
                    }

                    for (int i = 0; i < disconnectionList.Count - 1; i ++) {

                        Broadcast(disconnectionList[i].clientName + " has disconnected.", clients);

                        clients.Remove(disconnectionList[i]);
                        disconnectionList.RemoveAt(i);
                    }

                }
            } catch (Exception e) {
                Console.WriteLine(e.Message);
            }
        }

        private static void StartListening()
        {
            server.BeginAcceptTcpClient(AcceptTcpClient, server);
        }
        static bool IsConnected(TcpClient c) {
            try
            {
                if (c != null && c.Client != null && c.Client.Connected){
                    if (c.Client.Poll(0, SelectMode.SelectRead)){
                        return !(c.Client.Receive(new byte[1], SocketFlags.Peek) == 0);
                    }
                    return true;
                }
                else
                    return false;
            } catch {
                return false;
            }
        }
        static void AcceptTcpClient(IAsyncResult ar)
        {
            TcpListener listenter = (TcpListener)ar.AsyncState;

            clients.Add (new ServerClient(listenter.EndAcceptTcpClient(ar)));
            StartListening();

            //Send a message to everyone , say someone has connected
            Broadcast("%NAME", new List<ServerClient>() { clients[clients.Count - 1] });// + " has connected ", clients);
        }
        static void OnIncomingData(ServerClient c, string data) {
            if (data.Contains("&NAME")){
                c.clientName = data.Split('|')[1];
                Broadcast(c.clientName + " has connected ", clients);
                return;
            }

            Broadcast(c.clientName + " : " + data, clients);
        }
        static void Broadcast (string data, List<ServerClient> cl){
            if (!isClient) {
                Console.WriteLine(data);
            }
            foreach (ServerClient c in cl)
            {
                try
                {
                    StreamWriter writer = new StreamWriter(c.tcp.GetStream());
                    writer.WriteLine(data);
                    writer.Flush();
                } catch (Exception e)
                {

                    Console.WriteLine("Write error: " + e.Message + " to client " + c.clientName);
                }

            }
        }


    }

    public class ServerClient{
        public TcpClient tcp;
        public string clientName;

        public ServerClient (TcpClient clientSocket){
            clientName = "Guest";
            tcp = clientSocket;
        }
    }

    public class Client {
        private bool socketReady;
        private TcpClient socket;
        private NetworkStream stream;
        private StreamWriter writer;
        private StreamReader reader;

        string clientName = "TestName";
        string messageToSend;

        private Thread T;
        private Thread inputThread;

        public Client () {
            T = new Thread (ConnectToServer);
            T.Start();
        }

        public void ConnectToServer() {
            bool connectedYet = false;
            while (!connectedYet){
                //if connected ignore
                if (socketReady) 
                    return;

                string host = "127.0.0.1";
                int port = Program.port;

                try {
                    socket = new TcpClient(host, port);
                    stream = socket.GetStream();
                    writer = new StreamWriter(stream);
                    reader = new StreamReader (stream);
                    socketReady = true;
                    connectedYet = true;

                } catch (Exception e) {
                    Console.WriteLine("Socket Error : " + e.Message);
                }
            }
            T = new Thread (Update);
            T.Start();
            inputThread = new Thread(SendMessage);
            inputThread.Start();
        }

        void Update()
        {
            while (true)
            {
                if (socketReady) {
                    if (stream.DataAvailable)
                    {
                        string data = reader.ReadLine();
                        if (data != null)
                            OnIncomingData(data);

                    }
                }
            }

        }
        void OnIncomingData(string data) {
            if (data == "%NAME") {
                SendMessage("&NAME|" + clientName);
                return;
            }
            Console.WriteLine( data );
        }

        void SendMessage() {
            while (true){
                if (!socketReady)
                    return;
                messageToSend = Console.ReadLine();
                writer.WriteLine(messageToSend);
                writer.Flush();
            }
        }
        void SendMessage(string data)
        {
            writer.WriteLine(data);
            writer.Flush();
        }


        }

    

}
