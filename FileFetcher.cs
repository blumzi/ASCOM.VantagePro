using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Drawing;
using ASCOM.Utilities;

namespace ASCOM.VantagePro
{
    public class FileFetcher : Fetcher
    {
        public const string Profile_DataFile = "DataFile";

        public static string DataFile { get; set; }
        public string _stationName = "Unknown";
        public string _stationModel = "Unknown";

        public FileFetcher()
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
                DataFile = driverProfile.GetValue(DriverId, Profile_DataFile, string.Empty, "");
            }
        }

        public override void WriteLowerProfile()
        {
            using (Profile driverProfile = new Profile() { DeviceType = "ObservingConditions" })
            {
                driverProfile.WriteValue(DriverId, Profile_DataFile, DataFile);
            }
        }

        public string Source
        {
            get
            {
                return $"[{DataFile}]";
            }
        }

        public override string StationModel
        {
            get
            {
                if (!sensorData.TryGetValue("stationModel", out _stationModel))
                    return "Unknown";
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
                if (!sensorData.TryGetValue("stationName", out _stationName))
                    return "Unknown";
                return _stationName;
            }

            set
            {
                _stationName = value;
            }
        }

        public override void FetchSensorData()
        {
            string op = "File.ReadSensors";

            if (string.IsNullOrEmpty(DataFile))
            {
                #region trace
                VantagePro.LogMessage(op, "Empty file name");
                #endregion
                return;
            }

            if (LastRead == DateTime.MinValue || File.GetLastWriteTime(DataFile).CompareTo(LastRead) > 0)
            {
                #region trace
                VantagePro.LogMessage(op, $">>> Start");
                #endregion
                lock (sensorDataLock)
                {
                    for (int tries = 5; tries != 0; tries--)
                    {
                        try
                        {
                            using (StreamReader sr = new StreamReader(DataFile))
                            {
                                string[] words;
                                string line, key, value;

                                if (sr == null)
                                    throw new InvalidValueException($"{op}: cannot open \"{DataFile}\" for read.");

                                while ((line = sr.ReadLine()) != null)
                                {
                                    words = line.Split('=');
                                    if (words.Length < 2)
                                        continue;

                                    key = words[0].Trim();
                                    value = words[1].Trim();
                                    sensorData[key] = value;
                                    #region trace
                                    VantagePro.LogMessage(op, $"sensorData[{key}] = \"{sensorData[key]}\"");
                                    #endregion
                                }

                                string keyDate = "utcDate", keyTime = "utcTime";

                                if ((sensorData.ContainsKey(keyDate) && !string.IsNullOrEmpty(sensorData[keyDate])) &&
                                    (sensorData.ContainsKey(keyTime) && !string.IsNullOrEmpty(sensorData[keyTime])))
                                {
                                    string dateTime = sensorData[keyDate] + " " + sensorData[keyTime] + "m";
                                    LastRead = TryParseDateTime_LocalThenEnUS(dateTime);
                                }
                                else
                                    LastRead = DateTime.Now;
                                break;
                            }
                        }
                        catch
                        {
                            Thread.Sleep(500);  // Another process may be writing the file
                        }
                    }
                }
                #region trace
                VantagePro.LogMessage(op, $"<<< End");
                #endregion
            }
            else
            {
                #region trace
                VantagePro.LogMessage(op, $"{Source} did not change");
                #endregion
            }
        }

        public override VantagePro.DataSourceClass DataSource
        {
            get
            {
                return new VantagePro.DataSourceClass
                {
                    Type = "file",
                    Details = Source,
                };
            }
        }

        public void Test(string path, ref string result, ref Color color)
        {
            #region trace
            string traceId = "TestFileSettings";
            #endregion

            if (string.IsNullOrEmpty(path))
            {
                #region trace
                VantagePro.LogMessage(traceId, "Empty report file name");
                #endregion
                color = VantagePro.colorError;
                result = "Empty report file name!";
                goto Out;
            }
            DataFile = path;

            if (!File.Exists(path))
            {
                #region trace
                VantagePro.LogMessage(traceId, $"{Source}: File does not exist");
                #endregion
                result = $"File \"{path}\" does not exist.";
                color = VantagePro.colorError;
                goto Out;
            }
            #region trace
            VantagePro.LogMessage(traceId, $"{Source}: File exists");
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

                        if (sr == null)
                        {
                            continue;
                        }

                        while ((line = sr.ReadLine()) != null)
                        {
                            words = line.Split('=');
                            if (words.Length < 2)
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
                VantagePro.LogMessage(traceId, $"{Source}: Failed to parse file contents");
                #endregion
                result = $"Cannot get weather data from \"{path}\".";
                color = VantagePro.colorError;
                goto Out;
            }
            #region trace
            foreach (var key in dict.Keys) {
                 VantagePro.LogMessage(traceId, $"{Source}: dict[\"{key}\"] = {dict[key]}");
            }
            #endregion

            result = $"\"{path}\" contains a valid report, station name: {StationName}";
            #region trace
            VantagePro.LogMessage(traceId, $"{Source}: Success, the file contains a valid weather report (stationName: {StationName})");
            #endregion
            color = VantagePro.colorGood;
        Out:
            ;
        }
    }
}