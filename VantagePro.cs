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
        public static TimeSpan interval;
        public static Color colorGood = Color.Green;
        public static Color colorWarning = Color.Yellow;
        public static Color colorError = Color.IndianRed;

        private bool _initialized = false;
        private bool _connected = false;

        private static Fetcher fetcher;

        private static readonly string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        public static readonly string traceLogFile = $"{desktop}\\VantagePro-{DriverVersion}.log";
        public static TraceLogger tl = new TraceLogger(traceLogFile, "VantagePro");

        public VantagePro() {
        }

        static VantagePro() { }

        private static Dictionary<string, string> sensorData = null;

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

        public static string Profile_OpMode   = "OperationMode";
        public static string Profile_Tracing  = "Tracing";
        public static string Profile_Interval = "IntervalSeconds";

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
        /// Wait for the fetcher to fetch new readings
        /// </summary>
        public void Refresh()
        {
            DateTime currentLastRead = fetcher.LastRead;

            while (fetcher.LastRead == currentLastRead)
                Thread.Sleep(500);
        }

        private static string GetStationType()
        {
            return (fetcher == null) ? "Unknown" : fetcher.StationType;
        }

        public static OpMode OperationalMode { get; set; }
        public bool Tracing {
            get
            {
                return VantagePro.tl.Enabled;
            }

            set
            {
                VantagePro.tl.Enabled = value;
            }
        }

        public static readonly List<string> keysInUse = new List<string> {
            "outsideHumidity",
            "outsideDewPt",
            "outsideTemp",
            "barometer",
            "rainRate",
            "windDir",
            "windSpeed",
            "utcDate",
            "utcTime"
        };

        public void Init()
        {
            if (_initialized)
                return;

            ReadProfile();

            if (Tracing)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(traceLogFile));
                }
                catch { }
            }

            switch (OperationalMode)
            {
                case OpMode.File:
                    fetcher = new FileFetcher();
                    break;

                case OpMode.Serial:
                    fetcher = new SerialPortFetcher();
                    break;

                case OpMode.IP:
                    fetcher = new SocketFetcher();
                    break;
            }

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
                if (!_initialized)
                    Init();

                if (fetcher == null)
                    return;

                if (value)
                    fetcher.Start();
                else
                    fetcher.Stop();
                _connected = value;
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
                    Station = new StationData
                    {
                        Name = Name,
                        Vendor = Vendor.ToString(),
                        Model = Model,
                        DataSource = fetcher.DataSource,
                    },
                    SensorData = sensorData,
                };

                return JsonConvert.SerializeObject(raw);
            }
        }
        
        public static string DriverInfo
        {
            get  
            {
                string info;

                if (OperationalMode == OpMode.None)
                {
                    info = "Operational mode was not chosen yet";
                }
                else
                {
                    info = $"station model: {GetStationType()}, ";
                    info += (fetcher == null) ? "" : fetcher.DataSource.ToString();
                }

                var v = AssemblyVersion;
                DateTime buildTime = new DateTime(2000, 1, 1).AddDays(v.Build).AddSeconds(v.Revision * 2);
                info += $", built on: {buildTime:dddd, dd MMMM yyyy HH:mm:ss}";
                return info;
            }
        }

        public static Version AssemblyVersion
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

                Tracing = Convert.ToBoolean(driverProfile.GetValue(DriverId, Profile_Tracing, string.Empty, "False"));
                interval = TimeSpan.FromSeconds(Convert.ToInt32(driverProfile.GetValue(DriverId, Profile_Interval, string.Empty, "30")));
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
                driverProfile.WriteValue(DriverId, Profile_Tracing, Tracing.ToString());
                driverProfile.WriteValue(DriverId, Profile_Interval, interval.TotalSeconds.ToString());
                fetcher.WriteProfile();
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
                return fetcher.DewPoint;
            }
        }

        public double Humidity
        {
            get
            {
                return fetcher.Humidity;
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
                return fetcher.Pressure;
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
                return fetcher.RainRate;
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
                    throw new InvalidValueException("SensorDescription(" + PropertyName + ")");
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
                return fetcher.Temperature;
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
                    throw new MethodNotImplementedException("TimeSinceLastUpdate(" + PropertyName + ")");
            }

            return fetcher.TimeSinceLastUpdate(PropertyName);
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
                return fetcher.WindDirection;
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
                return fetcher.WindSpeedMps;
            }
        }

        #endregion

        public override bool Enabled { get; } = true;

        public override string Name {
            get
            {
                if (sensorData.Keys.Contains("StationName") && !string.IsNullOrEmpty(sensorData["StationName"]))
                    return sensorData["StationName"];
                return "Unknown";
            }
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
                if (OperationalMode == OpMode.File)
                    return WeatherStationVendor.Unknown;
                return WeatherStationVendor.DavisInstruments;
            }
        }

        public override string Model
        {
            get
            {
                return GetStationType();
            }
        }

        public class DataSourceClass
        {
            public string Type;
            public string Details;

            public override string ToString()
            {
                return $"{Type}:{Details}";
            }
        }
        public class StationData
        {
            public string Name;
            public string Vendor;
            public string Model;
            public DataSourceClass DataSource;
        };

        public class VantagePro2StationRawData
        {
            public StationData Station;
            public Dictionary<string, string> SensorData;
        }
    }
}
