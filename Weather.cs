using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Weather
{
    public abstract class WeatherStation
    {
        public enum WeatherStationVendor { DavisInstruments, Boltwood, Stars4All };
        public enum WeatherStationModel { VantagePro2, CloudSensorII, TessW };
        public enum WeatherStationInputMethod
        {
            ClarityII,
            WeatherLink_HtmlReport,
            WeatherLink_Serial,
            WeatherLink_IP,
            TessW,
        };

        public int _unitId;
        public string _name;

        public abstract WeatherStationVendor Vendor
        {
            get;
        }

        public abstract string Model
        {
            get;
        }

        public abstract bool Enabled
        {
            get;
            set;
        }

        public abstract WeatherStationInputMethod InputMethod
        {
            get;
            set;
        }
    }
}
