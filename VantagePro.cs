using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

using System.IO;
using System.Threading;
using System.Collections;
using ASCOM.Utilities;

using Weather;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;


namespace ASCOM.VantagePro
{
    public class VantagePro: WeatherStation
    {
        private readonly static Version version = new Version(1, 2);

        private readonly static string driverDescription = $"ASCOM VantagePro2 v1.2";

        public enum OpMode { None, File, Serial, IP };

        public const int serialPortSpeed = 19200;
        System.IO.Ports.SerialPort serialPort = new System.IO.Ports.SerialPort();

        public Socket IPsocket;

        private bool _connected = false;
        private bool _initialized = false;

        public static readonly string traceLogFile = $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\VantagePro v{version}.log";
        public static TraceLogger tl = new TraceLogger(traceLogFile, "VantagePro");

        private static readonly Util util = new Util();
        private const byte ACK = 0x6;

        public VantagePro() { }

        static VantagePro() { }

        private Dictionary<string, string> sensorData = null;
        private DateTime _lastDataRead = DateTime.MinValue;

        private static readonly Lazy<VantagePro> lazy = new Lazy<VantagePro>(() => new VantagePro()); // Singleton

        public static string DriverDescription
        {
            get
            {
                return driverDescription;
            }
        }

        public static string DriverId
        {
            get
            {
                return "ASCOM.VantagePro.ObservingConditions";
            }
        }

        public static string Profile_OpMode = "OperationMode";
        public static string Profile_DataFile = "DataFile";
        public static string Profile_SerialPort = "SerialPort";
        public static string Profile_IPAddress = "IPAddress";
        public static string Profile_IPPort = "IPPort";
        public static string Profile_Tracing = "Tracing";

        public static VantagePro Instance
        {
            get
            {
                if (lazy.IsValueCreated)
                    return lazy.Value;

                lazy.Value.Init();
                return lazy.Value;
            }
        }

        /// <summary>
        /// Forces the driver to immediatley query its attached hardware to refresh sensor
        /// values
        /// </summary>
        public void Refresh()
        {
            switch (OperationalMode) {
            case OpMode.File:
                Refresh_DataFile();
                break;

            case OpMode.Serial:
                Refresh_Serial();
                break;

            case OpMode.IP:
                Refresh_Socket();
                break;
            }
        }

        public OpMode OperationalMode { get; set; }
        public bool Tracing { get; set; }

        public void Refresh_DataFile()
        {
            if (string.IsNullOrEmpty(DataFile))
            {
                if (_connected)
                    throw new InvalidValueException("Null or empty dataFile name");
                else
                    return;
            }

            if (_lastDataRead == DateTime.MinValue || File.GetLastWriteTime(DataFile).CompareTo(_lastDataRead) > 0)
            {
                if (sensorData == null)
                    sensorData = new Dictionary<string, string>();

                for (int tries = 5; tries != 0; tries--)
                {
                    try
                    {
                        using (StreamReader sr = new StreamReader(DataFile))
                        {
                            string[] words;
                            string line;

                            if (sr == null)
                                throw new InvalidValueException($"Refresh: cannot open \"{DataFile}\" for read.");

                            while ((line = sr.ReadLine()) != null)
                            {
                                words = line.Split('=');
                                if (words.Length != 3)
                                    continue;
                                sensorData[words[0]] = words[1];
                            }

                            _lastDataRead = DateTime.Now;
                        }
                    } catch
                    {
                        Thread.Sleep(500);  // WeatherLink is writing the file
                    }
                }
            }
        }

        private void Open_Serial()
        {
            if (serialPort == null)
                serialPort = new System.IO.Ports.SerialPort();
            else if (serialPort.IsOpen)
                return;

            serialPort.PortName = SerialPortName;
            serialPort.BaudRate = serialPortSpeed;
            serialPort.ReadTimeout = 1000;
            serialPort.ReadBufferSize = 100;
            try
            {
                serialPort.Open();
            }
            catch
            {
                throw;
            }
        }

