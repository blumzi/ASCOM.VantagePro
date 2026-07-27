using System;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;

namespace ASCOM.VantagePro
{
    /// <summary>
    /// Plain values parsed out of one /v1/current_conditions response, in the
    /// same units Fetcher's properties expect (Celsius, m/s, hPa, in/hr).
    /// </summary>
    public class WeatherLinkLiveConditions
    {
        public string StationId;
        public double? TempC;
        public double? HumidityPct;
        public double? DewPointC;
        public double? WindSpeedMps;
        public double? WindDirDeg;
        public double? WindGustMps;
        public double? RainRateInPerHr;
        public double? BarometerHPa;
    }

    /// <summary>
    /// Fetch + parse logic for the WeatherLink Live local API
    /// (GET /v1/current_conditions), deliberately free of any ASCOM.Utilities
    /// dependency (Profile/Util/TraceLogger all pull in Microsoft.VisualBasic
    /// at runtime, which real .NET Framework/Windows has but plain Mono does
    /// not -- Debian's Mono package ships no runtime implementation of it at
    /// all). That split lets this class run standalone (e.g. from wll-cli,
    /// a command-line harness usable on Linux/Mono for diagnosing the wire
    /// protocol without the full ASCOM Platform installed) while
    /// WeatherLinkLiveFetcher stays a thin wrapper that calls this and copies
    /// results into the ASCOM-side sensorData dictionary, with its own
    /// tracing on top.
    /// </summary>
    public static class WeatherLinkLiveParser
    {
        public const double HpaPerInHg = 33.8639;
        public const double MpsPerMph = 0.44704;

        public static double FahrenheitToCelsius(double f)
        {
            return (f - 32.0) * 5.0 / 9.0;
        }

        /// <summary>GET http://address:port/v1/current_conditions. Returns null (and sets error) on any failure.</summary>
        public static string FetchJson(string address, int port, int timeoutMs, out string error)
        {
            error = null;
            string url = $"http://{address}:{port}/v1/current_conditions";

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = timeoutMs;
                request.ReadWriteTimeout = timeoutMs;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                error = $"{ex.GetType().Name}: {ex.Message}";
                return null;
            }
        }

        private static double? AsDouble(JToken token)
        {
            return (token == null || token.Type == JTokenType.Null) ? (double?)null : token.Value<double>();
        }

        /// <summary>
        /// Parses one /v1/current_conditions response body. Returns null
        /// (and sets error) on malformed JSON, a well-formed "no data"
        /// response, or a response with no data_structure_type=1 (ISS) block
        /// -- the only field set this driver actually reads.
        /// </summary>
        public static WeatherLinkLiveConditions Parse(string json, out string error)
        {
            error = null;

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                error = $"malformed JSON: {ex.Message}";
                return null;
            }

            if (!(root["data"] is JObject data))
            {
                error = (string)root["error"] ?? "no \"data\" in response (station reports no current conditions)";
                return null;
            }

            if (!(data["conditions"] is JArray conditions))
            {
                error = "response has no \"conditions\" array";
                return null;
            }

            JObject iss = null, bar = null;
            foreach (JToken cond in conditions)
            {
                int? type = (int?)cond["data_structure_type"];
                if (type == 1 && iss == null)
                    iss = cond as JObject;
                else if (type == 3 && bar == null)
                    bar = cond as JObject;
            }

            if (iss == null)
            {
                error = "no data_structure_type=1 (ISS) block in conditions -- nothing to parse";
                return null;
            }

            var result = new WeatherLinkLiveConditions { StationId = (string)data["did"] };

            double? tempF = AsDouble(iss["temp"]);
            if (tempF.HasValue)
                result.TempC = FahrenheitToCelsius(tempF.Value);

            result.HumidityPct = AsDouble(iss["hum"]);

            double? dewF = AsDouble(iss["dew_point"]);
            if (dewF.HasValue)
                result.DewPointC = FahrenheitToCelsius(dewF.Value);

            double? windAvgMph = AsDouble(iss["wind_speed_avg_last_2_min"]);
            if (windAvgMph.HasValue)
                result.WindSpeedMps = windAvgMph.Value * MpsPerMph;

            result.WindDirDeg = AsDouble(iss["wind_dir_scalar_avg_last_2_min"]);

            // See WeatherLinkLiveFetcher for why wind_speed_hi_last_10_min is
            // used as the closest analog to "gust" this API offers.
            double? windGustMph = AsDouble(iss["wind_speed_hi_last_10_min"]);
            if (windGustMph.HasValue)
                result.WindGustMps = windGustMph.Value * MpsPerMph;

            result.RainRateInPerHr = AsDouble(iss["rain_rate_last"]);

            if (bar != null)
            {
                double? barInHg = AsDouble(bar["bar_sea_level"]);
                if (barInHg.HasValue)
                    result.BarometerHPa = barInHg.Value * HpaPerInHg;
            }

            return result;
        }
    }
}
