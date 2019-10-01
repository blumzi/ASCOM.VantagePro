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

using Git;


namespace ASCOM.VantagePro
{
    public class VantagePro: WeatherStation
    {
        private string _dataFile;
        private static string version = Git.Commit.VersionTag;
        private static string driverDescription = string.Format("ASCOM VantagePro2 {0}", version);
        private Util util = new Util();

        public enum OpMode { File, Serial };
        public OpMode _opMode = VantagePro.OpMode.File;

        public string _portName = null;
        public int _portSpeed = 19200;
        System.IO.Ports.SerialPort _port = new System.IO.Ports.SerialPort();

        private bool _connected = false;
        private bool _initialized = false;

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

        public static string VantagePro_OpMode = "OperationMode";
        public static string VantagePro_DataFile = "DataFile";
        public static string VantagePro_SerialPort = "Port";

        public static VantagePro Instance
        {
            get
            {
                if (lazy.IsValueCreated)
                    return lazy.Value;

                lazy.Value.init();
                return lazy.Value;
            }
        }

        /// <summary>
        /// Forces the driver to immediatley query its attached hardware to refresh sensor
        /// values
        /// </summary>
        public void Refresh()
        {
            if (_opMode == OpMode.File)
                RefreshFromDatafile();
            else
                RefreshFromSerialPort();
        }

        public OpMode OperationalMode
        {
            get
            {
                return _opMode;
            }

            set
            {
                _opMode = value;
            }
        }

