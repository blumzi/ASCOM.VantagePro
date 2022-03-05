using System;
using System.Threading;
using System.Text;
using System.IO.Ports;
using System.Drawing;
using ASCOM.Utilities;

namespace ASCOM.VantagePro
{
    public class SerialPortFetcher : SerialProtocolFetcher
    {
        public const string Profile_SerialPort = "SerialPort";
        public const string Profile_SerialSpeed = "SerialSpeed";
        public const int defaultSpeed = 19200;

        public string ComPort { get; set; }
        public int Speed { get; set; } = defaultSpeed;

        public SerialPortFetcher()
        {
            ReadLowerProfile();
            lowerFetcher = this;
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        public override void ReadLowerProfile()
        {
            string str;
            using (Profile driverProfile = new Profile() { DeviceType = "ObservingConditions" })
            {
                str = driverProfile.GetValue(DriverId, Profile_SerialPort, string.Empty, "");
                if (!string.IsNullOrEmpty(str))
                    ComPort = str;
                driverProfile.GetValue(DriverId, Profile_SerialSpeed, string.Empty, defaultSpeed.ToString());

                str = driverProfile.GetValue(DriverId, Profile_SerialSpeed, string.Empty, defaultSpeed.ToString());
                if (!string.IsNullOrEmpty(str))
                    Speed = Convert.ToInt32(str);
            }
        }

        public override void WriteLowerProfile()
        {
            using (Profile driverProfile = new Profile() { DeviceType = "ObservingConditions" })
            {
                if (ComPort != null)
                    driverProfile.WriteValue(DriverId, Profile_SerialPort, ComPort.ToString());
                driverProfile.WriteValue(DriverId, Profile_SerialSpeed, Speed.ToString());
            }
        }

        public string Source
        {
            get
            {
                return $"[{ComPort}:{Speed}]";
            }
        }

        public override VantagePro.DataSourceClass DataSource
        {
            get
            {
                return new VantagePro.DataSourceClass
                {
                    Type = "serial",
                    Details = Source,
                };
            }
        }

        private SerialPort Open()
        {
            SerialPort serialPort = new System.IO.Ports.SerialPort
            {
                PortName = ComPort,
                BaudRate = Speed,
                ReadTimeout = 1000,
                ReadBufferSize = 100
            };

            try
            {
                serialPort.Open();
            }
            catch
            {
                throw;
            }
            return serialPort;
        }

        private bool Wakeup(SerialPort serialPort)
        {
            string op = "Serial.WakeUp";
            int[] rxBytes = new int[2];

            int attempt, maxAttempts = 3;
            for (attempt = 0; attempt < maxAttempts; attempt++)
            {
                serialPort.Write("\r");
                if ((rxBytes[0] = serialPort.ReadByte()) == '\n' && (rxBytes[1] = serialPort.ReadByte()) == '\r')
                {
                    #region trace
                    VantagePro.tl.LogMessage(op, $"{Source}: attempt: {attempt + 1}, Succeeded ([{rxBytes[0]:X2}], [{rxBytes[1]:X2}])");
                    #endregion
                    return true;
                }
                Thread.Sleep(1000);
            }

            return false;
        }

        public override string StationType
        {
            get
            {
                SerialPort serialPort;

                byte[] rxBytes;

                try
                {
                    serialPort = Open();
                }
                catch
                {
                    return "Unknown";
                }

                Wakeup(serialPort);
                serialPort.Write(GetStationTypeTxBytes, 0, GetStationTypeTxBytes.Length);
                Thread.Sleep(500);
                rxBytes = Encoding.ASCII.GetBytes(serialPort.ReadExisting());
                serialPort.Close();

                if (rxBytes.Length < 2 || rxBytes[0] != ACK || !ByteToStationModel.ContainsKey(rxBytes[1]))
                    return "Unknown:";

                return ByteToStationModel[rxBytes[1]];
            }
        }

        public override byte[] GetLoopDataBytes()
        {
            string op = $"Serial.GetLoopDataBytes";
            SerialPort serialPort = null;
            string error = null;

            try
            {
                serialPort = Open();
            }
            catch (Exception ex)
            {
                error = "Exception: " + ex.Message;
                goto BailOut;
            }

            if (!Wakeup(serialPort))
                goto BailOut;

            byte[] rxBytes = new byte[99];
            string txString = new string(GetLoopTxBytes);
            serialPort.Write(txString);
            #region trace
            VantagePro.tl.LogMessage(op, $"{Source}: Wrote: {txString}");
            #endregion

            int rxByte;
            if ((rxByte = serialPort.ReadByte()) != ACK)
            {
                error = $"{Source}: Got 0x{rxByte:X1} instead of ACK (existing: {serialPort.ReadExisting()})";
                goto BailOut;
            }
            #region trace
            VantagePro.tl.LogMessage(op, $"{Source}: Got ACK ([{rxByte:X2}])");
            #endregion

            Thread.Sleep(500);
            int nRxBytes;
            if ((nRxBytes = serialPort.Read(rxBytes, 0, rxBytes.Length)) != rxBytes.Length)
            {
                error = $"{Source}: Got {nRxBytes} bytes instead of {rxBytes.Length}";
                goto BailOut;
            }

            #region trace
            VantagePro.tl.LogMessage(op, $"{Source}: Successfully read {rxBytes.Length} bytes");
            #endregion

            serialPort.Close();
            return rxBytes;

        BailOut:
            #region trace
            if (!string.IsNullOrEmpty(error))
                VantagePro.tl.LogMessage(op, error);
            #endregion
            if (serialPort.IsOpen)
                serialPort.Close();
            return null;
        }

        public void Test(string port, ref string result, ref Color color)
        {
            #region trace
            string traceId = "Serial.Test";
            #endregion

            if (string.IsNullOrWhiteSpace(port))
            {
                #region trace
                VantagePro.tl.LogMessage(traceId, "Empty port");
                #endregion
                result = "Empty port";
                color = VantagePro.colorError;
                return;
            }
            ComPort = port;

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