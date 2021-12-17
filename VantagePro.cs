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
using System.Drawing;


namespace ASCOM.VantagePro
{
    public class VantagePro: WeatherStation
    {
        public enum OpMode { None, File, Serial, IP };
        public Color colorGood = Color.Green;
        public Color colorWarning = Color.Yellow;
        public Color colorError = Color.IndianRed;

        public const int serialPortSpeed = 19200;
        System.IO.Ports.SerialPort serialPort = new System.IO.Ports.SerialPort();

        public Socket IPsocket;

        private bool _connected = false;
        private bool _initialized = false;

        public static readonly string traceLogFile = $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\VantagePro-{DriverVersion}.log";
        public static TraceLogger tl;

        private static readonly Util util = new Util();
        private const byte ACK = 0x6;

        private readonly TimeSpan refreshInterval = TimeSpan.FromSeconds(30);

        public VantagePro() { }

        static VantagePro() { }

        private Dictionary<string, string> sensorData = null;
        private DateTime lastDataRead = DateTime.MinValue;

        private static readonly Lazy<VantagePro> lazy = new Lazy<VantagePro>(() => new VantagePro()); // Singleton

        public static string DriverDescription
        {
            get
            {
                return $"VantagePro ASCOM Driver {DriverVersion}";
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
            if (DateTime.Now < lastDataRead + refreshInterval)
                return;

            switch (OperationalMode)
            {
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

        public static readonly Dictionary<byte, string> ValueToStationModel = new Dictionary<byte, string>
        {
            {  0, "Wizard III" },
            {  1, "Wizard II" },
            {  2, "Monitor" },
            {  3, "Perception" },
            {  4, "GroWeather" },
            {  5, "Energy Enviromonitor" },
            {  6, "Health Enviromonitor" },
            { 16, "Vantage Pro or Vantage Pro 2" },
            { 17, "Vantage Vue" },
        };

        private string GetStationType()
        {
            char[] txBytes = { 'W', 'R', 'D', (char) 0x12, (char) 0x4d, '\n' };
            byte[] rxBytes = new byte[2];
            int nRxBytes = 0;
            string op = "GetStationType";

            switch (OperationalMode)
            {
                case OpMode.Serial:
                    Wakeup_Serial();
                    serialPort.Write(txBytes, 0, txBytes.Length);
                    Thread.Sleep(500);
                    rxBytes = Encoding.ASCII.GetBytes(serialPort.ReadExisting());
                    nRxBytes = rxBytes.Length;
                    break;

                case OpMode.IP:
                    Wakeup_Socket();
                    IPsocket.Send(Encoding.ASCII.GetBytes(txBytes), txBytes.Length, 0);
                    nRxBytes = IPsocket.Receive(rxBytes, rxBytes.Length, 0);
                    break;

                case OpMode.File:
                    return "Unknown";
            }

            if (nRxBytes < 2)
            {
                #region trace
                tl.LogMessage(op, $"Got only {nRxBytes} bytes (instead of 2)");
                #endregion
                return null;
            }

            if (rxBytes[0] != ACK)
            {
                #region trace
                tl.LogMessage(op, $"First byte is 0x{rxBytes[0]:X} instead of ACK");
                #endregion
                return null;
            }

            try
            {
                return ValueToStationModel[rxBytes[1]];
            }
            catch
            {
                return $"Unknown (byte[1]: {rxBytes[1]})";
            }
        }

        public static OpMode OperationalMode { get; set; }
        public bool Tracing { get; set; }

        private readonly List<string> usedKeys = new List<string> {
                    "outsideHumidity", "outsideDewPt", "outsideTemp", "barometer", "rainRate", "windDir", "windSpeed", "utcDate", "utcTime"
                };

        public void Refresh_DataFile()
        {
            if (string.IsNullOrEmpty(DataFile))
            {
                if (_connected)
                    throw new InvalidValueException("Null or empty dataFile name");
                else
                    return;
            }

            if (lastDataRead == DateTime.MinValue || File.GetLastWriteTime(DataFile).CompareTo(lastDataRead) > 0)
            {
                sensorData = new Dictionary<string, string>();

                for (int tries = 5; tries != 0; tries--)
                {
                    try
                    {
                        using (StreamReader sr = new StreamReader(DataFile))
                        {
                            string[] words;
                            string line, key, value;

                            if (sr == null)
                                throw new InvalidValueException($"Refresh: cannot open \"{DataFile}\" for read.");

                            while ((line = sr.ReadLine()) != null)
                            {
                                words = line.Split('=');
                                if (words.Length != 3)
                                    continue;

                                key = words[0].Trim();
                                value = words[1].Trim();
                                if (usedKeys.Contains(key))
                                {
                                    sensorData[key] = value;
                                    #region trace
                                    tl.LogMessage("Refresh_DataFile", $"Datafile: sensorData[{key}] = \"{sensorData[key]}\"");
                                    #endregion
                                }
                            }

                            lastDataRead = DateTime.Now;
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
            string op = "Open_Socket";
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
                    tl.LogMessage(op, $"Connected to {IPAddress}:{IPPort}");
                    return true;
                }
            }
            catch (Exception ex) {
                tl.LogMessage(op, $"Caught: {ex.Message} at {ex.StackTrace}");
                throw;
            }

            return false;
        }

        /// <summary>
        /// Disconnects and closes the IPsocket
        /// </summary>
        /// <returns>is IPsocket still connected</returns>
        private bool Close_Socket()
        {
            try
            {
                IPsocket.Disconnect(true);
                tl.LogMessage("Close_Socket", $"Disconnected from {IPAddress}:{IPPort}");
                IPsocket.Close();
                return true;
            }
            catch { }
            return false;
        }

        public static string SerialPortName { get; set;}
        public static int SerialPortSpeed { get; } = 19200;
        public static string IPAddress { get; set; }
        public static short IPPort { get; set; } = 22222;

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

            Byte[] rxBytes = new byte[2];
            int nRxBytes, attempt, maxAttempts = 3;

            for (attempt = 0; attempt < maxAttempts; attempt++)
            {
                IPsocket.Send(Encoding.ASCII.GetBytes("\r"), 1, 0);
                nRxBytes = IPsocket.Receive(rxBytes, rxBytes.Length, 0);
                if (nRxBytes == 2 && Encoding.ASCII.GetString(rxBytes, 0, nRxBytes) == "\n\r")
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

            byte[] rxBytes = new byte[99];
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
            int nRxBytes;
            if ((nRxBytes = serialPort.Read(rxBytes, 0, rxBytes.Length)) != rxBytes.Length)
            {
                #region trace
                tl.LogMessage(op, $"Got {nRxBytes} bytes instead of {rxBytes.Length}");
                #endregion
                return;
            }

            #region trace
            tl.LogMessage(op, $"Successfully read {rxBytes.Length} bytes");
            #endregion

            if (! CalculateCRC(rxBytes))
            {
                #region trace
                tl.LogMessage(op, "Bad CRC, packet discarded");
                #endregion
                return;
            }

            ParseSensorData(rxBytes);
            lastDataRead = DateTime.Now;
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

            int nRxBytes;
            if ((nRxBytes = IPsocket.Receive(rxBytes, rxBytes.Length, 0)) != rxBytes.Length)
            {
                #region trace
                tl.LogMessage(op, $"Failed to receive {rxBytes.Length} bytes from {IPAddress}:{IPPort}, received only {nRxBytes} bytes");
                #endregion
                return;
            }

            #region trace
            tl.LogMessage(op, $"Received {rxBytes.Length} bytes from {IPAddress}:{IPPort}");
            #endregion

            if (!CalculateCRC(rxBytes))
            {
                #region trace
                tl.LogMessage(op, "Bad CRC, packet discarded");
                #endregion
                return;
            }

            ParseSensorData(rxBytes);
            lastDataRead = DateTime.Now;
        }

        private string ByteArrayToString(byte[] arr)
        {
            StringBuilder hex = new StringBuilder(arr.Length * 3);

            foreach (byte b in arr)
                hex.AppendFormat($"{b:X2} ");

            return hex.ToString();
        }

        private void ParseSensorData(byte[] buf) {
            string op = "ParseSensorData";

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

            double P = GetUshort(buf, 7);
            sensorData["barometer"] = (util.ConvertUnits(P, Units.inHg, Units.hPa) / 1000).ToString();

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

            if (Tracing)
                Directory.CreateDirectory(Path.GetDirectoryName(traceLogFile));
            tl = new TraceLogger(traceLogFile, "VantagePro")
            {
                Enabled = Tracing
            };

            Refresh();

            _initialized = true;
        }

        private static string StationType { get; set; }

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
                        if (_connected)
                            tl.LogMessage("Connected", $"serial port: {SerialPortName}");
                        #endregion
                        break;

                    case OpMode.File:
                        _connected = value && !string.IsNullOrEmpty(DataFile) && File.Exists(DataFile);
                        #region trace
                        if (_connected)
                            tl.LogMessage("Connected", $"Datafile: {DataFile}");
                        #endregion
                        break;

                    case OpMode.IP:
                        _connected = value ? Open_Socket() : Close_Socket();
                        #region trace
                        if (_connected)
                            tl.LogMessage("Connected", $"Socket: {IPAddress}:{IPPort}");
                        #endregion
                        break;
                }
            }
        }

        public string Description
        {
            get
            {
                return DriverDescription;
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
                    Model = Model,
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
                string info = $"station model: {Instance.GetStationType()}, ";


                switch (VantagePro.OperationalMode)
                {
                    case OpMode.File:
                        info += $"file interface ({VantagePro.DataFile})";
                        break;
                    case OpMode.Serial:
                        info += $"serial interface ({VantagePro.SerialPortName} at {VantagePro.serialPortSpeed} baud)";
                        break;
                    case OpMode.IP:
                        info += $"socket interface ({VantagePro.IPAddress}:{VantagePro.IPPort})";
                        break;
                }

                var v = AssemblyVersion;
                DateTime buildTime = new DateTime(2000, 1, 1).AddDays(v.Build).AddSeconds(v.Revision * 2);
                info += $", built on: {buildTime:dddd, dd MMMM yyyy HH:mm:ss}";
                return info;
            }
        }

        private static Version AssemblyVersion
        {
            get
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            }
        }

        public static string DriverVersion
        {
            get
            {
                return $"v{AssemblyVersion}";
            }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal void ReadProfile()
        {
            using (Profile driverProfile = new Profile() { DeviceType = "ObservingConditions" })
            {
                Enum.TryParse<OpMode>(driverProfile.GetValue(DriverId, Profile_OpMode, string.Empty, OpMode.None.ToString()), out OpMode mode);
                OperationalMode = mode;

                DataFile = driverProfile.GetValue(DriverId, Profile_DataFile, string.Empty, "");
                SerialPortName = driverProfile.GetValue(DriverId, Profile_SerialPort, string.Empty, "");
                IPAddress = driverProfile.GetValue(DriverId, Profile_IPAddress, string.Empty, "");
                IPPort = Convert.ToInt16(driverProfile.GetValue(DriverId, Profile_IPPort, string.Empty, "22222"));
                Tracing = Convert.ToBoolean(driverProfile.GetValue(DriverId, Profile_Tracing, string.Empty, "False")); }
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
                driverProfile.WriteValue(DriverId, Profile_IPPort, IPPort.ToString());
                driverProfile.WriteValue(DriverId, Profile_Tracing, Tracing.ToString());
            }
        }

        public static string DataFile { get; set; }

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
                #region trace
                string traceId = "DewPoint";
                if (string.IsNullOrEmpty(sensorData["outsideDewPt"]))
                {
                    tl.LogMessage(traceId, "NullOrEmpty: sensorData[\"outsideDewPt\"]");
                    return double.NaN;
                }

                #endregion

                return TryParseDouble_LocalThenEnUS(sensorData["outsideDewPt"]);
            }
        }

