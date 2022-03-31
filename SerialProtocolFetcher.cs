using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using ASCOM.Utilities;

namespace ASCOM.VantagePro
{
    public abstract class SerialProtocolFetcher: Fetcher
    {
        private static readonly Util util = new Util();

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

        protected char[] GetStationTypeTxBytes = { 'W', 'R', 'D', (char)0x12, (char)0x4d, '\n' };
        protected char[] GetLoopTxBytes = { 'L', 'O', 'O', 'P', ' ', '1', '\n' };

        private bool CalculateCRC(byte[] bytes)
        {
            UInt16 crc = 0;

            for (int i = 0; i < bytes.Length; i++)
                crc = (UInt16)(crc_table[(crc >> 8) ^ bytes[i]] ^ (crc << 8));

            #region trace
             VantagePro.LogMessage("CalculateCRC", $"CRC: {(crc == 0 ? "OK" : "BAD")}");
            #endregion
            return crc == 0;
        }

        public override void FetchSensorData()
        {
            string op = "FetchSensorData";
            #region trace
             VantagePro.LogMessage(op, $"Start");
            #endregion
            byte[] rxBytes = GetLoopDataBytes();

            if (!CalculateCRC(rxBytes))
            {
                #region trace
                 VantagePro.LogMessage(op, $"{DataSource}: Bad CRC, packet discarded");
                #endregion
                return;
            }

            ParseSensorData(rxBytes);
            LastRead = DateTime.Now;
            #region trace
             VantagePro.LogMessage(op, $"End");
            #endregion
        }

        protected string ByteArrayToString(byte[] arr)
        {
            StringBuilder hex = new StringBuilder(arr.Length * 3);

            foreach (byte b in arr)
                hex.AppendFormat($"{b:X2} ");

            return hex.ToString();
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
            return (ushort)((bytes[o + 1] << 8) | bytes[o]);
        }

        private void ParseSensorData(byte[] buf)
        {
            string op = "ParseSensorData";

            #region trace
            VantagePro.LogMessage(op, ByteArrayToString(buf));
            #endregion

            if (buf[0] != 'L' || buf[1] != 'O' || buf[2] != 'O' || buf[4] != 0 || buf[95] != '\n' || buf[96] != '\r')
            {
                #region trace
                 VantagePro.LogMessage(op, $"Bad header [0]: {buf[0]}, [1]: {buf[1]}, [2]: {buf[2]}, [4]: {buf[4]} and/or trailer [95]: {buf[95]}, [96]: {buf[96]}");
                #endregion
                return;
            }
            #region trace
            VantagePro.LogMessage(op, "Header and trailer are valid");
            #endregion

            lock (sensorDataLock)
            {
                double F = GetUshort(buf, 12) / 10.0;
                sensorData["outsideTemp"] = util.ConvertUnits(F, Units.degreesFahrenheit, Units.degreesCelsius).ToString();
                sensorData["windSpeed"] = util.ConvertUnits(buf[14], Units.milesPerHour, Units.metresPerSecond).ToString();
                sensorData["windDir"] = GetUshort(buf, 16).ToString();

                double gust = GetUshort(buf, 22);
                sensorData["windGust"] = util.ConvertUnits(gust * 10.0, Units.milesPerHour, Units.metresPerSecond).ToString();

                double RH = buf[33];
                sensorData["outsideHumidity"] = RH.ToString();

                double P = GetUshort(buf, 7);
                sensorData["barometer"] = (util.ConvertUnits(P, Units.inHg, Units.hPa) / 1000).ToString();

                double K = util.ConvertUnits(F, Units.degreesFahrenheit, Units.degreesKelvin);
                double Td = K - ((100 - RH) / 5);
                sensorData["outsideDewPt"] = util.ConvertUnits(Td, Units.degreesKelvin, Units.degreesCelsius).ToString();

                sensorData["rainRate"] = GetUshort(buf, 41).ToString();
            }

            #region trace
            VantagePro.LogMessage(op, $"Successfully parsed sensor data (packet CRC: {GetUshort(buf, 97):X2})");
            #endregion
        }

        public abstract byte[] GetLoopDataBytes();

        public override string StationName
        {
            get
            {
                return "Unknown";
            }

            set { }
        }
    }
}
