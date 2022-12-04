using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UDPLibrary
{
    class ListenClass
    {
        // This is the main entry point of the program.
        public static void Main(string[] args)
        {
            // Create a new instance of the external server (B).
            ExternalServer server = new ExternalServer();

            // Start listening for incoming connections.
            server.Start(8000);

            // At this point, the external server (B) is listening for incoming connections from other servers (C) and clients (A).
        }
    }
    class ServerClass
    {
        // This is the main entry point of the program.
        public static void Main(string[] args)
        {
            // Create a new instance of the server (C).
            Server server = new Server(IPAddress.Parse("127.0.0.1"), 8000);

            // Connect to the external server (B).
            server.Start();

            // At this point, the server (C) is connected to the external server (B) and has been added to its list of connected servers.
        }
    }
    // This class represents a server (C) in the scenario described above.
    public class Server
    {
        // This is the UDP socket used by the server (C) to communicate with other servers (C) and clients (A).
        private readonly Socket _socket;

        // This is the thread that is used to continuously listen for incoming data.
        private Thread _listenThread;

        // This is the external server's (B) IP address and port number.
        private readonly IPEndPoint _externalServerEndPoint;

        // This is the client's (A) IP address and port number, once a connection has been established.
        private IPEndPoint _clientEndPoint;

        public Server(IPAddress externalServerAddress, int externalServerPort)
        {
            // Create a UDP socket for the server (C).
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Bind the server's (C) socket to the local endpoint.
            _socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            // Save the external server's (B) IP address and port number.
            _externalServerEndPoint = new IPEndPoint(externalServerAddress, externalServerPort);
        }
        // This method is used to start listening for incoming data.
        public void Start()
        {
            // Start the listening thread.
            _listenThread = new Thread(ListenForData);
            _listenThread.Start();

            // Register the server (C) with the external server (B).
            byte[] data = Encoding.UTF8.GetBytes("REGISTER_SERVER|" + ((IPEndPoint)_socket.LocalEndPoint).Address + "," + ((IPEndPoint)_socket.LocalEndPoint).Port);
            _socket.SendTo(data, _externalServerEndPoint);
        }

        // This method is used to continuously listen for incoming data.
        private void ListenForData()
        {
            while (true)
            {
                // Receive incoming data from the socket.
                byte[] data = new byte[1024];
                EndPoint senderEndPoint = new IPEndPoint(IPAddress.Any, 0);
                int bytesReceived = _socket.ReceiveFrom(data, ref senderEndPoint);

                // Parse the received data as a string.
                string receivedString = Encoding.UTF8.GetString(data, 0, bytesReceived);
                string[] splitString = receivedString.Split('|');

                // Check the type of data that was received.
                if (splitString[0] == "HOLE_PUNCH")
                {
                    // The external server (B) is pretending to be the client (A), and is sending hole punching data.
                    // Parse the client's (A) IP address and port number.
                    string[] addressSplit = splitString[1].Split(',');
                    IPAddress clientAddress = IPAddress.Parse(addressSplit[0]);
                    int clientPort = int.Parse(addressSplit[1]);

                    // Save the client's (A) IP address and port number.
                    _clientEndPoint = new IPEndPoint(clientAddress, clientPort);
                }
            }
        }
    }
    // This class represents the client (A) in the scenario described above.
    public class Client
    {
        // This is the IP address and port number of the external server (B).
        private EndPoint _serverEndPoint;

        // This is the UDP socket used by the client (A) to communicate with the server (B).
        private readonly Socket _socket;

        // This is the client's (A) public IP address and port number.
        private IPEndPoint _clientEndPoint;

        // This is the IP address and port number of the server (C) that the client (A) wants to connect to.
        private EndPoint _targetServerEndPoint;

        public Client(IPAddress serverIP, int serverPort)
        {
            _serverEndPoint = new IPEndPoint(serverIP, serverPort);

            // Create a UDP socket for the client (A).
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Bind the client's (A) socket to the local endpoint.
            _socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        }

        // This method is used by the client (A) to request a list of all connected servers (C) from the server (B).
        public string[] RequestServerList()
        {
            // Send a request for the server list to the server (B).
            byte[] data = Encoding.UTF8.GetBytes("REQUEST_SERVER_LIST");
            _socket.SendTo(data, _serverEndPoint);

            // Receive the server list from the server (B).
            data = new byte[1024];
            int bytesReceived = _socket.ReceiveFrom(data, ref _serverEndPoint);
            string serverListString = Encoding.UTF8.GetString(data, 0, bytesReceived);
            // Parse the server list and return it as an array of strings.
            return serverListString.Split(",");
        }

        // This method is used by the client (A) to request a connection to a specific server (C) from the server (B).
        public void RequestConnection(IPAddress targetServerIP, int targetServerPort)
        {
            // Save the IP address and port number of the target server (C).
            _targetServerEndPoint = new IPEndPoint(targetServerIP, targetServerPort);
            // Save the client's (A) public IP address and port number.
            _clientEndPoint = (IPEndPoint)_socket.LocalEndPoint;
            // Send a request for a connection to the target server (C) to the server (B).
            byte[] data = Encoding.UTF8.GetBytes("REQUEST_CONNECTION|" + _clientEndPoint + "|" + _targetServerEndPoint);
            _socket.SendTo(data, _serverEndPoint);

            // Receive a response from the server (B) indicating whether the connection request was successful.
            data = new byte[1024];
            int bytesReceived = _socket.ReceiveFrom(data, ref _serverEndPoint);
            string response = Encoding.UTF8.GetString(data, 0, bytesReceived);

            if (response == "CONNECTION_REQUEST_ACCEPTED")
            {
                // The connection request was accepted by the server (B).
                // Perform the UDP hole punching technique to establish a connection between the client (A) and the target server (C).

                // Send a message to the target server (C) through the server (B).
                data = Encoding.UTF8.GetBytes("HOLE_PUNCHING|" + _clientEndPoint);
                _socket.SendTo(data, _targetServerEndPoint);

                // Receive a message from the target server (C) through the server (B).
                data = new byte[1024];
                bytesReceived = _socket.ReceiveFrom(data, ref _targetServerEndPoint);
                string message = Encoding.UTF8.GetString(data, 0, bytesReceived);

                // At this point, a connection has been established between the client (A) and the target server (C).
            }
            else
            {
                // The connection request was not accepted by the server (B).
                throw new Exception("Connection request was not accepted.");
            }
        }
    }
    // This class represents the external server (B) in the scenario described above.
    public class ExternalServer
    {
        // This is the UDP socket used by the external server (B) to communicate with other servers (C) and clients (A).
        private readonly Socket _socket;

        // This is a list of all connected servers (C).
        private readonly List<IPEndPoint> _serverList;

        // This is a thread that is used to continuously listen for incoming connections and requests.
        private Thread _listenThread;

        public ExternalServer()
        {
            // Create a UDP socket for the external server (B).
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Bind the external server's (B) socket to the local endpoint.
            _socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            // Initialize the list of connected servers (C).
            _serverList = new List<IPEndPoint>();
        }
        // This method is used to start listening for incoming connections and requests.
        public void Start(int port)
        {
            // Save the external server's (B) public IP address and port number.
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Any, port);

            // Bind the external server's (B) socket to the public endpoint.
            _socket.Bind(serverEndPoint);

            // Start the listening thread.
            _listenThread = new Thread(ListenForConnections);
            _listenThread.Start();
        }

        // This method is used to continuously listen for incoming connections and requests.
        private void ListenForConnections()
        {
            while (true)
            {
                // Receive incoming data from the socket.
                byte[] data = new byte[1024];
                EndPoint senderEndPoint = new IPEndPoint(IPAddress.Any, 0);
                int bytesReceived = _socket.ReceiveFrom(data, ref senderEndPoint);

                // Parse the received data as a string.
                string receivedString = Encoding.UTF8.GetString(data, 0, bytesReceived);
                string[] splitString = receivedString.Split('|');

                // Check the type of request that was received.
                if (splitString[0] == "REQUEST_SERVER_LIST")
                {
                    // The client (A) is requesting a list of connected servers (C).
                    // Create a string containing the list of connected servers (C).
                    string serverListString = "SERVER_LIST|";
                    foreach (IPEndPoint server in _serverList)
                    {
                        serverListString += server.Address + "," + server.Port + "|";
                    }

                    // Send the list of connected servers (C) to the client (A).
                    byte[] serverListData = Encoding.UTF8.GetBytes(serverListString);
                    _socket.SendTo(serverListData, senderEndPoint);
                }
                else if (splitString[0] == "REGISTER_SERVER")
                {
                    // A new server (C) is trying to register with the external server (B).
                    // Add the server (C) to the list of connected servers.
                    _serverList.Add(new IPEndPoint(IPAddress.Parse(splitString[1]), int.Parse(splitString[2])));
                }
                else if (splitString[0] == "HOLE_PUNCH")
                {
                    // The client (A) is requesting a connection to a server (C) using UDP hole punching.
                    // Parse the client's (A) and server's (C) IP addresses and port numbers.
                    IPAddress clientAddress = IPAddress.Parse(splitString[1]);
                    int clientPort = int.Parse(splitString[2]);
                    IPAddress serverAddress = IPAddress.Parse(splitString[3]);
                    int serverPort = int.Parse(splitString[4]);

                    // Create the endpoint for the client (A).
                    IPEndPoint clientEndPoint = new IPEndPoint(clientAddress, clientPort);

                    // Send a message to the client (A), pretending to be the server (C).
                    byte[] holePunchData = Encoding.UTF8.GetBytes("HOLE_PUNCH|" + serverAddress + "," + serverPort);
                    _socket.SendTo(holePunchData, clientEndPoint);

                    // Send a message to the server (C), pretending to be the client (A).
                    holePunchData = Encoding.UTF8.GetBytes("HOLE_PUNCH|" + clientAddress + "," + clientPort);
                    _socket.SendTo(holePunchData, new IPEndPoint(serverAddress, serverPort));
                }
            }
        }
    }
}