        private bool Open_Socket()
        {
            IPsocket = null;

            try
            {
                System.Net.IPAddress.TryParse(IPAddress, out IPAddress addr);
                IPEndPoint ipe = new IPEndPoint(addr, IPPort);
                Socket tempSocket =
                    new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                tempSocket.Connect(ipe);

                if (tempSocket.Connected)
                {
                    IPsocket = tempSocket;
                    tl.LogMessage("TryOpenSocket", $"Connected to {IPAddress}:{IPPort}");
                    return true;
                }
            }
            catch (Exception ex) {
                tl.LogMessage("TryOpenSocket", $"Caught: {ex.Message} at {ex.StackTrace}");
                throw;
            }

            return false;
        }

        /// <summary>
        /// Disconnects and closes the IPsocket
        /// </summary>
        /// <returns>is IPsocket still connected</returns>
        private bool TryCloseStation_Socket()
        {
            try
            {
                IPsocket.Disconnect(true);
                tl.LogMessage("TryCloseSocket", $"Disconnected from {IPAddress}:{IPPort}");
                IPsocket.Close();
                return true;
            }
            catch { }
            return false;
        }

        public string SerialPortName { get; set;}
        public string IPAddress { get; set; }
        public short IPPort { get; set; }

        private bool Wakeup_Serial()
        {
            string op = $"WakeUp_Serial [{SerialPortName}:{serialPortSpeed}]";
            Open_Serial();
            int[] rxBytes = new int[2];

            int attempt, maxAttempts = 3;
            for (attempt = 0; attempt < maxAttempts; attempt++)
            {
                serialPort.Write("\r");
                if ((rxBytes[0] = serialPort.ReadByte()) == '\n' && (rxBytes[1] = serialPort.ReadByte()) == '\r')
                {
                    #region trace
                    tl.LogMessage(op, $"attempt: {attempt+1}, Succeeded ([{rxBytes[0]:X2}], [{rxBytes[1]:X2}])");
                    #endregion
                    return true;
                }
                Thread.Sleep(1000);
            }

            #region trace
            tl.LogMessage(op, $"Failed after {attempt+1} attempts");
            #endregion
            return false;
        }

        private bool Wakeup_Socket()
        {
            string op = $"WakeUp_Socket [{IPAddress}:{IPPort}]";
            Open_Socket();

            Byte[] bytesReceived = new byte[2];
            int nBytes, attempt, maxAttempts = 3;

            for (attempt = 0; attempt < maxAttempts; attempt++)
            {
                IPsocket.Send(Encoding.ASCII.GetBytes("\r"), 1, 0);
                nBytes = IPsocket.Receive(bytesReceived, bytesReceived.Length, 0);
                if (nBytes == 2 && Encoding.ASCII.GetString(bytesReceived, 0, nBytes) == "\n\r")
                {
                    #region trace
                    tl.LogMessage(op, $"attempt: {attempt}, Success");
                    #endregion
                    return true;
                }
                Thread.Sleep(1000);
            }

            #region trace
            tl.LogMessage(op, $"Failed after {attempt+1} attempts");
            #endregion
            return false;
        }

        /// <summary>
        /// Gets a VantagePro two-bytes entity from the LPS command reply block
        ///  They are transmitted LSB first - (buf[offset] = LSB, buf[offset+1] = MSB)
        /// </summary>
        /// <param name="bytes">The stream of bytes in the reply block</param>
        /// <param name="o">The starting offset</param>
        /// <returns></returns>
        public static ushort GetUshort(byte[] bytes, int o)
        {
            return (ushort) ((bytes[o + 1] << 8) | bytes[o]);
        }

        public void Refresh_Serial()
        {
            string op = $"Refresh_Serial [{SerialPortName}:{serialPortSpeed}]";
            string txString = "LOOP 1\n";

            if (!Wakeup_Serial())
                return;

            byte[] buf = new byte[99];
            serialPort.Write(txString);
            #region trace
            tl.LogMessage(op, $"Wrote: {txString} to {SerialPortName}");
            #endregion

            int rxByte;
            if ((rxByte = serialPort.ReadByte()) != ACK)
            {
                #region trace
                tl.LogMessage(op, $"Got 0x{rxByte:X1} instead of ACK (existing: {serialPort.ReadExisting()})");
                #endregion
                return;
            }
            #region trace
            tl.LogMessage(op, $"Got ACK ([{rxByte:X2}])");
            #endregion

            Thread.Sleep(500);
            int nbytes;
            if ((nbytes = serialPort.Read(buf, 0, buf.Length)) != buf.Length)
            {
                #region trace
                tl.LogMessage(op, $"Got {nbytes} bytes instead of {buf.Length}");
                #endregion
                return;
            }

            #region trace
            tl.LogMessage(op, $"Successfully read {buf.Length} bytes");
            #endregion
            GetSensorData(buf);
            _lastDataRead = DateTime.Now;
        }

