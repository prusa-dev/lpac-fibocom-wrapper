# lpac-fibocom-wrapper
This is a wrapper for the [LPAC](https://github.com/estkme-group/lpac) client that uses serial port to manage eUICC (eSIM) on Fibocom FM350.

# Usage
- Download and extract [LPAC](https://github.com/estkme-group/lpac/releases)
- Download and extract [lpac-fibocom-wrapper](https://github.com/prusa-dev/lpac-fibocom-wrapper/releases)
- Copy `lpac.exe` to lpac-fibocom-wrapper folder
- Rename `lpac.exe` to `lpac.orig.exe`
- Connect modem to PC via USB
- Set environment variable `AT_DEVICE` to modem serial port. (Ex: `SET AT_DEVICE=COM10`)
- Run `lpac-fibocom-wrapper.exe chip info`

# Usage with EasyLPAC
- Download and extract [EasyLPAC](https://github.com/creamlike1024/EasyLPAC/releases)
- Rename `lpac.exe` to `lpac.orig.exe`
- Copy `lpac-fibocom-wrapper.exe` from lpac-fibocom-wrapper to EasyLPAC
- Rename `lpac-fibocom-wrapper.exe` to `lpac.exe`
- Connect modem to PC via USB
- Execute EasyLPAC
- Select serial port in `Card Reader` menu
- Press `refresh` button

# Information
When using Fibocom FM350 select the correct slot based on type of eSIM you are using (slot 0=Physical SIM, slot 1=Embedded eSIM) 
```
AT+GTDUALSIM=1
```

For AT provisioning, the modem needs these commands to interact with the eUICC:

- `AT+CCHO` to open logical channel
- `AT+CCHC` to close logical channel
- `AT+CGLA` to use logical channel access