        private static readonly CultureInfo en_US = CultureInfo.CreateSpecificCulture("en-US");

        public double TryParseDouble_LocalThenEnUS(string str)
        {
            if (Double.TryParse(str, out double value))
                return value;

            if (Double.TryParse(str, NumberStyles.Float, en_US, out value))
                return value;

            return Double.NaN;
        }

        public static DateTime TryParseDateTime_LocalThenEnUS(string str)
        {
            if (DateTime.TryParse(str, out DateTime d))
                return d;

            if (DateTime.TryParse(str, en_US, DateTimeStyles.None, out d))
                return d;

            return DateTime.MinValue;
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
                #region trace
                string traceId = "Humidity";
                if (string.IsNullOrEmpty(sensorData["outsideHumidity"]))
                {
                    tl.LogMessage(traceId, "NullOrEmpty: sensorData[\"outsideHumidity\"]");
                    return double.NaN;
                }
                #endregion

                return TryParseDouble_LocalThenEnUS(sensorData["outsideHumidity"]);
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
                string traceId = "Pressure";

                Refresh();
                #region trace
                if (string.IsNullOrEmpty(sensorData["barometer"]))
                {
                    tl.LogMessage(traceId, "NullOrEmpty: sensorData[\"Pressure\"]");
                    return double.NaN;
                }
                #endregion

                return TryParseDouble_LocalThenEnUS(sensorData["barometer"]);
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
                string traceId = "RainRate";

                Refresh();
                #region trace
                if (string.IsNullOrEmpty(sensorData["rainRate"]))
                {
                    tl.LogMessage(traceId, "NullOrEmpty: sensorData[\"rainRate\"]");
                    return double.NaN;
                }
                #endregion

                return TryParseDouble_LocalThenEnUS(sensorData["rainRate"]);
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
                string traceId = "Temperature";

                Refresh();
                #region trace
                if (string.IsNullOrEmpty(sensorData["outsideTemp"]))
                {
                    tl.LogMessage(traceId, "NullOrEmpty: sensorData[\"outsideTemp\"]");
                    return double.NaN;
                }
                #endregion

                return Double.TryParse(sensorData["outsideTemp"], out double temperature) ? temperature : Double.NaN;
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
                #region trace
                string traceId = $"TimeSinceLastUpdate({PropertyName})";

                if (string.IsNullOrEmpty(sensorData["utcDate"]))
                {
                    tl.LogMessage(traceId, "NullOrEmpty: sensorData[\"utcDate\"]");
                    return TimeSpan.MaxValue.TotalSeconds;
                }

                if (string.IsNullOrEmpty(sensorData["utcTime"]))
                {
                    tl.LogMessage(traceId, "NullOrEmpty: sensorData[\"utcTime\"]");
                    return TimeSpan.MaxValue.TotalSeconds;
                }
                #endregion
                string dateTime = sensorData["utcDate"] + " " + sensorData["utcTime"] + "m";
                var lastUpdate = TryParseDateTime_LocalThenEnUS(dateTime);

                seconds = (DateTime.UtcNow - lastUpdate).TotalSeconds;
            }
            else
            {
                seconds = (DateTime.UtcNow - lastDataRead.ToUniversalTime()).TotalSeconds;
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
                #region trace
                string traceId = "WindDirection";
                if (string.IsNullOrEmpty(sensorData["windDir"]))
                {
                    tl.LogMessage(traceId, "NullOrEmpty: sensorData[\"windDir\"]");
                    return double.NaN;
                }
                #endregion

                if (WindSpeedMps == 0.0)
                    return 0.0;

                return TryParseDouble_LocalThenEnUS(sensorData["windDir"]);
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

        public static double MPS(double kmh)
        {
            return kmh * (1000.0 / 3600.0);
        }

        public static double KMH(double mps)
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
                string traceId = "WindSpeedMps";
                Refresh();
                #region trace
                if (string.IsNullOrEmpty(sensorData["windSpeed"]))
                {
                    tl.LogMessage(traceId, "NullOrEmpty: sensorData[\"windSpeed\"]");
                    return double.NaN;
                }
                #endregion

                return TryParseDouble_LocalThenEnUS(sensorData["windSpeed"]);
            }
        }

        #endregion

        public override bool Enabled
        {
            get { return true; }
            set { }
        }

        private static readonly Dictionary<OpMode, WeatherStationInputMethod> opMode2InputMethod = new Dictionary<OpMode, WeatherStationInputMethod>()
        {
            { OpMode.File, WeatherStationInputMethod.WeatherLink_HtmlReport },
            { OpMode.Serial, WeatherStationInputMethod.WeatherLink_Serial },
            { OpMode.IP, WeatherStationInputMethod.WeatherLink_IP },
        };

        public override WeatherStationInputMethod InputMethod
        {
            get
            {
                return opMode2InputMethod[VantagePro.OperationalMode];
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

        public override string Model
        {
            get
            {
                return StationType;
            }
        }

        private static bool CalculateCRC(byte[] buf)
        {
            UInt16 crc = 0;

            for (int i = 0; i < buf.Length; i++)
                crc = (UInt16)(crc_table[(crc >> 8) ^ buf[i]] ^ (crc << 8));

            #region trace
            tl.LogMessage("CalculateCRC", $"CRC: {(crc == 0 ? "OK" : "BAD")}");
            #endregion
            return crc == 0;
        }

        private static readonly UInt16[] crc_table = {
            0x0, 0x1021, 0x2042, 0x3063, 0x4084, 0x50a5, 0x60c6, 0x70e7,
            0x8108, 0x9129, 0xa14a, 0xb16b, 0xc18c, 0xd1ad, 0xe1ce, 0xf1ef,
            0x1231, 0x210, 0x3273, 0x2252, 0x52b5, 0x4294, 0x72f7, 0x62d6,
            0x9339, 0x8318, 0xb37b, 0xa35a, 0xd3bd, 0xc39c, 0xf3ff, 0xe3de,
            0x2462, 0x3443, 0x420, 0x1401, 0x64e6, 0x74c7, 0x44a4, 0x5485,
            0xa56a, 0xb54b, 0x8528, 0x9509, 0xe5ee, 0xf5cf, 0xc5ac, 0xd58d,
            0x3653, 0x2672, 0x1611, 0x630, 0x76d7, 0x66f6, 0x5695, 0x46b4,
            0xb75b, 0xa77a, 0x9719, 0x8738, 0xf7df, 0xe7fe, 0xd79d, 0xc7bc,
            0x48c4, 0x58e5, 0x6886, 0x78a7, 0x840, 0x1861, 0x2802, 0x3823,
            0xc9cc, 0xd9ed, 0xe98e, 0xf9af, 0x8948, 0x9969, 0xa90a, 0xb92b,
            0x5af5, 0x4ad4, 0x7ab7, 0x6a96, 0x1a71, 0xa50, 0x3a33, 0x2a12,
            0xdbfd, 0xcbdc, 0xfbbf, 0xeb9e, 0x9b79, 0x8b58, 0xbb3b, 0xab1a,
            0x6ca6, 0x7c87, 0x4ce4, 0x5cc5, 0x2c22, 0x3c03, 0xc60, 0x1c41,
            0xedae, 0xfd8f, 0xcdec, 0xddcd, 0xad2a, 0xbd0b, 0x8d68, 0x9d49,
            0x7e97, 0x6eb6, 0x5ed5, 0x4ef4, 0x3e13, 0x2e32, 0x1e51, 0xe70,
            0xff9f, 0xefbe, 0xdfdd, 0xcffc, 0xbf1b, 0xaf3a, 0x9f59, 0x8f78,
            0x9188, 0x81a9, 0xb1ca, 0xa1eb, 0xd10c, 0xc12d, 0xf14e, 0xe16f,
            0x1080, 0xa1, 0x30c2, 0x20e3, 0x5004, 0x4025, 0x7046, 0x6067,
            0x83b9, 0x9398, 0xa3fb, 0xb3da, 0xc33d, 0xd31c, 0xe37f, 0xf35e,
            0x2b1, 0x1290, 0x22f3, 0x32d2, 0x4235, 0x5214, 0x6277, 0x7256,
            0xb5ea, 0xa5cb, 0x95a8, 0x8589, 0xf56e, 0xe54f, 0xd52c, 0xc50d,
            0x34e2, 0x24c3, 0x14a0, 0x481, 0x7466, 0x6447, 0x5424, 0x4405,
            0xa7db, 0xb7fa, 0x8799, 0x97b8, 0xe75f, 0xf77e, 0xc71d, 0xd73c,
            0x26d3, 0x36f2, 0x691, 0x16b0, 0x6657, 0x7676, 0x4615, 0x5634,
            0xd94c, 0xc96d, 0xf90e, 0xe92f, 0x99c8, 0x89e9, 0xb98a, 0xa9ab,
            0x5844, 0x4865, 0x7806, 0x6827, 0x18c0, 0x8e1, 0x3882, 0x28a3,
            0xcb7d, 0xdb5c, 0xeb3f, 0xfb1e, 0x8bf9, 0x9bd8, 0xabbb, 0xbb9a,
            0x4a75, 0x5a54, 0x6a37, 0x7a16, 0xaf1, 0x1ad0, 0x2ab3, 0x3a92,
            0xfd2e, 0xed0f, 0xdd6c, 0xcd4d, 0xbdaa, 0xad8b, 0x9de8, 0x8dc9,
            0x7c26, 0x6c07, 0x5c64, 0x4c45, 0x3ca2, 0x2c83, 0x1ce0, 0xcc1,
            0xef1f, 0xff3e, 0xcf5d, 0xdf7c, 0xaf9b, 0xbfba, 0x8fd9, 0x9ff8,
            0x6e17, 0x7e36, 0x4e55, 0x5e74, 0x2e93, 0x3eb2, 0xed1, 0x1ef0,
        };

        public class VantagePro2StationRawData
        {
            public string Name;
            public string Vendor;
            public string Model;
            public Dictionary<string, string> SensorData;
        }

        public void TestFileSettings(string path, ref string result, ref Color color)
        {
            #region trace
            string traceId = "TestFileSettings";
            string settings = $"[{path}]";
            #endregion

            if (string.IsNullOrEmpty(path))
            {
                #region trace
                tl.LogMessage(traceId, $"{settings}: Empty report file name");
                #endregion
                color = colorError;
                result = "Empty report file name!";
                goto Out;
            }

            if (!File.Exists(path))
            {
                #region trace
                tl.LogMessage(traceId, $"{settings}: File does not exist");
                #endregion
                result = $"File \"{path}\" does not exist.";
                color = colorError;
                goto Out;
            }
            #region trace
            tl.LogMessage(traceId, $"{settings}: File exists");
            #endregion

            Dictionary<string, string> dict = new Dictionary<string, string>();

            for (int tries = 5; tries != 0; tries--)
            {
                try
                {
                    using (StreamReader sr = new StreamReader(path))
                    {
                        string[] words;
                        string line;

                        if (sr == null) {
                            continue;
                        }

                        while ((line = sr.ReadLine()) != null)
                        {
                            words = line.Split('=');
                            if (words.Length != 3)
                                continue;
                            dict[words[0]] = words[1];
                        }
                    }
                }
                catch
                {
                    Thread.Sleep(500);  // WeatherLink is writing the file
                }
            }

            if (dict.Keys.Count == 0)
            {
                #region trace
                tl.LogMessage(traceId, $"{settings}: Failed to parse file contents");
                #endregion
                result = $"Cannot get weather data from \"{path}\".";
                color = colorError;
                goto Out;
            }
            #region trace
            tl.LogMessage(traceId, $"{settings}: Parsed {dict.Keys.Count} keys");
            #endregion

            if (string.IsNullOrWhiteSpace(dict["insideHumidity"]) && string.IsNullOrWhiteSpace(dict["outsideHumidity"]))
            {
                #region trace
                tl.LogMessage(traceId, $"{settings}: File parsed, but no entries for insideHumidity or outsideHumidity");
                #endregion
                result = $"\"{path}\" does not contain a valid report";
                color = colorError;
                goto Out;
            }

            result = $"\"{path}\" contains a valid report";
            string stationName = dict["StationName"];
            if (!string.IsNullOrWhiteSpace(stationName))
                result += $" for station \"{stationName}\".";
            else
                stationName = "NullOrWhiteSpace";
            #region trace
            tl.LogMessage(traceId, $"{settings}: Success, the file contains a valid weather report (stationName: {stationName}, insideHumidity: {dict["insideHumidity"]}, outsideHumidity: {dict["outsideHumidity"]})");
            #endregion
            color = colorGood;
        Out:
            ;
        }

        public void TestSerialSettings(string portName, ref string result, ref Color color)
        {
            #region trace
            string traceId = "TestSerialSettings";
            string settings = $"[{portName}:{serialPortSpeed}]";
            #endregion

            if (string.IsNullOrWhiteSpace(portName))
            {
                #region trace
                tl.LogMessage(traceId, "Empty comm port name");
                #endregion
                result = "Empty serial port name";
                color = colorError;
                return;
            }

            System.IO.Ports.SerialPort serialPort = new System.IO.Ports.SerialPort
            {
                PortName = portName,
                BaudRate = serialPortSpeed,
                ReadTimeout = 1000,
                ReadBufferSize = 100
            };

            #region Open
            try
            {
                serialPort.Open();
                #region trace
                tl.LogMessage(traceId, $"{settings}: Open() succeeded");
                #endregion
            }
            catch (Exception ex)
            {
                #region trace
                tl.LogMessage(traceId, $"{settings}: Open() caught {ex.Message} at {ex.StackTrace}");
                #endregion
                result = $"Cannot open serial port \"{portName}:{serialPortSpeed}\" ";
                color = colorError;
                goto Out;
            }
            #endregion

            #region Wakeup
            int[] rxBytesWakeup = new int[2];
            int attempt, maxAttempts = 3;
            for (attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    serialPort.Write("\r");
                    if ((rxBytesWakeup[0] = serialPort.ReadByte()) == '\n' && (rxBytesWakeup[1] = serialPort.ReadByte()) == '\r')
                    {
                        #region trace
                        tl.LogMessage(traceId, $"{settings}: Wakeup sequence succeeded");
                        #endregion
                        break;
                    }
                } catch
                {
                    continue;
                }
                Thread.Sleep(1000);
            }

            if (attempt >= maxAttempts)
            {
                #region trace
                tl.LogMessage(traceId, $"{settings}: Wakeup sequence failed after {attempt + 1} attempts");
                #endregion
                result = $"Cannot wake up station on port \"{portName}\"";
                color = colorError;
                goto Out;
            }
            #endregion

            #region Identify
            char[] txBytes = { 'W', 'R', 'D', (char)0x12, (char)0x4d, '\n' };
            byte[] rxBytes = new byte[2];
            int nRxBytes;
            serialPort.Write(txBytes, 0, txBytes.Length);
            Thread.Sleep(500);
            if ((nRxBytes = serialPort.Read(rxBytes, 0, rxBytes.Length)) != rxBytes.Length)
            {
                #region trace
                tl.LogMessage(traceId, $"{settings}: Identify: got only {nRxBytes} bytes (instead of 2)");
                #endregion
                result = $"Cannot identify station";
                color = colorError;
                goto Out;
            }

            if (rxBytes[0] != ACK)
            {
                #region trace
                tl.LogMessage(traceId, $"{settings}: Identify: first byte is 0x{rxBytes[0]:X} instead of ACK");
                #endregion
                result = $"Cannot identify station";
                color = colorError;
                goto Out;
            }

            string stationType = "Unknown";
            try
            {
                stationType = ValueToStationModel[rxBytes[1]];
            }
            catch { }
            #endregion

            #region trace
            tl.LogMessage(traceId, $"{settings}: Found a \"{stationType}\" type station.");
            #endregion
            result = $"Found a \"{stationType}\" type station at {settings}.";
            color = colorGood;
        Out:
            serialPort.Close();
        }

        public void TestIPSettings(string address, ref string result, ref Color color)
        {
            #region trace
            string traceId = "TestIPSettings";
            string settings = $"[{address}:{IPPort}]";
            #endregion

            if (string.IsNullOrWhiteSpace(address))
            {
                #region trace
                tl.LogMessage(traceId, "Empty IP address");
                #endregion
                result = "Empty IP address";
                color = colorError;
                return;
            }

            Socket sock = null;
            #region Open
            try
            {
                const int timeoutMillis = 5000;

                System.Net.IPAddress.TryParse(address, out IPAddress addr);
                IPEndPoint ipe = new IPEndPoint(addr, IPPort);
                sock = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                IAsyncResult asyncResult = sock.BeginConnect(ipe, null, null);
                bool success = asyncResult.AsyncWaitHandle.WaitOne(timeoutMillis, true);
                if (sock.Connected)
                {
                    sock.EndConnect(asyncResult);
                    #region trace
                    tl.LogMessage(traceId, $"{settings}: Connected");
                    #endregion
                }
                else
                {
                    #region trace
                    tl.LogMessage(traceId, $"{settings}: Connect() failed after {timeoutMillis} millis");
                    #endregion
                    result = $"Cannot connect IP address \"{address}:{IPPort}\"";
                    color = colorError;
                    goto Out;
                }
            }
            catch (Exception ex)
            {
                #region trace
                tl.LogMessage(traceId, $"{settings}: Connect() caught {ex.Message} at {ex.StackTrace}");
                #endregion
                result = $"Cannot connect IP address \"{address}:{IPPort}\"";
                color = colorError;
                goto Out;
            }
            #endregion

            #region Wakeup
            Byte[] rxBytes = new byte[2];
            int nRxBytes = 0, attempt, maxAttempts = 3;

            for (attempt = 0; attempt < maxAttempts; attempt++)
            {
                sock.Send(Encoding.ASCII.GetBytes("\r"), 1, 0);
                nRxBytes = sock.Receive(rxBytes, rxBytes.Length, 0);
                if (nRxBytes == 2 && Encoding.ASCII.GetString(rxBytes, 0, nRxBytes) == "\n\r")
                {
                    #region trace
                    tl.LogMessage(traceId, $"{settings}: Wakeup sequence succeeded.");
                    #endregion
                    break;
                }
                Thread.Sleep(1000);
            }

            if (attempt >= maxAttempts)
            {
                #region trace
                tl.LogMessage(traceId, $"{settings}: Wakeup sequence failed after {attempt + 1} attempts, (received {nRxBytes})");
                #endregion
                result = $"Wakeup failed for IP address \"{address}:{IPPort}\"";
                color = colorError;
                goto Out;
            }
            #endregion

            #region Identify
            char[] txBytes = { 'W', 'R', 'D', (char)0x12, (char)0x4d, '\n' };
            rxBytes = new byte[2];
            IPsocket.Send(Encoding.ASCII.GetBytes(txBytes), txBytes.Length, 0);

            if ((nRxBytes = IPsocket.Receive(rxBytes, rxBytes.Length, 0)) != rxBytes.Length)
            {
                #region trace
                tl.LogMessage(traceId, $"{settings}: Identify: got only {nRxBytes} bytes (instead of 2)");
                #endregion
                result = $"Cannot identify station";
                color = colorError;
                goto Out;
            }

            if (rxBytes[0] != ACK)
            {
                #region trace
                tl.LogMessage(traceId, $"{settings}: Identify: first byte is 0x{rxBytes[0]:X} instead of ACK");
                #endregion
                result = $"Cannot identify station";
                color = colorError;
                goto Out;
            }

            string stationType = "Unknown";
            try
            {
                stationType = ValueToStationModel[rxBytes[1]];
            }
            catch { }
            #endregion

            #region trace
            tl.LogMessage(traceId, $"{settings}: Found a \"{stationType}\" type station.");
            #endregion
            result = $"Found a \"{stationType}\" type station at {settings}.";
            color = colorGood;
        Out:
            sock.Close();
        }
    }
}