        public void RefreshFromDatafile()
        {
            if (_dataFile == null || _dataFile == string.Empty)
            {
                if (_connected)
                    throw new InvalidValueException("Null or empty dataFile name");
                else
                    return;
            }

            if (_lastDataRead == DateTime.MinValue || File.GetLastWriteTime(_dataFile).CompareTo(_lastDataRead) > 0)
            {
                if (sensorData == null)
                    sensorData = new Dictionary<string, string>();

                for (int tries = 5; tries != 0; tries--)
                {
                    try
                    {
                        using (StreamReader sr = new StreamReader(_dataFile))
                        {
                            string[] words;
                            string line;

                            if (sr == null)
                                throw new InvalidValueException(string.Format("Refresh: cannot open \"{0}\" for read.", _dataFile));

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

        private void TryOpenPort()
        {
            if (_port == null)
                _port = new System.IO.Ports.SerialPort();
            else if (_port.IsOpen)
                return;

            _port.PortName = _portName;
            _port.BaudRate = _portSpeed;
            _port.ReadTimeout = 1000;
            _port.ReadBufferSize = 100;
            try
            {
                _port.Open();
            }
            catch
            {
                throw;
            }
        }

        public string PortName
        {
            get
            {
                return _portName;
            }

            set
            {
                _portName = value;
            }
        }

        private bool TryWakeUpVantagePro()
        {
            TryOpenPort();

            bool awake = false;
            for (var attempts = 3; attempts != 0; attempts--)
            {
                _port.Write("\r");
                if (_port.ReadExisting() == "\n\r")
                {
                    awake = true;
                    break;
                }
            }

            if (!awake)
            {
                #region debug
                #endregion
            }
            return awake;
        }

        /// <summary>
        /// Gets a VantagePro two-bytes entity from the LPS command reply block
        ///  They are transmitted LSB first - (buf[offset] = LSB, buf[offset+1] = MSB)
        /// </summary>
        /// <param name="bytes">The stream of bytes in the reply block</param>
        /// <param name="o">The starting offset</param>
        /// <returns></returns>
        public ushort getTwoBytes(byte[] bytes, int o)
        {
            return (ushort) ((bytes[o + 1] << 8) | bytes[o]);
        }

        public void RefreshFromSerialPort()
        {
            if (!TryWakeUpVantagePro())
                return;

            byte[] buf = new byte[99];
            _port.Write("LPS 2 1\n");

            if (_port.ReadByte() != 0x6)
                return;

            if (_port.Read(buf, 0, 99) != 99)
                return;

            // Check the reply is valid - TBD verify the checksum
            if (buf[0] != 'L' || buf[1] != 'O' || buf[2] != 'O' || buf[4] != 1 || buf[95] != '\n' || buf[96] != '\r')
                return;

            ASCOM.Utilities.Util util = new Util();

            double F = getTwoBytes(buf, 12) / 10.0;
            sensorData["outsideTemp"] = util.ConvertUnits(F, Units.degreesFarenheit, Units.degreesCelsius).ToString();
            sensorData["windSpeed"] = util.ConvertUnits(buf[14], Units.milesPerHour, Units.metresPerSecond).ToString();
            sensorData["windDir"] = getTwoBytes(buf, 16).ToString();
            sensorData["outsideHumidity"] = buf[33].ToString();
            sensorData["barometer"] = getTwoBytes(buf, 7).ToString();
            F = getTwoBytes(buf, 30);
            sensorData["outsideDewPt"] = util.ConvertUnits(F, Units.degreesFarenheit, Units.degreesCelsius).ToString();
            sensorData["rainRate"] = getTwoBytes(buf, 41).ToString();
            sensorData["ForecastStr"] = "No forecast";
        }

        public void init()
        {
            if (_initialized)
                return;

            _name = "VantagePro";
            sensorData = new Dictionary<string, string>();
            ReadProfile();
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

                if (_opMode == OpMode.Serial)
                {
                    if (value == true)
                        TryOpenPort();
                    else
                        _port.Close();
                    _connected = _port.IsOpen;
                } else
                    _connected = value;
            }
        }

        public string Description
        {
            get
            {
                return driverDescription;
            }
        }

        private static ArrayList supportedActions = new ArrayList() {
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
            string ret = "";

            switch (action)
            {
                case "OCHTag":
                    ret = "Wise40.VantagePro2";
                    break;

                case "raw-data":
                    ret = RawData;
                    break;

                case "forecast":
                    return Forecast;

                default:
                    throw new ASCOM.ActionNotImplementedException("Action " + action + " is not implemented by this driver");
            }
            return ret;
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
                return "VantagePro2 Report File or Serial Port driver, " +  version;
            }
        }

        public static string DriverVersion
        {
            get
            {
                return version;
            }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal void ReadProfile()
        {
            string defaultReportFile = "C:/Weather/Davis VantagePro/Weather_Vantage_Pro.htm";

            using (Profile driverProfile = new Profile() { DeviceType = "ObservingConditions" })
            {
                OpMode mode;

                Enum.TryParse<OpMode>(driverProfile.GetValue(DriverId, VantagePro_OpMode, string.Empty, OpMode.File.ToString()), out mode);
                _opMode = mode;
                _dataFile = driverProfile.GetValue(DriverId, VantagePro_DataFile, string.Empty, defaultReportFile);
                _portName = driverProfile.GetValue(DriverId, VantagePro_SerialPort, string.Empty, "");
            }
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal void WriteProfile()
        {
            using (Profile driverProfile = new Profile() { DeviceType = "ObservingConditions" })
            {
                driverProfile.WriteValue(DriverId, VantagePro_OpMode, _opMode.ToString());
                driverProfile.WriteValue(DriverId, VantagePro_DataFile, _dataFile);
                driverProfile.WriteValue(DriverId, VantagePro_SerialPort, _portName);
            }
        }

        public string DataFile
        {
            get
            {
                return _dataFile;
            }

            set
            {
                _dataFile = value;
            }
        }

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
                    return "SensorDescription - " + PropertyName;

                case "SkyBrightness":
                case "SkyQuality":
                case "StarFWHM":
                case "SkyTemperature":
                case "WindGust":
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
                case "WindGust":
                case "CloudCover":
                    throw new MethodNotImplementedException("SensorDescription(" + PropertyName + ")");
            }
            Refresh();

            double seconds = 0.0;
            if (_opMode == OpMode.File)
            {
                string dateTime = sensorData["utcDate"] + " " + sensorData["utcTime"] + "m";
                DateTime lastUpdate = Convert.ToDateTime(dateTime);
                seconds = (DateTime.UtcNow - lastUpdate).TotalSeconds;
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
