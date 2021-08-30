
# ASCOM *VantagePro2* driver
The _VantagePro2_ weather station is usually serviced by the vendor-supplied _WeatherLink_ software, no **ASCOM** driver is supplied.

This **ASCOM** *ObservingConditions* driver bridges this gap.


## Operational modes
The driver accesses the station's data in one of three operational modes, selectable via the driver's _Setup_ window

- ### *WeatherLink* report

The driver capitalizes on _WeatherLink_'s capability to produce a periodic report.  The report is parsed and the 
data is presented in an **ASCOM** _ObservingConditions_ compliant manner.  This allows the user to continue 
enjoying _WeatherLink_'s capabilities while gaining **ASCOM** compatibility.

The *WeatherLink* software is set-up to periodically (minimal interval is 1 minute) prepare an *HTML* report-file, using a template supplied by this driver's installation (VantagePro.htx) , thus  exposing the weather station's internal data elements.

The driver periodically parses the report-file (a valid path must be provided at _Setup_ time) and presents it as an **ASCOM** *ObservingConditions* object.

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
