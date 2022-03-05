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
            Socket socket;

            try
            {
                IPAddress.TryParse(Address, out IPAddress addr);
                IPEndPoint ipe = new IPEndPoint(addr, Port);
                socket = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(ipe);

                if (socket.Connected)
                {
                    VantagePro.tl.LogMessage(op, $"Connected to {Source}");
                    return socket;
                }
            }
            catch (Exception ex)
            {
               VantagePro.tl.LogMessage(op, $"Caught: {ex.Message} at {ex.StackTrace}");
                throw;
            }

            return null;
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
               VantagePro.tl.LogMessage("Socket.Close", $"{Source}: " + (socket.Connected ? "still connected" : "disconnected"));
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

            for (attempt = 0; attempt < maxAttempts; attempt++)
            {
                socket.Send(Encoding.ASCII.GetBytes("\r"), 1, 0);
                nRxBytes = socket.Receive(rxBytes, rxBytes.Length, 0);
                if (nRxBytes == 2 && Encoding.ASCII.GetString(rxBytes, 0, nRxBytes) == "\n\r")
                {
                    #region trace
                   VantagePro.tl.LogMessage(op, $"{Source}: attempt#: {attempt}, Success");
                    #endregion
                    return true;
                }
                Thread.Sleep(1000);
            }

            #region trace
           VantagePro.tl.LogMessage(op, $"{Source}: Failed after {attempt + 1} attempts");
            #endregion
            return false;
        }

        public override string StationType
        {
            get
            {
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

                    return ByteToStationModel[rxBytes[1]];
                }
                catch (Exception ex)
                {
                    #region trace
                    VantagePro.tl.LogMessage("StationType", $"Caught: {ex.Message} at {ex.StackTrace}");
                    #endregion
                    return null;
                }
            }
        }

        public override byte[] GetLoopDataBytes()
        {
            string op = $"Socket.GetLoopDataBytes";
            string error = null;
            Socket socket;

            if ((socket = Open()) == null || !Wakeup(socket))
                goto BailOut;

            Byte[] txBytes = Encoding.ASCII.GetBytes(GetLoopTxBytes);
            Byte[] rxBytes = new byte[99];

            socket.Send(txBytes, txBytes.Length, 0);
            socket.Receive(rxBytes, 1, 0);
            if (rxBytes[0] != ACK)
            {
                #region trace
                VantagePro.tl.LogMessage(op, $"{Source}: Got 0x{rxBytes[0]:X2} instead of 0x{ACK:X2}");
                #endregion
                goto BailOut;
            }
            #region trace
            VantagePro.tl.LogMessage(op, $"{Source}: Got ACK (0x{rxBytes[0]:X2})");
            #endregion

            int nRxBytes;
            if ((nRxBytes = socket.Receive(rxBytes, rxBytes.Length, 0)) != rxBytes.Length)
            {
                #region trace
                VantagePro.tl.LogMessage(op, $"{Source}: Failed to receive {rxBytes.Length} bytes, received only {nRxBytes} bytes");
                #endregion
                goto BailOut;
            }

            #region trace
            VantagePro.tl.LogMessage(op, $"{Source}: Received {rxBytes.Length} bytes");
            #endregion
            return rxBytes;

        BailOut:
            #region trace
            if (!string.IsNullOrEmpty(error))
            {
                VantagePro.tl.LogMessage(op, error);
            }
            #endregion

            if (socket != null)
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
            #region trace
            string traceId = "Socket.Test";
            #endregion

            if (string.IsNullOrWhiteSpace(address))
            {
                #region trace
                VantagePro.tl.LogMessage(traceId, "Empty IP address");
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

            string stationType = StationType;

            if (!string.IsNullOrEmpty(stationType))
            {
                #region trace
                VantagePro.tl.LogMessage(traceId, $"{Source}: Found a \"{stationType}\" type station.");
                #endregion
                result = $"Found a \"{stationType}\" type station at {Source}.";
                color = VantagePro.colorGood;
            }
            else
            {
                #region trace
                VantagePro.tl.LogMessage(traceId, $"Could not find a station at {Source}.");
                #endregion
                result = $"Could not find a station at {Source}.";
                color = VantagePro.colorError;
            }
        }
    }
}