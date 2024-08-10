using System;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;
using ASCOM.Utilities;

namespace ASCOM.VantagePro
{
	public abstract class Fetcher
	{
		public static readonly Dictionary<byte, string> ByteToStationModel = new Dictionary<byte, string>
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

		protected const byte ACK = 0x06;

		protected static Fetcher lowerFetcher;
		protected string DriverId = VantagePro.DriverId;
		public static Dictionary<string, string> sensorData = new Dictionary<string, string>();
		protected static object sensorDataLock = new object();

		private static void OnTimer(object state)
		{
			string op = "OnTimer";

			try
			{
				#region trace
				VantagePro.tl.LogMessage(op, $"Calling lowerFetcher");
				#endregion
				lowerFetcher.FetchSensorData();
			}
			catch (Exception ex)
			{
				#region trace
				VantagePro.tl.LogMessage(op, $"Caught: {ex.Message}");
				#endregion
			}

			System.Threading.Timer timer = (System.Threading.Timer) state;
			timer.Change(Convert.ToInt32(VantagePro.interval.TotalMilliseconds), Timeout.Infinite);
		}

		private readonly System.Threading.Timer timer = new System.Threading.Timer(new TimerCallback(OnTimer));

		public void Start()
		{
			timer.Change(Convert.ToInt32(0), Timeout.Infinite);
		}

		public void Stop()
		{
			timer.Change(Timeout.Infinite, Timeout.Infinite);
		}

		public DateTime LastRead { get; set; } = DateTime.MinValue;

		public int TimeSinceLastUpdate(string _)
		{
			if (LastRead == DateTime.MinValue)
				return -1;
			return Convert.ToInt32(DateTime.Now.Subtract(LastRead).TotalSeconds);
		}

		public abstract VantagePro.DataSourceClass DataSource { get; }

		public abstract string StationModel { get; set; }

		public abstract string StationName { get; set;  }

		public static readonly CultureInfo en_US = CultureInfo.CreateSpecificCulture("en-US");
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
				string property = "Humidity", key = "outsideHumidity";
				double humidity = Double.NaN;

				lock (sensorDataLock)
				{
					if (!sensorData.ContainsKey(key) || string.IsNullOrEmpty(sensorData["outsideHumidity"]))
					{
						#region trace
						VantagePro.tl.LogMessage(property, $"NullOrEmpty: sensorData[\"{key}\"]");
						#endregion
						throw new PropertyNotImplementedException(property, false);
					}
					humidity = TryParseDouble_LocalThenEnUS(sensorData[key]);
				}

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
				string property = "Pressure", key = "barometer";
				double pressure = Double.NaN;

				lock (sensorDataLock) {
					if (!sensorData.ContainsKey(key) || string.IsNullOrEmpty(sensorData[key]))
					{
						#region trace
						VantagePro.tl.LogMessage(property, $"NullOrEmpty: sensorData[\"{property}\"]");
						#endregion
						throw new PropertyNotImplementedException(property, false);
					}
					pressure = TryParseDouble_LocalThenEnUS(sensorData[key]);
				}

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
				string property = "RainRate", key = "rainRate";
				double rainRate = Double.NaN;

				lock (sensorDataLock)
				{
					if (!sensorData.ContainsKey(key) || string.IsNullOrEmpty(sensorData[key]))
					{
						#region trace
						VantagePro.tl.LogMessage(property, $"NullOrEmpty: sensorData[\"{key}\"]");
						#endregion
						throw new PropertyNotImplementedException(property, false);
					}
					rainRate = TryParseDouble_LocalThenEnUS(sensorData[key]);
				}
				return rainRate;
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
				string property = "DewPoint", key = "outsideDewPt";
				double dewPoint = Double.NaN;

				lock (sensorDataLock)
				{
					if (!sensorData.ContainsKey(key) || string.IsNullOrEmpty(sensorData[key]))
					{
						#region trace
						VantagePro.tl.LogMessage(property, $"NullOrEmpty: sensorData[\"{key}\"]");
						#endregion
						throw new PropertyNotImplementedException(property, false);
					}
					dewPoint = TryParseDouble_LocalThenEnUS(sensorData[key]);
				}

				return dewPoint;
			}
		}


		/// <summary>
		/// Temperature at the observatory in deg C
		/// </summary>
		public double Temperature
		{
			get
			{
				string property = "Temperature", key = "outsideTemp";
				double temperature = Double.NaN;

				lock (sensorDataLock)
				{
					if (!sensorData.ContainsKey(key) || string.IsNullOrEmpty(sensorData[key]))
					{
						#region trace
						VantagePro.tl.LogMessage(property, $"NullOrEmpty: sensorData[\"{key}\"]");
						#endregion
						throw new PropertyNotImplementedException(property, false);
					}
					temperature = TryParseDouble_LocalThenEnUS(sensorData[key]);
				}
				return temperature;
			}
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
				string property = "WindDirection", key = "windDir";
				double windDirection = Double.NaN;

				if (WindSpeedMps == 0.0)
					return 0.0;
				lock (sensorDataLock)
				{
					if (!sensorData.ContainsKey(key) || string.IsNullOrEmpty(sensorData[key]))
					{
						#region trace
						VantagePro.tl.LogMessage(property, $"NullOrEmpty: sensorData[\"{key}\"]");
						#endregion
						throw new PropertyNotImplementedException(property, false);
					}
					windDirection = TryParseDouble_LocalThenEnUS(sensorData[key]);
				}

				return windDirection;
			}
		}

		/// <summary>
		/// Wind speed at the observatory in m/s
		/// </summary>
		public double WindSpeedMps
		{
			get
			{
				string property = "WindSpeedMps", key = "windSpeed";
				double windSpeed = Double.NaN;

				lock (sensorDataLock)
				{
					if (!sensorData.ContainsKey(key) || string.IsNullOrEmpty(sensorData[key]))
					{
						#region trace
						VantagePro.tl.LogMessage(property, $"NullOrEmpty: sensorData[\"{key}\"]");
						#endregion
						throw new PropertyNotImplementedException(property, false);
					}
					windSpeed = TryParseDouble_LocalThenEnUS(sensorData[key]);
				}

				return windSpeed;
			}
		}

		public double WindGust
		{
			get
			{
				string property = "WindGustMps", key = "windGust";
				double windGust = Double.NaN;

				lock (sensorDataLock)
				{
					if (!sensorData.ContainsKey(key) || string.IsNullOrEmpty(sensorData[key]))
					{
						#region trace
						VantagePro.tl.LogMessage(property, $"NullOrEmpty: sensorData[\"{key}\"]");
						#endregion
						throw new PropertyNotImplementedException(property, false);
					}
					windGust = TryParseDouble_LocalThenEnUS(sensorData[key]);
					#region trace
					VantagePro.tl.LogMessage(property, $"windGust: sensorData[\"{key}\"]: {sensorData[key]} => {windGust}");
					#endregion
				}

                return windGust;
			}
		}

		public Dictionary<string, string> RawData
		{
			get
			{
				return sensorData;
			}
		}

		public void WriteProfile()
		{
			WriteLowerProfile();
		}

		public abstract void WriteLowerProfile();
		public abstract void ReadLowerProfile();

		public abstract void FetchSensorData();
	}
}