        private void Refresh_Socket()
        {
            if (!Wakeup_Socket())
                return;

            string op = $"Refresh_Socket [{IPAddress}:{IPPort}]";
            string LOOP = "LOOP 1\n";
            Byte[] txBytes = Encoding.ASCII.GetBytes(LOOP);
            Byte[] rxBytes = new byte[99];

            IPsocket.Send(txBytes, txBytes.Length, 0);
            IPsocket.Receive(rxBytes, 1, 0);
            if (rxBytes[0] != ACK)
            {
                #region trace
                tl.LogMessage(op, $"Got 0x{rxBytes[0]:X2} instead of 0x{ACK:X2}");
                #endregion
                return;
            }
            #region trace
            tl.LogMessage(op, $"Got ACK (0x{rxBytes[0]:X2})");
            #endregion

            if (IPsocket.Receive(rxBytes, rxBytes.Length, 0) != rxBytes.Length)
            {
                #region trace
                tl.LogMessage(op, $"Failed to receive {rxBytes.Length} bytes from {IPAddress}:{IPPort}");
                #endregion
                return;
            }

            #region trace
            tl.LogMessage(op, $"Received {rxBytes.Length} bytes from {IPAddress}:{IPPort}");
            #endregion
            GetSensorData(rxBytes);
            _lastDataRead = DateTime.Now;
        }

        private string ByteArrayToString(byte[] arr)
        {
            StringBuilder hex = new StringBuilder(arr.Length + 2);

            foreach (byte b in arr)
            {
                if (Char.IsLetterOrDigit((char)b) || Char.IsWhiteSpace((char)b) || Char.IsPunctuation((char)b))
                    hex.AppendFormat($" {(char) b}");
                else
                    hex.AppendFormat($"{b:X2}");
                hex.Append(" ");
            }
            return hex.ToString();
        }

        private void GetSensorData(byte[] buf) {
            //
            // Check the reply is valid - TBD verify the checksum
            // buf[4] == 1 for LOOP2 packets
            //
            string op = "GetSensorData";

            #region trace
            tl.LogMessage(op, ByteArrayToString(buf));
            #endregion

            if (buf[0] != 'L' || buf[1] != 'O' || buf[2] != 'O' || buf[4] != 0 || buf[95] != '\n' || buf[96] != '\r')
            {
                #region trace
                tl.LogMessage(op, $"Bad header [0]: {buf[0]}, [1]: {buf[1]}, [2]: {buf[2]}, [4]: {buf[4]} and/or trailer [95]: {buf[95]}, [96]: {buf[96]}");
                #endregion
                return;
            }
            #region trace
            tl.LogMessage(op, "Header and trailer are valid");
            #endregion

            sensorData = new Dictionary<string, string>();

            double F = GetUshort(buf, 12) / 10.0;
            sensorData["outsideTemp"] = util.ConvertUnits(F, Units.degreesFahrenheit, Units.degreesCelsius).ToString();
            sensorData["windSpeed"] = util.ConvertUnits(buf[14], Units.milesPerHour, Units.metresPerSecond).ToString();
            sensorData["windDir"] = GetUshort(buf, 16).ToString();

            double RH = buf[33];
            sensorData["outsideHumidity"] = RH.ToString();

            double P = GetUshort(buf, 7) / 1000;
            sensorData["barometer"] = util.ConvertUnits(P, Units.inHg, Units.hPa).ToString();

            double K = util.ConvertUnits(F, Units.degreesFahrenheit, Units.degreesKelvin);
            double Td = K - ((100 - RH) / 5);
            sensorData["outsideDewPt"] = util.ConvertUnits(Td, Units.degreesKelvin, Units.degreesCelsius).ToString();

            sensorData["rainRate"] = GetUshort(buf, 41).ToString();

            #region trace
            tl.LogMessage(op, $"Successfully parsed sensor data (packet CRC: {GetUshort(buf, 97):X2})");
            #endregion
        }

