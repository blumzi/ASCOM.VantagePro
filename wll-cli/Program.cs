using System;
using System.Threading;
using ASCOM.VantagePro;

namespace ASCOM.VantagePro.WllCli
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1 || args[0] == "-h" || args[0] == "--help")
            {
                Console.Error.WriteLine("usage: wll-cli <host> [port=80] [--raw] [--loop] [--interval seconds=10]");
                return 2;
            }

            const int defaultPort = 80; // matches WeatherLinkLiveFetcher.defaultPort

            string host = args[0];
            int port = defaultPort;
            bool raw = false;
            bool loop = false;
            int intervalSeconds = 10;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--raw")
                    raw = true;
                else if (args[i] == "--loop")
                    loop = true;
                else if (args[i] == "--interval" && i + 1 < args.Length)
                    intervalSeconds = int.Parse(args[++i]);
                else if (int.TryParse(args[i], out int p))
                    port = p;
            }

            int exitCode = 0;
            do
            {
                exitCode = RunOnce(host, port, raw);
                if (loop)
                {
                    Console.WriteLine(new string('-', 60));
                    Thread.Sleep(intervalSeconds * 1000);
                }
            } while (loop);

            return exitCode;
        }

        static int RunOnce(string host, int port, bool raw)
        {
            string url = $"http://{host}:{port}/v1/current_conditions";
            Console.WriteLine($"GET {url}");

            string json = WeatherLinkLiveParser.FetchJson(host, port, 5000, out string error);
            if (json == null)
            {
                Console.WriteLine($"FAIL  request failed: {error}");
                return 1;
            }

            if (raw)
            {
                Console.WriteLine("--- raw response body ---");
                Console.WriteLine(json);
                Console.WriteLine("--------------------------");
            }

            WeatherLinkLiveConditions cond = WeatherLinkLiveParser.Parse(json, out error);
            if (cond == null)
            {
                Console.WriteLine($"FAIL  parse failed: {error}");
                Console.WriteLine("      (this is exactly why the driver would show every property as N/A --");
                Console.WriteLine("       FetchSensorData() bails out here without touching sensorData at all)");
                return 1;
            }

            Console.WriteLine("OK    parsed current conditions:");
            Console.WriteLine($"        StationId       = {cond.StationId}");
            PrintField("TempC", cond.TempC, "C");
            PrintField("HumidityPct", cond.HumidityPct, "%");
            PrintField("DewPointC", cond.DewPointC, "C");
            PrintField("WindSpeedMps", cond.WindSpeedMps, "m/s");
            PrintField("WindDirDeg", cond.WindDirDeg, "deg");
            PrintField("WindGustMps", cond.WindGustMps, "m/s");
            PrintField("RainRateInPerHr", cond.RainRateInPerHr, "in/hr");
            PrintField("BarometerHPa", cond.BarometerHPa, "hPa");

            int missing = 0;
            missing += cond.TempC.HasValue ? 0 : 1;
            missing += cond.HumidityPct.HasValue ? 0 : 1;
            missing += cond.DewPointC.HasValue ? 0 : 1;
            missing += cond.WindSpeedMps.HasValue ? 0 : 1;
            missing += cond.WindGustMps.HasValue ? 0 : 1;
            missing += cond.RainRateInPerHr.HasValue ? 0 : 1;
            missing += cond.BarometerHPa.HasValue ? 0 : 1;
            if (missing > 0)
                Console.WriteLine($"WARN  {missing} field(s) came back null -- those specific ASCOM properties would show N/A even though the fetch/parse itself succeeded");

            return 0;
        }

        static void PrintField(string name, double? value, string unit)
        {
            string shown = value.HasValue ? $"{value.Value} {unit}" : "(null -- missing/null in source JSON)";
            Console.WriteLine($"        {name,-16}= {shown}");
        }
    }
}
