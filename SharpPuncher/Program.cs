using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace UdpHolePunching
{
    public class StunServer
    {
        // The IP address of the server
        public IPAddress ServerAddress { get; set; }

        // The port number of the server
        public int ServerPort { get; set; }

        // The maximum number of pending connections in the connection queue
        public int MaxPendingConnections { get; set; }

        // The maximum size of the receive buffer
        public int ReceiveBufferSize { get; set; }

        // The maximum size of the send buffer
        public int SendBufferSize { get; set; }

        // The STUN message type
        public StunMessageType MessageType { get; set; }

        // The STUN transaction ID
        public byte[] TransactionId { get; set; }

        // The STUN attributes
        public List<StunAttribute> Attributes { get; set; }

        // The TcpListener used to listen for incoming connections
        private TcpListener listener;

        // The list of active connections
        private List<TcpClient> connections;

        // The buffer used to receive data
        private byte[] receiveBuffer;

        // The buffer used to send data
        private byte[] sendBuffer;

        // Initializes a new instance of the StunServer class
        public StunServer()
        {
            // Set the default server address and port number
            ServerAddress = IPAddress.Any;
            ServerPort = 3478;

            // Set the default maximum number of pending connections
            MaxPendingConnections = 10;

            // Set the default maximum size of the receive and send buffers
            ReceiveBufferSize = 4096;
            SendBufferSize = 4096;

            // Set the default STUN message type and transaction ID
            MessageType = StunMessageType.BindingRequest;
            TransactionId = new byte[12];
            Random rand = new Random();
            rand.NextBytes(TransactionId);

            // Set the default STUN attributes
            Attributes = new List<StunAttribute>();

            // Set the default TcpListener and connections
            listener = null;
            connections = new List<TcpClient>();

            // Set the default receive and send buffers
            receiveBuffer = new byte[ReceiveBufferSize];
            sendBuffer = new byte[SendBufferSize];
        }

        // Starts the STUN server
        public void Start()
        {
            // Create a new TcpListener with the server address and port number
            listener = new TcpListener(ServerAddress, ServerPort);

            // Start the TcpListener and listen for incoming connections
            listener.Start(MaxPendingConnections);
            listener.BeginAcceptTcpClient(OnAcceptTcpClient, null);
        }

        // Stops the STUN server
        public void Stop()
        {
            // Stop the TcpListener and close all active connections
            listener.Stop();
            foreach (TcpClient connection in connections)
            {
                connection.Close();
            }
        }

        // Called when a new TcpClient is accepted by the TcpListener
        private void OnAcceptTcpClient(IAsyncResult result)
        {
            // Get the TcpClient and add it to the list of active connections
            TcpClient client = listener.EndAcceptTcpClient(result);
            connections.Add(client);

            // Begin receiving data from the TcpClient
            client.GetStream().BeginRead(receiveBuffer, 0, ReceiveBufferSize, OnReceiveData, client);
        }
        // Called when data is received from a TcpClient
        private void OnReceiveData(IAsyncResult result)
        {
            // Get the TcpClient and the number of bytes received
            TcpClient client = (TcpClient)result.AsyncState;
            int bytesReceived = client.GetStream().EndRead(result);

            // Parse the STUN message
            StunMessage message = ParseMessage(receiveBuffer, bytesReceived);

            // Send a STUN response
            SendResponse(client, message);
        }

        // Called when data is sent to a TcpClient
        private void OnSendData(IAsyncResult result)
        {
            // Get the TcpClient and the number of bytes sent
            TcpClient client = (TcpClient)result.AsyncState;
            client.GetStream().EndWrite(result);

            // Begin receiving data from the TcpClient
            client.GetStream().BeginRead(receiveBuffer, 0, ReceiveBufferSize, OnReceiveData, client);
        }
        // Parses a STUN message
        private StunMessage ParseMessage(byte[] buffer, int length)
        {
            // Create a new STUN message
            StunMessage message = new StunMessage();

            // Set the STUN message type
            message.Type = (StunMessageType)(buffer[0] << 8 | buffer[1]);

            // Set the STUN message length
            message.Length = (ushort)(buffer[2] << 8 | buffer[3]);

            // Set the STUN transaction ID
            message.TransactionId = new byte[StunMessage.TransactionIdLength];
            Buffer.BlockCopy(buffer, 4, message.TransactionId, 0, StunMessage.TransactionIdLength);

            // Parse the STUN attributes
            int offset = 20;
            while (offset < length)
            {
                // Get the STUN attribute type
                StunAttributeType attributeType = (StunAttributeType)(buffer[offset] << 8 | buffer[offset + 1]);

                // Get the STUN attribute length
                ushort attributeLength = (ushort)(buffer[offset + 2] << 8 | buffer[offset + 3]);

                // Get the STUN attribute value
                byte[] attributeValue = new byte[attributeLength];
                Buffer.BlockCopy(buffer, offset + 4, attributeValue, 0, attributeLength);

                // Create a new STUN attribute
                StunAttribute attribute = new StunAttribute(attributeType, attributeValue);

                // Add the STUN attribute to the message
                message.Attributes.Add(attribute);

                // Move to the next STUN attribute
                offset += 4 + attributeLength;
            }

            // Return the STUN message
            return message;
        }
        // Sends a STUN response
        private void SendResponse(TcpClient client, StunMessage request)
        {
            // Create a STUN binding response
            StunMessage response = CreateBindingResponse(request);

            // Get the length of the STUN message
            int messageLength = StunMessage.HeaderLength;
            foreach (StunAttribute attribute in response.Attributes)
            {
                messageLength += Stun.HeaderLength + attribute.Value.Length;
            }

            // Set the STUN message type
            sendBuffer[0] = (byte)((int)response.Type >> 8);
            sendBuffer[1] = (byte)((int)response.Type & 255);

            // Set the STUN message length
            sendBuffer[2] = (byte)(messageLength >> 8);
            sendBuffer[3] = (byte)(messageLength & 255);

            // Set the STUN transaction ID
            Buffer.BlockCopy(response.TransactionId, 0, sendBuffer, 4, StunMessage.TransactionIdLength);

            // Set the STUN attributes
            int offset = 20;
            foreach (StunAttribute attribute in response.Attributes)
            {
                // Set the STUN attribute type
                sendBuffer[offset] = (byte)((int)attribute.Type >> 8);
                sendBuffer[offset + 1] = (byte)((int)attribute.Type & 255);

                // Set the STUN attribute length
                sendBuffer[offset + 2] = (byte)(attribute.Length >> 8);
                sendBuffer[offset + 3] = (byte)(attribute.Length & 255);

                // Set the STUN attribute value
                Buffer.BlockCopy(attribute.ToByteArray(), 0, sendBuffer, offset + 4, attribute.Length);

                // Move to the next STUN attribute
                offset += 4 + attribute.Length;
            }

            // Send the STUN response
            client.GetStream().BeginWrite(sendBuffer, 0, messageLength, OnSendData, client);
        }
    }
    public class StunClient
    {
        // The STUN server to connect to
        private string stunServer;

        // The STUN server port to connect to
        private int stunPort;

        public StunClient(string stunServer, int stunPort)
        {
            this.stunServer = stunServer;
            this.stunPort = stunPort;
        }

        // Perform a STUN binding request to get the mapped IP address and port of the client
        public IPAddress GetMappedAddress()
        {
            // Create a STUN binding request message
            StunMessage request = new StunMessage(StunMessageType.BindingRequest);

            // Set the transaction ID of the request to a random value
            Random random = new Random();
            request.TransactionId = new byte[12];
            random.NextBytes(request.TransactionId);

            // Serialize the STUN request message to a byte array
            byte[] requestBytes = request.ToByteArray();

            // Send the STUN request to the STUN server
            IPEndPoint endPoint = new IPEndPoint(Dns.GetHostAddresses(stunServer)[0], stunPort);
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SendTo(requestBytes, endPoint);

            // Receive the STUN response from the STUN server
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] responseBytes = new byte[1024];
            int bytesReceived = socket.ReceiveFrom(responseBytes, ref remoteEndPoint);

            // Deserialize the STUN response message from the byte array
            StunMessage response = StunMessage.FromByteArray(responseBytes, bytesReceived);

            // If the STUN response is a binding response, get the mapped IP address and port
            if (response.Type == StunMessageType.BindingResponse)
            {
                // Get the XOR-MAPPED-ADDRESS attribute from the response
                StunXorMappedAddressAttribute xorMappedAddress = (StunXorMappedAddressAttribute)response.Attributes[StunAttributeType.XorMappedAddress];

                // XOR the mapped IP address and port with the transaction ID to get the original values
                IPAddress mappedAddress = xorMappedAddress.Address;
                int mappedPort = xorMappedAddress.Port ^ BitConverter.ToInt16(request.TransactionId, 0);

                // Return the mapped IP address and port
                return new IPEndPoint(mappedAddress, mappedPort).Address;
            }

            // If the STUN response is not a binding response, throw an exception
            else
            {
                throw new Exception("Invalid STUN response");
            }
        }
    }
    public class StunMessage
    {

        // The length of the STUN message, in bytes
        public int Length { get; set; }

        // The magic cookie value used in STUN messages
        public static readonly uint MagicCookie = 0x2112A442;

        // The type of the STUN message
        public StunMessageType Type { get; set; }

        // The length of the transaction ID in bytes
        public const int TransactionIdLength = 12;

        // The length of the STUN message header in bytes
        public const int HeaderLength = 20;

        // The transaction ID of the STUN message
        public byte[] TransactionId { get; set; }

        // The list of STUN attributes in the STUN message
        public List<StunAttribute> Attributes { get; set; }
        public StunMessage()
        {
            TransactionId = Array.Empty<byte>();
            Attributes = new List<StunAttribute>();
        }
        public StunMessage(StunMessageType type)
        {
            Type = type;
            TransactionId = Array.Empty<byte>();
            Attributes = new List<StunAttribute>();
        }

        public StunMessage(StunMessageType type, byte[] transactionId)
        {
            Type = type;
            TransactionId = transactionId;
            Attributes = new List<StunAttribute>();
        }
        // Serialize the STUN message to a byte array
        public byte[] ToByteArray()
        {
            // Create a memory stream for serializing the STUN message
            MemoryStream stream = new MemoryStream();

            // Write the STUN message type to the stream
            stream.WriteByte((byte)((int)Type >> 8));
            stream.WriteByte((byte)((int)Type & 0xff));

            // Write the STUN message length to the stream
            stream.WriteByte((byte)(Length >> 8));
            stream.WriteByte((byte)(Length & 0xff));

            // Write the STUN transaction ID to the stream
            stream.Write(TransactionId, 0, TransactionId.Length);
            // Write the STUN attributes to the stream
            foreach (StunAttribute attribute in Attributes)
            {
                byte[] attributeBytes = attribute.ToByteArray();
                stream.Write(attributeBytes, 0, attributeBytes.Length);
            }

            // Return the serialized STUN message as a byte array
            return stream.ToArray();
        }

        // Deserialize a STUN message from a byte array
        public static StunMessage FromByteArray(byte[] bytes, int length)
        {
            // Create a memory stream for the byte array
            MemoryStream stream = new MemoryStream(bytes, 0, length);

            // Read the STUN message type and length from the stream
            int messageType = stream.ReadByte() << 8 | stream.ReadByte();
            int messageLength = stream.ReadByte() << 8 | stream.ReadByte();

            // Read the transaction ID from the stream
            byte[] transactionId = new byte[12];
            stream.Read(transactionId, 0, 12);

            // Create a STUN message object with the type and transaction ID
            StunMessage message = new StunMessage((StunMessageType)messageType, transactionId);

            // Read the STUN attributes from the stream
            while (stream.Position < length)
            {
                // Read the attribute type and length from the stream
                int attributeType = stream.ReadByte() << 8 | stream.ReadByte();
                int attributeLength = stream.ReadByte() << 8 | stream.ReadByte();

                // Create a memory stream for the STUN attribute value
                MemoryStream attributeStream = new MemoryStream(bytes, (int)stream.Position, attributeLength);

                // Deserialize the STUN attribute from the memory stream
                StunAttribute attribute = StunAttribute.FromByteArray(attributeStream, (StunAttributeType)attributeType);

                // Add the STUN attribute to the message
                message.Attributes.Add(attribute);

                // Skip past the attribute value in the stream
                stream.Position += attributeLength;
            }

            // Return the STUN message
            return message;
        }
    }
    public enum StunMessageType
    {
        BindingRequest = 0x0001,
        BindingResponse = 0x0101,
        BindingErrorResponse = 0x0111,
        SharedSecretRequest = 0x0002,
        SharedSecretResponse = 0x0102,
        SharedSecretErrorResponse = 0x0112
    }
    public enum StunAttributeType
    {
        MappedAddress = 0x0001,
        ResponseAddress = 0x0002,
        ChangeRequest = 0x0003,
        SourceAddress = 0x0004,
        ChangedAddress = 0x0005,
        Username = 0x0006,
        Password = 0x0007,
        MessageIntegrity = 0x0008,
        ErrorCode = 0x0009,
        UnknownAttribute = 0x000A,
        ReflectedFrom = 0x000B,
        Realm = 0x0014,
        Nonce = 0x0015,
        XorMappedAddress = 0x8020,
        XorOnly = 0x0021,
        ServerName = 0x8022,
        SecondaryAddress = 0x8050
    }

    public abstract class StunAttribute
    {
        // The type of the STUN attribute
        public StunAttributeType Type { get; set; }

        // The length of the STUN attribute, in bytes
        public int Length { get; set; }

        // Serialize the STUN attribute to a byte array
        public abstract byte[] ToByteArray();

        public StunAttribute(StunAttributeType type)
        {
            Type = type;
        }
        // Parses a STUN attribute from a byte array
        public abstract void FromByteArray(byte[] buffer, ushort length);

        // Deserialize the attribute value from the memory stream
        public abstract void FromByteArray(MemoryStream stream);

        // Parses a STUN attribute from a byte array
        public static StunAttribute FromByteArray(byte[] buffer, int length)
        {
            // Get the STUN attribute type
            StunAttributeType attributeType = (StunAttributeType)(buffer[0] << 8 | buffer[1]);

            // Get the STUN attribute length
            ushort attributeLength = (ushort)(buffer[2] << 8 | buffer[3]);

            // Get the STUN attribute value
            byte[] attributeValue = new byte[attributeLength];
            Buffer.BlockCopy(buffer, 4, attributeValue, 0, attributeLength);

            // Create a new STUN attribute of the correct type
            StunAttribute attribute = null;
            switch (attributeType)
            {
                // Create a new STUN MAPPED-ADDRESS attribute
                case StunAttributeType.MappedAddress:
                    attribute = new StunMappedAddressAttribute();
                    break;

                // Create a new STUN XOR-MAPPED-ADDRESS attribute
                case StunAttributeType.XorMappedAddress:
                    attribute = new StunXorMappedAddressAttribute();
                    break;

                // Create a new STUN MESSAGE-INTEGRITY attribute
                case StunAttributeType.MessageIntegrity:
                    attribute = new StunMessageIntegrityAttribute();
                    break;

                // Create a new STUN ERROR-CODE attribute
                case StunAttributeType.ErrorCode:
                    attribute = new StunErrorCodeAttribute();
                    break;

                // Create a new STUN UNKNOWN-ATTRIBUTES attribute
                case StunAttributeType.UnknownAttribute:
                    attribute = new StunUnknownAttribute();
                    break;

                // Otherwise, create a new STUN UNKNOWN-ATTRIBUTE attribute
                default:
                    attribute = new StunUnknownAttribute();
                    break;
            }

            // Set the STUN attribute type
            attribute.Type = attributeType;

            // Set the STUN attribute length
            attribute.Length = attributeLength;

            // Set the STUN attribute value
            attribute.FromByteArray(attributeValue, attributeLength);

            // Return the STUN attribute
            return attribute;
        }

        // Create a STUN attribute object with the given type
        public static StunAttribute CreateAttribute(StunAttributeType attributeType)
        {
            // Check the attribute type and return the corresponding STUN attribute object
            switch (attributeType)
            {
                case StunAttributeType.MappedAddress:
                    return new StunMappedAddressAttribute();
                case StunAttributeType.XorMappedAddress:
                    return new StunXorMappedAddressAttribute();
                case StunAttributeType.MessageIntegrity:
                    return new StunMessageIntegrityAttribute();
                case StunAttributeType.ErrorCode:
                    return new StunErrorCodeAttribute();
                default:
                    return new StunUnknownAttribute();
            }
        }
    }
    public class StunXorMappedAddressAttribute : StunAttribute
    {
        // The address and port of the mapped IP address and port
        public IPAddress Address { get; set; }
        public int Port { get; set; }

        // The XOR-ed address and port
        private long xorAddress;
        private long xorPort;

        public StunXorMappedAddressAttribute() : base(StunAttributeType.MappedAddress)
        {
        }

        public override byte[] ToByteArray()
        {
            // Create a memory stream for the STUN attribute
            MemoryStream stream = new MemoryStream();

            // Write the STUN attribute type to the stream
            stream.WriteByte((byte)((int)Type >> 8));
            stream.WriteByte((byte)((int)Type & 255));

            // Write the XOR-ed address and port to the stream
            stream.WriteByte((byte)(xorAddress >> 24));
            stream.WriteByte((byte)(xorAddress >> 16));
            stream.WriteByte((byte)(xorAddress >> 8));
            stream.WriteByte((byte)(xorAddress & 255));
            stream.WriteByte((byte)(xorPort >> 8));
            stream.WriteByte((byte)(xorPort & 255));

            // Return the STUN attribute as a byte array
            return stream.ToArray();
        }

        public override void FromByteArray(MemoryStream stream)
        {
            // Read the XOR-ed address and port from the stream
            xorAddress = stream.ReadByte() << 24 | stream.ReadByte() << 16 | stream.ReadByte() << 8 | stream.ReadByte();
            xorPort = stream.ReadByte() << 8 | stream.ReadByte();

            // XOR the XOR-ed address and port with the magic cookie to get the original values
            Address = new IPAddress((uint)xorAddress ^ StunMessage.MagicCookie);
            Port = (int)(xorPort ^ StunMessage.MagicCookie);
        }

        // Update the value of the attribute with the XOR-address
        public void Update(IPEndPoint endPoint)
        {
            // XOR the address and port with the magic cookie to get the XOR-ed values
            xorAddress = (int)endPoint.Address.Address ^ StunMessage.MagicCookie;
            xorPort = endPoint.Port ^ StunMessage.MagicCookie;

            // Update the length of the attribute
            Length = 8;
        }
        // Parses a STUN XOR-MAPPED-ADDRESS attribute from a byte array
        public override void FromByteArray(byte[] buffer, ushort length)
        {
            // Verify the STUN attribute length
            if (length != 8)
                throw new StunException("Invalid STUN XOR-MAPPED-ADDRESS attribute length.");

            // Get the STUN XOR-MAPPED-ADDRESS family
            AddressFamily family = (AddressFamily)buffer[4];

            // Get the STUN XOR-MAPPED-ADDRESS port
            Port = (ushort)(buffer[5] ^ (MagicCookie >> 8) ^ buffer[7]);

            // Get the STUN XOR-MAPPED-ADDRESS address
            uint address = BitConverter.ToUInt32(buffer, 4);
            address = address ^ MagicCookie ^ BitConverter.ToUInt32(buffer, 0);
            Address = new IPAddress(address);
        }
    }
    public class StunMappedAddressAttribute : StunAttribute
    {
        // The address family of the mapped address
        public AddressFamily Family { get; set; }

        // The port number of the mapped address
        public ushort Port { get; set; }

        // The IP address of the mapped address
        public IPAddress Address { get; set; }

        public StunMappedAddressAttribute() : base(StunAttributeType.MappedAddress)
        {
        }

        // Deserialize the attribute value from the memory stream
        public override void FromByteArray(MemoryStream stream)
        {
            // Read the address family and port number from the stream
            byte family = (byte)stream.ReadByte();
            byte[] portBytes = new byte[2];
            stream.Read(portBytes, 0, 2);
            Port = (ushort)(portBytes[0] << 8 | portBytes[1]);

            // Read the IP address from the stream
            byte[] addressBytes = new byte[4];
            stream.Read(addressBytes, 0, 4);

            // Convert the address family and IP address to a System.Net.IPAddress object
            if (family == 1)
            {
                Family = AddressFamily.InterNetwork;
                Address = new IPAddress(addressBytes);
            }
            else if (family == 2)
            {
                Family = AddressFamily.InterNetworkV6;
                Address = new IPAddress(addressBytes);
            }
            else
            {
                Family = AddressFamily.Unknown;
                Address = IPAddress.None;
            }

            // Set the attribute length
            Length = 8;
        }

        // Parses a STUN MAPPED-ADDRESS attribute from a byte array
        public override void FromByteArray(byte[] buffer, ushort length)
        {
            // Verify the STUN attribute length
            if (length != 8)
                throw new StunException("Invalid STUN MAPPED-ADDRESS attribute length.");

            // Get the STUN address family
            AddressFamily family = (AddressFamily)(buffer[1] << 8 | buffer[2]);

            // Get the STUN port
            Port = (ushort)(buffer[3] << 8 | buffer[4]);

            // Get the STUN address
            Address = new(buffer.Skip(4).Take(4).ToArray());
        }

        // Serialize the attribute value to a byte array
        public override byte[] ToByteArray()
        {
            // Convert the address family and IP address to byte arrays
            byte family;
            byte[] addressBytes;
            if (Family == AddressFamily.InterNetwork)
            {
                family = 1;
                addressBytes = Address.GetAddressBytes();
            }
            else if (Family == AddressFamily.InterNetworkV6)
            {
                family = 2;
                addressBytes = Address.GetAddressBytes();
            }
            else
            {
                family = 0;
                addressBytes = new byte[4];
            }

            // Convert the port number to a byte array
            byte[] portBytes = new byte[2];
            portBytes[0] = (byte)(Port >> 8);
            portBytes[1] = (byte)Port;

            // Return the concatenation of the address family, port number, and IP address as a byte array
            return new byte[] { family }.Concat(portBytes).Concat(addressBytes).ToArray();
        }
    }
    public class StunMessageIntegrityAttribute : StunAttribute
    {
        // The HMAC-SHA1 message integrity value
        public byte[] Value { get; set; }

        // The STUN HMAC-SHA1 value
        public byte[] HMAC { get; private set; }

        // Creates a new STUN MESSAGE-INTEGRITY attribute
        public StunMessageIntegrityAttribute() : base(StunAttributeType.MessageIntegrity) { }

        // Parses a STUN MESSAGE-INTEGRITY attribute from a byte array
        public override void FromByteArray(byte[] buffer, ushort length)
        {
            // Verify the STUN attribute length
            if (length != 20)
                throw new StunException("Invalid STUN MESSAGE-INTEGRITY attribute length.");

            // Get the STUN HMAC-SHA1 value
            HMAC = new byte[20];
            Buffer.BlockCopy(buffer, 0, HMAC, 0, 20);
        }

        // Converts the STUN MESSAGE-INTEGRITY attribute to a byte array
        public override byte[] ToByteArray()
        {
            // Create a new byte array with the correct size
            byte[] buffer = new byte[24];

            // Set the STUN attribute type
            ushort type = (ushort)Type;
            buffer[0] = (byte)(type >> 8);
            buffer[1] = (byte)(type & 0xff);

            // Set the STUN attribute length
            ushort length = 20;
            buffer[2] = (byte)(length >> 8);
            buffer[3] = (byte)(length & 0xff);

            // Set the STUN HMAC-SHA1 value
            Buffer.BlockCopy(HMAC, 0, buffer, 4, 20);

            // Return the byte array
            return buffer;
        }

        // Deserialize the attribute value from the memory stream
        public override void FromByteArray(MemoryStream stream)
        {
            // Read the HMAC-SHA1 value from the stream
            Value = new byte[20];
            stream.Read(Value, 0, 20);

            // Set the attribute length
            Length = 20;
        }
    }
    public class StunErrorCodeAttribute : StunAttribute
    {
        // The error code value
        public int ErrorCode { get; set; }

        // The error reason phrase
        public string ReasonPhrase { get; set; }

        public StunErrorCodeAttribute() : base(StunAttributeType.ErrorCode)
        {
        }

        // Deserialize the attribute value from the memory stream
        public override void FromByteArray(MemoryStream stream)
        {
            // Read the error code value from the stream
            byte[] errorCodeBytes = new byte[3];
            stream.Read(errorCodeBytes, 0, 3);
            ErrorCode = errorCodeBytes[0] * 100 + errorCodeBytes[1] * 10 + errorCodeBytes[2];

            // Read the error reason phrase from the stream
            byte[] reasonPhraseBytes = new byte[Length - 4];
            stream.Read(reasonPhraseBytes, 0, Length - 4);
            ReasonPhrase = Encoding.ASCII.GetString(reasonPhraseBytes);

            // Set the attribute length
            Length = 4 + reasonPhraseBytes.Length;
        }

        // Serialize the attribute value to a byte array
        public override byte[] ToByteArray()
        {
            // Convert the error code value to a byte array
            byte[] errorCodeBytes = new byte[3];
            errorCodeBytes[0] = (byte)(ErrorCode / 100);
            errorCodeBytes[1] = (byte)(ErrorCode / 10 % 10);
            errorCodeBytes[2] = (byte)(ErrorCode % 10);

            // Convert the error reason phrase to a byte array
            byte[] reasonPhraseBytes = Encoding.ASCII.GetBytes(ReasonPhrase);

            // Return the concatenation of the error code value, padding, and error reason phrase as a byte array
            return errorCodeBytes.Concat(new byte[1]).Concat(reasonPhraseBytes).ToArray();
        }
        // Parses a STUN ERROR-CODE attribute from a byte array
        public override void FromByteArray(byte[] buffer, ushort length)
        {
            // Verify the STUN attribute length
            if (length < 4 || (length - 4) % 4 != 0)
                throw new StunException("Invalid STUN ERROR-CODE attribute length.");

            // Get the STUN error code
            ushort code = (ushort)(buffer[3] << 8 | buffer[4]);
            ErrorCode = code;

            // Get the STUN error reason phrase
            string reason = Encoding.UTF8.GetString(buffer, 5, length - 4);
            ReasonPhrase = reason.TrimEnd('\0');
        }
    }
    public class StunUnknownAttribute : StunAttribute
    {
        // The STUN unknown attribute types
        public ushort[] UnknownAttributes { get; private set; }

        // The list of unknown attribute types
        public List<StunAttributeType> AttributeTypes { get; set; }

        public StunUnknownAttribute() : base(StunAttributeType.UnknownAttribute)
        {
        }

        // Deserialize the attribute value from the memory stream
        public override void FromByteArray(MemoryStream stream)
        {
            // Read the list of unknown attribute types from the stream
            AttributeTypes = new List<StunAttributeType>();
            while (Length > 0)
            {
                // Read the attribute type from the stream
                byte[] attributeTypeBytes = new byte[2];
                stream.Read(attributeTypeBytes, 0, 2);
                StunAttributeType attributeType = (StunAttributeType)(attributeTypeBytes[0] << 8 | attributeTypeBytes[1]);

                // Add the attribute type to the list
                AttributeTypes.Add(attributeType);

                // Decrement the attribute length
                Length -= 2;
            }

            // Set the attribute length
            Length = AttributeTypes.Count * 2;
        }

        // Serialize the attribute value to a byte array
        public override byte[] ToByteArray()
        {
            // Convert the list of unknown attribute types to a byte array
            byte[] attributeTypesBytes = new byte[AttributeTypes.Count * 2];
            for (int i = 0; i < AttributeTypes.Count; i++)
            {
                byte[] attributeTypeBytes = BitConverter.GetBytes((ushort)AttributeTypes[i]);
                attributeTypesBytes[i * 2] = attributeTypeBytes[0];
                attributeTypesBytes[i * 2 + 1] = attributeTypeBytes[1];
            }

            // Return the byte array of unknown attribute types
            return attributeTypesBytes;
        }
        // Parses a STUN UNKNOWN-ATTRIBUTES attribute from a byte array
        public override void FromByteArray(byte[] buffer, ushort length)
        {
            // Verify the STUN attribute length
            if (length % 2 != 0)
                throw new StunException("Invalid STUN UNKNOWN-ATTRIBUTES attribute length.");

            // Get the STUN unknown attribute types
            ushort[] types = new ushort[length / 2];
            for (int i = 0; i < length; i += 2)
            {
                ushort type = (ushort)(buffer[i] << 8 | buffer[i + 1]);
                types[i / 2] = type;
            }

            // Set the STUN unknown attribute types
            UnknownAttributes = types;
        }
    }
    public class StunException : Exception
    {
        // Creates a new STUN exception
        public StunException(string message) : base(message) { }
    }
}