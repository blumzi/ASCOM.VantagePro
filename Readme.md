# ASCOM *VantagePro2* driver
This is an **ASCOM** *ObservingConditions* driver for the *VantagePro2* weather station, by Davis Instruments.

In many _Windows_ installations the _VantagePro2_ weather station is serviced by the vendor-supplied _WeatherLink_ software (no **ASCOM** driver is supplied).  While _WeatherLink_ is running the accesses to the station (usually via a serial port) is blocked.


## Operational modes
Three operational modes are selectable via the driver's _Setup_ window

- ### *WeatherLink* report

The driver capitalizes on _WeatherLink_'s capability to produce a periodic report.  The report is parsed and the 
data is presented in an **ASCOM** _ObservingConditions_ compliant manner.  This allows the user to continue 
enjoying _WeatherLink_'s capabilities while gaining **ASCOM** compatibility.

In this mode the *WeatherLink* software is set-up to periodically prepare an *HTML* report-file (minimal interval is 1 minute), based on
a user-supplied template.

The template supplied with the driver exposes all the internal data elements maintained by the weather station.

The driver periodically parses the report-file (a valid path must be provided at *Setup* time) and presents it in **ASCOM** *ObservingConditions* protocol.

- ### Serial-port
In this mode the driver will directly connect to the station and get the relevant data (the serial-port, e.g. _**COM1**_, is supplied at _Setup_ time).

In this mode the *WeatherLink* software cannot be used, as it will no longer get access to the serial port.

- ### WeatherLinkIP
In this mode the driver will connect directly to the station's IP address and port (settable in the _Setup_ form)

## Weather properties
The driver exposes the following _ObservingConditions_ properties:

* DewPoint
* Humidity
* Pressure
* RainRate
* WindSpeed 
* WindDirection
* WindGust
* TimeSinceLastUpdate

## Supported actions
The driver supports the following actions:

* _**`raw-data`** (no parameters)_: produces a *JSON* string containing all the raw data gathered from the weather station (lots of it :-).
* _**`OCHTag`** (no-parameters)_: produces a tag which can be used by the _**OCH**_ (*Observing Conditions Hub*) to redirect actions to this specific driver.
* _**`forecast`** (no parameters)_: gets the *forecast* string produced by the weather station.