        public void Init()
        {
            if (_initialized)
                return;

            _name = "VantagePro";
            sensorData = new Dictionary<string, string>();
            ReadProfile();
            tl.Enabled = Tracing;
            Refresh();

            _initialized = true;
        }

        public bool Connected
        {
            get
            {
                return _connected;
            }

            set
            {
                if (value == _connected)
                    return;

                if (OperationalMode == OpMode.None)
                    _connected = false;

                switch (OperationalMode) {
                    case OpMode.Serial:
                        if (value)
                            Open_Serial();
                        else
                            serialPort.Close();
                        _connected = serialPort.IsOpen;
                        #region trace
                        tl.LogMessage("Connected", $"serial port: {SerialPortName}, connected: {_connected}");
                        #endregion
                        break;

                    case OpMode.File:
                        _connected = value && !string.IsNullOrEmpty(DataFile) && File.Exists(DataFile);
                        #region trace
                        tl.LogMessage("Connected", $"Datafile: {DataFile}, connected: {_connected}");
                        #endregion
                        break;

                    case OpMode.IP:
                        _connected = value ? Open_Socket() : TryCloseStation_Socket();
                        #region trace
                        tl.LogMessage("Connected", $"Socket: {IPAddress}:{IPPort}, connected: {_connected}");
                        #endregion
                        break;
                }
            }
        }

        public string Description
        {
            get
            {
                Init();
                return driverDescription;
            }
        }

        private readonly static ArrayList supportedActions = new ArrayList() {
            "raw-data",
            "OCHTag",
        };

        public ArrayList SupportedActions
        {
            get
            {
                return supportedActions;
            }
        }

        public string Action(string action, string _)
        {
            switch (action)
            {
                case "OCHTag":
                    return "VantagePro";

                case "raw-data":
                    return RawData;

                default:
                    throw new ASCOM.ActionNotImplementedException($"Action \"{action}\" is not implemented by this driver");
            }
        }

        public string RawData
        {
            get
            {
                VantagePro2StationRawData raw = new VantagePro2StationRawData()
                {
                    Name = _name,
                    Vendor = Vendor.ToString(),
                    Model = Model.ToString(),
                    SensorData = sensorData,
                };

                return JsonConvert.SerializeObject(raw);
            }
        }

        public static string Name
        {
            get
            {
                return Instance._name;
            }
        }
        
        public static string DriverInfo
        {
            get
            {
                return $"{Name} driver {DriverVersion}";
            }
        }

