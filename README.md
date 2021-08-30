# ASCOM *VantagePro2* driver
This is an **ASCOM** *ObservingConditions* driver for the *VantagePro2* weather station, by Davis Instruments.

The VantagePro2 station is usually serviced on a Windows system by the *WeatherLink* software, supplied by the vendor (no ASCOM driver is supplied)

The station can be connected via several hardware interfaces, but an RS232 serial port is very common.  While *WeatherLink* uses the serial port no other software can connect to the station.

This driver provides **ASCOM** compliant access to the station's weather data in one of the following two operational modes (selectable via the driver's *Setup* window)

## Operational modes
### The *WeatherLink* report-file operational mode

The *WeatherLink* software can be set-up to periodically prepare an *HTML* report-file (minimal interval is 1 minute), based on
a user-supplied template.

The template supplied with the driver exposes all the internal data elements maintained by the weather station.

The driver periodically parses the report-file (a valid path must be provided at *Setup* time) and presents it in **ASCOM** *ObservingConditions* protocol.

### The serial port operational mode
If this mode is selected at *Setup* time and a valid serial port (e.g. _**COM1**_) is supplied,
the driver will connect directly to the station and get the relevant data.

In this mode the *WeatherLink* software cannot be used, as it will no longer get access to the serial port.

## Special actions
The driver supports the following special actions:

* _**`raw-data`** (no parameters)_: produces a *JSON* string containing all the raw data gathered from the weather station (lots of it :-).
* _**`OCHTag`** (no-parameters)_: produces a tag which can be used by the _**OCH**_ (*Observing Conditions Hub*) to redirect actions to this specific driver.
* _**`forecast`** (no parameters)_: gets the *forecast* string produced by the weather station.
