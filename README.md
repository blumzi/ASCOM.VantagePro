# ASCOM *VantagePro2* driver

This is a general-purpose **ASCOM** *ObservingConditions* driver for weather stations, with special support for the VantagePro stations.

It evolved from being dedicated to the _VantagePro2_ stations (by **Davis Systems Inc.**) to handling home made stations created by amateurs.

The _VantagePro2_ weather station is usually serviced by the vendor-supplied _WeatherLink_ software (Windows), no **ASCOM** driver is supplied.
This driver bridges the gap.


## Operational modes
The driver can access various data sources, selectable via the _Operational Mode_ in the driver's _Setup_ window

- ### Report File
The driver periodically parses an ASCII report file (the path is specified in the _Setup_ window) and looks for the following lines:

>**outsideTemp**=_25.5_<br>
>**outsideHumidity**=_59_<br>
>**barometer**=_1006.5_<br>
>**windSpeed**=_38.6_<br>
>**windDir**=_328_<br>
>**rainRate**=_0.0_<br>
>**outsideDewPt**=_16.9_<br>
>**utcTime**= _4:03p_<br>
>**utcDate**=_06/23/19_<br>
>**StationName**=_My Very Own Station_<br>

NOTES:
-- The **bold** words are keywords, the must appear exactly as presented above
 - The ***=*** (equals) character is a separator.  It separates the **keywords** from the _values_.  At least one ***=*** (equals) character is expected per each line.
 - White spaces are trimmed, both on the keyword and the value.
 - The driver first tries to parse the _value_ using the local "***Culture***" and, if it fails, it tries to parse it with "***en-US***".
 - If any keywords are missing, getting the _values_ of the respective _ObservingConditions_ properties will produce **PropertyNotImplemented** exceptions
 - If either the **utcDate** or **utcTime** keywords are missing, _TimeSinceLastUpdate_ will reflect the last _time-of-change_ of the file.


 - ### Special case - WeatheLink Report File
The _WeatherLink_ software can be set-up to produce a periodic _HTML_ report (the minimal interval is 1 minute) using a specified template (the driver provides one, named ***VantagePro.htx***, which just dumps all the station's internal data).  This operational mode allows the user to continue enjoying _WeatherLink_'s capabilities while gaining **ASCOM** compatibility.

- ### Serial-port
The driver will directly connect the station and get the relevant data (the serial-port, e.g. _**COM1**_, is supplied at _Setup_ time).

In this mode the *WeatherLink* software cannot be used, as it will no longer get access to the serial port.

- ### WeatherLinkIP
The driver will directly connect the station's IP address (see _Setup_ form), on port 22222.

## _ObservingConditions_ properties
The driver exposes the following [**ASCOM** _ObservingConditions_](https://ascom-standards.org/Help/Developer/html/Properties_T_ASCOM_DriverAccess_ObservingConditions.htm) properties (if the data source provides the respective keyword):

<table>
  <tr><th>Property</th><th>Keyword</th><th>Units</th></tr>
  <tr><td>DewPoint</td><td>outsideDewPt</td><td>centigrades</td>
  <tr><td>Humidity</td><td>outsideHumidity</td><td>percents</td>
  <tr><td>Pressure</td><td>barometer</td><td>hPa</td>
  <tr><td>RainRate</td><td>rainRate</td><td>zero or more :)</td>
  <tr><td>Temperature</td><td>outsideTemp</td><td>centigrades</td>
  <tr><td>WindSpeed</td><td>windSpeed</td><td>meters/second</td>
  <tr><td>WindDirection</td><td>windDir</td><td>degrees (zero when WindSpeed == 0)</td>
  <tr><td>TimeSinceLastUpdate</td><td></td><td>seconds</td>
</table>

## Supported actions
The driver supports the following actions:

* _**`raw-data`** (no parameters)_: produces a *JSON* string containing all the raw data gathered from the weather station.
* _**`OCHTag`** (no-parameters)_: produces a tag which can be used by the _**ASCOM OCH**_ (*Observing Conditions Hub*) to redirect actions to this specific driver.
