# ASCOM *VantagePro2* driver

This is an **ASCOM** *ObservingConditions* driver for weather stations.

It evolved from being dedicated to the _VantagePro2_ (by **Davis Systems Inc.**) stations to handling home made stations produced by amateurs.

The _VantagePro2_ weather station is usually serviced by the vendor-supplied _WeatherLink_ software, no **ASCOM** driver is supplied.
This driver bridges the gap.


## Operational modes
The driver accesses the station's data in one of three operational modes, selectable via the driver's _Setup_ window

- ### Report File
The driver periodically parses an ASCII report file (the path is specified in the _Setup_ window) and looks for the following lines:

>**outsideTemp**=25.5=<br>
>**outsideHumidity**=59=<br>
>**barometer**=1006.5=<br>
>**windSpeed**=38.6=<br>
>**windDir**=328=<br>
>**rainRate**=0.0=<br>
>**outsideDewPt**=16.9=<br>
>**utcTime**= 4:03p=<br>
>**utcDate**=06/23/19=<br>
>**StationName**=My Very Own Station=<br>

NOTES:
- The **bold** words are keywords, the must appear exactly as presented here
- The **=** (equals) characters separate the keywords from the values and also end the values.  Exactly two **=** characters are expected per each line.
- The driver first tries to parse the values using the local "***Culture***" and, if it fails, it tries to parse with "***en-US***".
- If any keywords are missing, getting the values of the respective _ObservingConditions_ properties will produce **NotImplemented** exceptions


  ### Special case - WeatheLink Report File
The _WeatherLink_ software can be set-up to produce a periodic _HTML_ report (the minimal interval is 1 minute) using a specified template (the driver provides one, named ***VantagePro.htx***, which just dumps all the station's internal data).  This operational mode allows the user to continue enjoying _WeatherLink_'s capabilities while gaining **ASCOM** compatibility.

- ### Serial-port
The driver will directly connect the station and get the relevant data (the serial-port, e.g. _**COM1**_, is supplied at _Setup_ time).

In this mode the *WeatherLink* software cannot be used, as it will no longer get access to the serial port.

- ### WeatherLinkIP
The driver will directly connect the station's IP address (settable in the _Setup_ form), on port 22222.

## Weather properties
The driver exposes the following _ObservingConditions_ properties:

* DewPoint
* Humidity
* Pressure
* RainRate
* WindSpeed 
* WindDirection
* TimeSinceLastUpdate

## Supported actions
The driver supports the following actions:

* _**`raw-data`** (no parameters)_: produces a *JSON* string containing all the raw data gathered from the weather station (lots of it :-).
* _**`OCHTag`** (no-parameters)_: produces a tag which can be used by the _**OCH**_ (*Observing Conditions Hub*) to redirect actions to this specific driver.
