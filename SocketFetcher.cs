using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using ASCOM.Utilities;
using System.Drawing;

namespace ASCOM.VantagePro
{
	public class SocketFetcher : SerialProtocolFetcher
    {
        public const string Profile_IPAddress = "IPAddress";
        public const string Profile_IPPort = "IPPort";
        public const ushort defaultPort = 22222;

        private string _stationModel = "Unknown";

        public string Address { get; set; }
		public UInt16 Port { get; set; } = defaultPort;

		public SocketFetcher()
		{
            ReadLowerProfile();
            lowerFetcher = this;
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        public override void ReadLowerProfile()
        {
            using (Profile driverProfile = new Profile() { DeviceType = "ObservingConditions" })
            {
                Address = driverProfile.GetValue(DriverId, Profile_IPAddress, string.Empty, "");
                Port = Convert.ToUInt16(driverProfile.GetValue(DriverId, Profile_IPPort, string.Empty, defaultPort.ToString()));
            }
        }

        public override void WriteLowerProfile()
        {
            using (Profile driverProfile = new Profile() { DeviceType = "ObservingConditions" })
            {
                driverProfile.WriteValue(DriverId, Profile_IPAddress, Address);
                driverProfile.WriteValue(DriverId, Profile_IPPort, Port.ToString());
            }
        }

        public string Source
        {
            get
            {
                return $"[{Address}:{Port}]";
            }
        }

        private Socket Open()
        {
            string op = "Socket.Open";
            Socket socket = null;

            try
            {
                IPAddress.TryParse(Address, out IPAddress addr);
                IPEndPoint ipe = new IPEndPoint(addr, Port);
                socket = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                #region trace
                 VantagePro.LogMessage(op, $"Connecting to {Source}");
                #endregion
                socket.Connect(ipe);

                if (socket.Connected)
                {
                    #region trace
                     VantagePro.LogMessage(op, $"Connected to {Source}");
                    #endregion
                }
            }
            catch (Exception ex)
            {
                #region trace
                 VantagePro.LogMessage(op, $"Caught: {ex.Message} at {ex.StackTrace}");
                #endregion
            }

            return socket;
        }

        /// <summary>
        /// Disconnects and closes the IPsocket
        /// </summary>
        /// <returns>is IPsocket still connected</returns>
        private bool Close(Socket socket)
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Disconnect(true);
                #region trace
                 VantagePro.LogMessage("Socket.Close", $"{Source}: " + (socket.Connected ? "still connected" : "disconnected"));
                #endregion
                return true;
            }
            catch { }
            return false;
        }

        private bool Wakeup(Socket socket)
        {
            string op = $"Socket.WakeUp";

            Byte[] rxBytes = new byte[2];
            int nRxBytes, attempt, maxAttempts = 3;

            if (!socket.Connected) return false;

            for (attempt = 0; attempt < maxAttempts; attempt++)
            {
                socket.Send(Encoding.ASCII.GetBytes("\r"), 1, 0);
                nRxBytes = socket.Receive(rxBytes, rxBytes.Length, 0);
                if (nRxBytes == 2 && Encoding.ASCII.GetString(rxBytes, 0, nRxBytes) == "\n\r")
                {
                    #region trace
                     VantagePro.LogMessage(op, $"{Source}: attempt#: {attempt}, Success");
                    #endregion
                    return true;
                }
                Thread.Sleep(1000);
            }

            #region trace
             VantagePro.LogMessage(op, $"{Source}: Failed after {attempt + 1} attempts");
            #endregion
            return false;
        }

        public override string StationModel
        {
            get
            {
                string op = "Socket.StationType";
                byte[] rxBytes = new byte[2];
                byte[] txBytes = Encoding.ASCII.GetBytes(GetStationTypeTxBytes);
                int nRxBytes = 0;
                Socket socket;

                try
                {
                    socket = Open();
                    if (Wakeup(socket)) {
                        socket.Send(txBytes, txBytes.Length, 0);
                        nRxBytes = socket.Receive(rxBytes, rxBytes.Length, 0);
                        Close(socket);
                    }

                    if (nRxBytes < 2 || rxBytes[0] != ACK || !ByteToStationModel.ContainsKey(rxBytes[1]))
                        return null;

                    string model = ByteToStationModel[rxBytes[1]];
                    #region trace
                     VantagePro.LogMessage(op, $"Got model: {model}");
                    #endregion
                    return _stationModel;
                }
                catch (Exception ex)
                {
                    #region trace
                     VantagePro.LogMessage(op, $"Caught: {ex.Message} at {ex.StackTrace}");
                    #endregion
                    return null;
                }
            }

            set
            {
                _stationModel = value;
            }
        }

