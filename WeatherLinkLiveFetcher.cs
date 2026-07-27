using System;
using System.Drawing;
using ASCOM.Utilities;

namespace ASCOM.VantagePro
{
    /// <summary>
    /// Speaks the modern WeatherLink Live local API instead of the legacy
    /// binary WeatherLinkIP protocol: a plain HTTP GET of
    /// /v1/current_conditions (port 80 on a real WeatherLink Live hub),
    /// returning JSON rather than a wakeup/WRD/LOOP binary exchange.
    ///
    /// Shares its IP address/port profile keys with <see cref="SocketFetcher"/>
    /// since both are just "the WeatherLinkIP mode" (VantagePro.OpMode.IP) with
    /// a different wire protocol selected via VantagePro.IPProtocol -- same
    /// physical host:port, same Setup dialog fields.
    ///
    /// The actual fetch/parse/unit-conversion logic lives in
    /// WeatherLinkLiveParser (no ASCOM.Utilities dependency, so it can also
    /// run standalone under wll-cli); this class is the thin ASCOM-side
    /// wrapper that calls it and copies results into sensorData, with
    /// tracing and Profile-backed settings persistence on top.
    /// </summary>
    public class WeatherLinkLiveFetcher : Fetcher
    {
        public const ushort defaultPort = 80;

        private string _stationModel = "Unknown";
        private string _stationName = "Unknown";

        public string Address { get; set; }
        public UInt16 Port { get; set; } = defaultPort;

        public WeatherLinkLiveFetcher()
        {
            ReadLowerProfile();
            lowerFetcher = this;
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store. Reuses
        /// SocketFetcher's profile keys: this is the same "IP" operational
        /// mode, just a different protocol.
        /// </summary>
        public override void ReadLowerProfile()
        {
            using (Profile driverProfile = new Profile() { DeviceType = "ObservingConditions" })
            {
                string op = "ReadLowerProfile";
                Address = driverProfile.GetValue(DriverId, SocketFetcher.Profile_IPAddress, string.Empty, "");
                Port = Convert.ToUInt16(driverProfile.GetValue(DriverId, SocketFetcher.Profile_IPPort, string.Empty, defaultPort.ToString()));
                #region trace
                VantagePro.LogMessage(op, $"Address: '{Address}', Port: '{Port}'");
                #endregion
            }
        }

        public override void WriteLowerProfile()
        {
            using (Profile driverProfile = new Profile() { DeviceType = "ObservingConditions" })
            {
                string op = "WriteLowerProfile";

                driverProfile.WriteValue(DriverId, SocketFetcher.Profile_IPAddress, Address);
                driverProfile.WriteValue(DriverId, SocketFetcher.Profile_IPPort, Port.ToString());
                #region trace
                VantagePro.LogMessage(op, $"Address: '{Address}', Port: '{Port}'");
                #endregion
            }
        }

        public string Source
        {
            get
            {
                return $"[{Address}:{Port}]";
            }
        }

        private string Url
        {
            get
            {
                return $"http://{Address}:{Port}/v1/current_conditions";
            }
        }

        public override void FetchSensorData()
        {
            string op = "FetchSensorData";

            if (string.IsNullOrWhiteSpace(Address))
            {
                #region trace
                VantagePro.LogMessage(op, "empty address");
                #endregion
                return;
            }

            string json = WeatherLinkLiveParser.FetchJson(Address, Port, 5000, out string error);
            if (json == null)
            {
                #region trace
                VantagePro.LogMessage(op, $"{Source}: GET {Url} failed: {error}");
                #endregion
                return;
            }

            WeatherLinkLiveConditions cond = WeatherLinkLiveParser.Parse(json, out error);
            if (cond == null)
            {
                #region trace
                VantagePro.LogMessage(op, $"{Source}: {error}");
                #endregion
                return;
            }

            lock (sensorDataLock)
            {
                if (cond.TempC.HasValue)
                {
                    sensorData["outsideTemp"] = cond.TempC.Value.ToString();
                    #region trace
                    VantagePro.tl.LogMessage("outsideTemp", $"sensorData[\"outsideTemp\"]: {sensorData["outsideTemp"]} (Celsius)");
                    #endregion
                }

                if (cond.HumidityPct.HasValue)
                    sensorData["outsideHumidity"] = cond.HumidityPct.Value.ToString();

                if (cond.DewPointC.HasValue)
                    sensorData["outsideDewPt"] = cond.DewPointC.Value.ToString();

                if (cond.WindSpeedMps.HasValue)
                {
                    sensorData["windSpeed"] = cond.WindSpeedMps.Value.ToString();
                    #region trace
                    VantagePro.tl.LogMessage("windSpeed", $"sensorData[\"windSpeed\"]: {sensorData["windSpeed"]} (m/s)");
                    #endregion
                }

                if (cond.WindDirDeg.HasValue)
                    sensorData["windDir"] = cond.WindDirDeg.Value.ToString();

                if (cond.WindGustMps.HasValue)
                {
                    sensorData["windGust"] = cond.WindGustMps.Value.ToString();
                    #region trace
                    VantagePro.tl.LogMessage("windGust", $"sensorData[\"windGust\"]: {sensorData["windGust"]}");
                    #endregion
                }

                if (cond.RainRateInPerHr.HasValue)
                    sensorData["rainRate"] = cond.RainRateInPerHr.Value.ToString();

                if (cond.BarometerHPa.HasValue)
                    sensorData["barometer"] = cond.BarometerHPa.Value.ToString();

                _stationName = cond.StationId ?? _stationName;
            }

            LastRead = DateTime.Now;
            #region trace
            VantagePro.LogMessage(op, $"{Source}: End");
            #endregion
        }

        public override string StationModel
        {
            get
            {
                return _stationModel;
            }

            set
            {
                _stationModel = value;
            }
        }

        public override string StationName
        {
            get
            {
                return _stationName;
            }

            set
            {
                _stationName = value;
            }
        }

        public override VantagePro.DataSourceClass DataSource
        {
            get
            {
                return new VantagePro.DataSourceClass
                {
                    Type = "http",
                    Details = Url,
                };
            }
        }

        public void Test(string address, string port, ref string result, ref Color color)
        {
            string op = "WeatherLinkLive.Test";

            #region trace
            VantagePro.LogMessage(op, "Start");
            #endregion
            if (string.IsNullOrWhiteSpace(address))
            {
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

            string json = WeatherLinkLiveParser.FetchJson(Address, Port, 5000, out string error);
            WeatherLinkLiveConditions cond = json == null ? null : WeatherLinkLiveParser.Parse(json, out error);

            if (cond != null)
            {
                result = $"Found a WeatherLink Live station (did: {cond.StationId}) at {Url}.";
                color = VantagePro.colorGood;
                #region trace
                VantagePro.LogMessage(op, result);
                #endregion
            }
            else
            {
                result = $"Could not get current conditions from {Url}: {error}";
                color = VantagePro.colorError;
                #region trace
                VantagePro.LogMessage(op, result);
                #endregion
            }
            #region trace
            VantagePro.LogMessage(op, "Done");
            #endregion
        }
    }
}