        public static string DriverVersion
        {
            get
            {
                return $"v{version}";
            }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal void ReadProfile()
        {
            using (Profile driverProfile = new Profile() { DeviceType = "ObservingConditions" })
            {
                Enum.TryParse<OpMode>(driverProfile.GetValue(DriverId, Profile_OpMode, string.Empty, OpMode.File.ToString()), out OpMode mode);
                OperationalMode = mode;
                DataFile = driverProfile.GetValue(DriverId, Profile_DataFile, string.Empty, "");
                SerialPortName = driverProfile.GetValue(DriverId, Profile_SerialPort, string.Empty, "");
                IPAddress = driverProfile.GetValue(DriverId, Profile_IPAddress, string.Empty, "");
                IPPort = Convert.ToInt16(driverProfile.GetValue(DriverId, Profile_IPPort, string.Empty, "22222"));
                Tracing = Convert.ToBoolean(driverProfile.GetValue(DriverId, Profile_Tracing, string.Empty, "True"));
            }
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal void WriteProfile()
        {
            using (Profile driverProfile = new Profile() { DeviceType = "ObservingConditions" })
            {
                driverProfile.WriteValue(DriverId, Profile_OpMode, OperationalMode.ToString());
                driverProfile.WriteValue(DriverId, Profile_DataFile, DataFile);
                driverProfile.WriteValue(DriverId, Profile_SerialPort, SerialPortName);
                driverProfile.WriteValue(DriverId, Profile_IPAddress, IPAddress);
                driverProfile.WriteValue(DriverId, Profile_IPPort, Convert.ToString(IPPort));
                driverProfile.WriteValue(DriverId, Profile_Tracing, Convert.ToString(Tracing));
            }
        }

        public string DataFile { get; set; }

        #region IObservingConditions Implementation

        /// <summary>
        /// Gets and sets the time period over which observations wil be averaged
        /// </summary>
        /// <remarks>
        /// Get must be implemented, if it can't be changed it must return 0
        /// Time period (hours) over which the property values will be averaged 0.0 =
        /// current value, 0.5= average for the last 30 minutes, 1.0 = average for the
        /// last hour
        /// </remarks>
        public double AveragePeriod
        {
            get
            {
                return 0;
            }

            set
            {
                if (value != 0)
                    throw new InvalidValueException("Only 0.0 accepted");
            }
        }

        /// <summary>
        /// Amount of sky obscured by cloud
        /// </summary>
        /// <remarks>0%= clear sky, 100% = 100% cloud coverage</remarks>
        public double CloudCover
        {
            get
            {
                throw new PropertyNotImplementedException("CloudCover", false);
            }
        }

        /// <summary>
        /// Atmospheric dew point at the observatory in deg C
        /// </summary>
        /// <remarks>
        /// Normally optional but mandatory if <see cref=" ASCOM.DeviceInterface.IObservingConditions.Humidity"/>
        /// Is provided
        /// </remarks>
        public double DewPoint
        {
            get
            {
                Refresh();
                var dewPoint = Convert.ToDouble(sensorData["outsideDewPt"]);

                return dewPoint;
            }
        }

        /// <summary>
        /// Atmospheric relative humidity at the observatory in percent
        /// </summary>
        /// <remarks>
        /// Normally optional but mandatory if <see cref="ASCOM.DeviceInterface.IObservingConditions.DewPoint"/> 
        /// Is provided
        /// </remarks>
        public double Humidity
        {
            get
            {
                Refresh();
                var humidity = Convert.ToDouble(sensorData["outsideHumidity"]);

                return humidity;
            }
        }

        /// <summary>
        /// Atmospheric pressure at the observatory in hectoPascals (mB)
        /// </summary>
        /// <remarks>
        /// This must be the pressure at the observatory and not the "reduced" pressure
        /// at sea level. Please check whether your pressure sensor delivers local pressure
        /// or sea level pressure and adjust if required to observatory pressure.
        /// </remarks>
        public double Pressure
        {
            get
            {
                Refresh();
                var pressure = Convert.ToDouble(sensorData["barometer"]);

                return pressure;
            }
        }

        /// <summary>
        /// Rain rate at the observatory
        /// </summary>
        /// <remarks>
        /// This property can be interpreted as 0.0 = Dry any positive nonzero value
        /// = wet.
        /// </remarks>
        public double RainRate
        {
            get
            {
                Refresh();
                var rainRate = Convert.ToDouble(sensorData["rainRate"]);

                return rainRate;
            }
        }
        

        /// <summary>
        /// Provides a description of the sensor providing the requested property
        /// </summary>
        /// <param name="PropertyName">Name of the property whose sensor description is required</param>
        /// <returns>The sensor description string</returns>
        /// <remarks>
        /// PropertyName must be one of the sensor properties, 
        /// properties that are not implemented must throw the MethodNotImplementedException
        /// </remarks>
        public static string SensorDescription(string PropertyName)
        {
            switch (PropertyName)
            {
                case "AveragePeriod":
                    return "Average period in hours, immediate values are only available";

                case "DewPoint":
                case "Humidity":
                case "Pressure":
                case "Temperature":
                case "WindDirection":
                case "WindSpeed":
                case "RainRate":
                    return "SensorDescription - " + PropertyName;

                case "SkyBrightness":
                case "SkyQuality":
                case "StarFWHM":
                case "SkyTemperature":
                case "CloudCover":
                case "WindGust":
                    throw new MethodNotImplementedException("SensorDescription(" + PropertyName + ")");
                default:
                    throw new ASCOM.InvalidValueException("SensorDescription(" + PropertyName + ")");
            }
        }

        /// <summary>
        /// Sky brightness at the observatory
        /// </summary>
        public double SkyBrightness
        {
            get
            {
                throw new PropertyNotImplementedException("SkyBrightness", false);
            }
        }

        /// <summary>
        /// Sky quality at the observatory
        /// </summary>
        public double SkyQuality
        {
            get
            {
                throw new PropertyNotImplementedException("SkyQuality", false);
            }
        }

        /// <summary>
        /// Seeing at the observatory
        /// </summary>
        public double StarFWHM
        {
            get
            {
                throw new PropertyNotImplementedException("StarFWHM", false);
            }
        }

        /// <summary>
        /// Sky temperature at the observatory in deg C
        /// </summary>
        public double SkyTemperature
        {
            get
            {
                throw new PropertyNotImplementedException("SkyTemperature", false);
            }
        }

        /// <summary>
        /// Temperature at the observatory in deg C
        /// </summary>
        public double Temperature
        {
            get
            {
                Refresh();
                var temperature = Convert.ToDouble(sensorData["outsideTemp"]);

                return temperature;
            }
        }

        /// <summary>
        /// Provides the time since the sensor value was last updated
        /// </summary>
        /// <param name="PropertyName">Name of the property whose time since last update Is required</param>
        /// <returns>Time in seconds since the last sensor update for this property</returns>
        /// <remarks>
        /// PropertyName should be one of the sensor properties Or empty string to get
        /// the last update of any parameter. A negative value indicates no valid value
        /// ever received.
        /// </remarks>
        public double TimeSinceLastUpdate(string PropertyName)
        {
            switch (PropertyName)
            {
                case "SkyBrightness":
                case "SkyQuality":
                case "StarFWHM":
                case "SkyTemperature":
                case "CloudCover":
                case "WindGust":
                    throw new MethodNotImplementedException("SensorDescription(" + PropertyName + ")");
            }
            Refresh();

            double seconds;
            if (OperationalMode == OpMode.File)
            {
                string dateTime = sensorData["utcDate"] + " " + sensorData["utcTime"] + "m";
                DateTime lastUpdate = Convert.ToDateTime(dateTime);
                seconds = (DateTime.UtcNow - lastUpdate).TotalSeconds;
            }
            else
            {
                seconds = (DateTime.UtcNow - _lastDataRead.ToUniversalTime()).TotalSeconds;
            }

            return seconds;
        }

        /// <summary>
        /// Wind direction at the observatory in degrees
        /// </summary>
        /// <remarks>
        /// 0..360.0, 360=N, 180=S, 90=E, 270=W. When there Is no wind the driver will
        /// return a value of 0 for wind direction
        /// </remarks>
        public double WindDirection
        {
            get
            {
                Refresh();
                return Convert.ToDouble(sensorData["windDir"]);
            }
        }

        /// <summary>
        /// Peak 3 second wind gust at the observatory over the last 2 minutes in m/s
        /// </summary>
        public double WindGust
        {
            get
            {
                throw new PropertyNotImplementedException("WindGust", false);
            }
        }

        public double MPS(double kmh)
        {
            return kmh * (1000.0 / 3600.0);
        }

        public double KMH(double mps)
        {
            return mps * 3.6;
        }

        /// <summary>
        /// Wind speed at the observatory in m/s
        /// </summary>
        public double WindSpeedMps
        {
            get
            {
                Refresh();
                double kmh = Convert.ToSingle(sensorData["windSpeed"]);
                double windSpeed = MPS(kmh);

                return windSpeed;
            }
        }

        #endregion

        public override bool Enabled
        {
            get { return true; }
            set { }
        }

        public override WeatherStationInputMethod InputMethod
        {
            get
            {
                return WeatherStationInputMethod.WeatherLink_HtmlReport;
            }

            set { }
        }

        public override WeatherStationVendor Vendor
        {
            get
            {
                return WeatherStationVendor.DavisInstruments;
            }
        }

        public override WeatherStationModel Model
        {
            get
            {
                return WeatherStationModel.VantagePro2;
            }
        }

        public class VantagePro2StationRawData
        {
            public string Name;
            public string Vendor;
            public string Model;
            public Dictionary<string, string> SensorData;
        }
    }
}
