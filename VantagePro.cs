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

        public string serialPortName = null;
        public int serialPortSpeed = 19200;
        System.IO.Ports.SerialPort serialPort = new System.IO.Ports.SerialPort();

        public Socket IPsocket;

        private bool _connected = false;
        private bool _initialized = false;

        public static TraceLogger tl = new TraceLogger(traceLogFile, "VantagePro");

        public VantagePro() { }

        static VantagePro() { }

        private Dictionary<string, string> sensorData = null;
        private DateTime _lastDataRead = DateTime.MinValue;

        private static readonly Lazy<VantagePro> lazy = new Lazy<VantagePro>(() => new VantagePro()); // Singleton

        public static readonly string traceLogFile = $"{Path.GetTempPath()}VantagePro v{version}.log";
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
                RefreshFrom_DataFile();
                break;

            case OpMode.Serial:
                RefreshFrom_SerialPort();
                break;

            case OpMode.IP:
                RefreshFrom_Socket();
                break;
            }
        }

        public OpMode OperationalMode { get; set; }
        public bool Tracing { get; set; }

        public void RefreshFrom_DataFile()
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

        private void TryOpenStation_Serial()
        {
            if (serialPort == null)
                serialPort = new System.IO.Ports.SerialPort();
            else if (serialPort.IsOpen)
                return;

            serialPort.PortName = serialPortName;
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

        private bool TryOpenStation_Socket()
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

        private bool TryWakeUpStation_Serial()
        {
            TryOpenStation_Serial();

            bool awake = false;
            for (var attempts = 3; attempts != 0; attempts--)
            {
                serialPort.Write("\r");
                if (serialPort.ReadExisting() == "\n\r")
                {
                    awake = true;
                    #region trace
                    tl.LogMessage("TryWakeUpSerialVantagePro", $"Successfully woke up {SerialPortName}");
                    #endregion
                    break;
                }
            }

            if (!awake)
            {
                #region trace
                tl.LogMessage("TryWakeUpSerialVantagePro", $"Failed to wake up {SerialPortName}");
                #endregion
            }
            return awake;
        }

        private bool TryWakeUpStation_Socket()
        {
            TryOpenStation_Socket();

            Byte[] bytesSent = Encoding.ASCII.GetBytes("\r");
            Byte[] bytesReceived = new byte[2];
            int nBytes;

            for (var attempts = 3; attempts != 0; attempts--)
            {
                IPsocket.Send(bytesSent, 1, 0);
                nBytes = IPsocket.Receive(bytesReceived, bytesReceived.Length, 0);
                if (nBytes == 2 && Encoding.ASCII.GetString(bytesReceived, 0, nBytes) == "\n\r")
                {
                    #region trace
                    tl.LogMessage("TryWakeUpIPVantagePro", $"Succesfully woke up {IPAddress}:{IPPort}");
                    #endregion
                    return true;
                }
            }

            #region trace
            tl.LogMessage("TryWakeUpIPVantagePro", $"Failed to wake up {IPAddress}:{IPPort}");
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
        public ushort GetTwoBytes(byte[] bytes, int o)
        {
            return (ushort) ((bytes[o + 1] << 8) | bytes[o]);
        }

        public void RefreshFrom_SerialPort()
        {
            if (!TryWakeUpStation_Serial())
                return;

            byte[] buf = new byte[99];
            serialPort.Write("LPS 2 1\n");

            if (serialPort.ReadByte() != 0x6)
                return;

            if (serialPort.Read(buf, 0, 99) != 99)
            {
                #region trace
                tl.LogMessage("RefreshFromSerialPort", $"Failed to read 99 bytes from {SerialPortName}");
                #endregion
                return;
            }

            #region trace
            tl.LogMessage("RefreshFromSerialPort", $"Successfully read 99 bytes from {SerialPortName}");
            #endregion
            GetSensorData(buf);
            _lastDataRead = DateTime.Now;
        }

        private void RefreshFrom_Socket()
        {
            if (!TryWakeUpStation_Socket())
                return;

            string LPS = "LPS 2 1\n";
            Byte[] txBytes = Encoding.ASCII.GetBytes(LPS);
            Byte[] rxBytes = new byte[99];

            IPsocket.Send(txBytes, txBytes.Length, 0);
            IPsocket.Receive(rxBytes, 1, 0);
            if (rxBytes[0] != 0x6)
                return;

            if (IPsocket.Receive(rxBytes, rxBytes.Length, 0) != 99)
            {
                #region trace
                tl.LogMessage("RefreshFromSocket", $"Failed to receive 99 bytes from {IPAddress}:{IPPort}");
                #endregion
                return;
            }

            #region trace
            tl.LogMessage("RefreshFromSocket", $"Received 99 bytes from {IPAddress}:{IPPort}");
            #endregion
            GetSensorData(rxBytes);
            _lastDataRead = DateTime.Now;
        }

        private void GetSensorData(byte[] buf) {
            //
            // Check the reply is valid - TBD verify the checksum
            // buf[4] == 1 for LOOP2 packets
            //
            if (buf[0] != 'L' || buf[1] != 'O' || buf[2] != 'O' || buf[4] != 1 || buf[95] != '\n' || buf[96] != '\r')
                return;

            ASCOM.Utilities.Util util = new Util();

            double F = GetTwoBytes(buf, 12) / 10.0;
            sensorData["outsideTemp"] = util.ConvertUnits(F, Units.degreesFarenheit, Units.degreesCelsius).ToString();
            sensorData["windSpeed"] = util.ConvertUnits(buf[14], Units.milesPerHour, Units.metresPerSecond).ToString();
            sensorData["windDir"] = GetTwoBytes(buf, 16).ToString();
            sensorData["windGust"] = util.ConvertUnits(GetTwoBytes(buf, 22) * 10, Units.milesPerHour, Units.metresPerSecond).ToString();
            sensorData["outsideHumidity"] = buf[33].ToString();
            double P = GetTwoBytes(buf, 7);
            sensorData["barometer"] = util.ConvertUnits(P, Units.mmHg, Units.hPa).ToString();
            F = GetTwoBytes(buf, 30);
            sensorData["outsideDewPt"] = util.ConvertUnits(F, Units.degreesFarenheit, Units.degreesCelsius).ToString();
            sensorData["rainRate"] = GetTwoBytes(buf, 41).ToString();
            sensorData["ForecastStr"] = "No forecast";

            #region trace
            tl.LogMessage("GetSensorData", $"Successfully parsed sensor data (packet CRC: {GetTwoBytes(buf, 97):X2})");
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
                            TryOpenStation_Serial();
                        else
                            serialPort.Close();
                        _connected = serialPort.IsOpen;
                        break;

                    case OpMode.File:
                        _connected = value && DataFile != null && DataFile != "" && File.Exists(DataFile);
                        break;

                    case OpMode.IP:
                        _connected = value ? TryOpenStation_Socket() : TryCloseStation_Socket();
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
            "forecast",
        };

        public ArrayList SupportedActions
        {
            get
            {
                return supportedActions;
            }
        }

        public string Action(string action, string parameter)
        {
            switch (action)
            {
                case "OCHTag":
                    return "VantagePro";

                case "raw-data":
                    return RawData;

                case "forecast":
                    return Forecast;

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
        public string SensorDescription(string PropertyName)
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
                case "WindGust":
                    return "SensorDescription - " + PropertyName;

                case "SkyBrightness":
                case "SkyQuality":
                case "StarFWHM":
                case "SkyTemperature":
                case "CloudCover":
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
                seconds = (DateTime.UtcNow - _lastDataRead).TotalSeconds;
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
                Refresh();
                return Convert.ToDouble(sensorData["windGust"]);
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

        public string Forecast
        {
            get
            {
                Refresh();
                var forecast = sensorData["ForecastStr"];

                return forecast;
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
