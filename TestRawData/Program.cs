using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;


namespace TestRawData
{
    class Program
    {
        static void Main(string[] _)
        {
            ASCOM.DriverAccess.ObservingConditions device = new ASCOM.DriverAccess.ObservingConditions("ASCOM.VantagePro.ObservingConditions");

            dynamic json = JsonConvert.DeserializeObject(device.Action("raw-data", ""));
            Console.WriteLine(JsonConvert.SerializeObject(json, Formatting.Indented));
            Console.ReadKey();
        }
    }
}