        public override byte[] GetLoopDataBytes()
        {
            string op = $"Socket.GetLoopDataBytes";
            string error;
            Socket socket = null;
            int tries;

            for (tries = 0; tries < 10; tries++)
            {
                socket = Open();
                if (socket == null || !socket.Connected)
                {
                    VantagePro.LogMessage(op, $"try#{tries}: Could not Open {DataSource}");
                    Thread.Sleep(500);
                    continue;
                }

                if (!Wakeup(socket))
                {
                    VantagePro.LogMessage(op, $"try#{tries}: Could not Wakeup {DataSource}");
                    Close(socket);
                    socket = null;
                    Thread.Sleep(500);
                    continue;
                }
                else
                    break;
            }

            if (socket == null || !socket.Connected)
            {
                error = $"Could not open and wakeup {DataSource} after {tries} tries.";
                goto BailOut;
            }

            Byte[] txBytes = Encoding.ASCII.GetBytes(GetLoopTxBytes);
            Byte[] rxBytes = new byte[99];

            socket.Send(txBytes, txBytes.Length, 0);
            socket.Receive(rxBytes, 1, 0);
            if (rxBytes[0] != ACK)
            {
                error = $"Got 0x{rxBytes[0]:X2} instead of ACK";
                goto BailOut;
            }
            #region trace
             VantagePro.LogMessage(op, $"{Source}: Got ACK (0x{rxBytes[0]:X2})");
            #endregion

            int nRxBytes;
            if ((nRxBytes = socket.Receive(rxBytes, rxBytes.Length, 0)) != rxBytes.Length)
            {
                error = $"Received {nRxBytes} instead of {rxBytes.Length}";
                goto BailOut;
            }
            #region trace
             VantagePro.LogMessage(op, $"{Source}: Received {rxBytes.Length} bytes");
            #endregion
            return rxBytes;

        BailOut:
            #region trace
            if (!string.IsNullOrEmpty(error))
            {
                 VantagePro.LogMessage(op, error);
            }
            #endregion

            if (socket != null && socket.Connected)
                socket.Close();
            return null;
        }

        public override VantagePro.DataSourceClass DataSource
        {
            get
            {
                return new VantagePro.DataSourceClass
                {
                    Type = "socket",
                    Details = Source,
                };
            }
        }

        public void Test(string address, string port, ref string result, ref Color color)
        {
            string op = "Socket.Test";

            #region trace
             VantagePro.LogMessage(op, "Start");
            #endregion
            if (string.IsNullOrWhiteSpace(address))
            {
                #region trace
                 VantagePro.LogMessage(op, "Empty IP address");
                #endregion
                result = "Empty IP address";
                color = VantagePro.colorError;
                return;
            }
            Address = address;

            if (string.IsNullOrWhiteSpace(port))
            {
                Port = defaultPort;
            }
            else
            {
                try
                {
                    Port = Convert.ToUInt16(port);
                }
                catch
                {
                    Port = defaultPort;
                }
            }
            #region trace
             VantagePro.LogMessage(op, $"Source: {Source}");
            #endregion

            string stationType = StationModel;

            if (!string.IsNullOrEmpty(stationType))
            {
                #region trace
                 VantagePro.LogMessage(op, $"{Source}: Found a \"{stationType}\" type station.");
                #endregion
                result = $"Found a \"{stationType}\" type station at {Source}.";
                color = VantagePro.colorGood;
            }
            else
            {
                #region trace
                 VantagePro.LogMessage(op, $"Could not find a station at {Source}.");
                #endregion
                result = $"Could not find a station at {Source}.";
                color = VantagePro.colorError;
            }
            #region trace
             VantagePro.LogMessage(op, "Done");
            #endregion
        }
    }
}