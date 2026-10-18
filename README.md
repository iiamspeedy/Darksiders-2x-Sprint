# Darksiders 2x Sprint - source for review

This is the C# source for the v0.4 standalone helper distributed as
`DarksidersSprint.exe`. It provides hold-L3, 2x ordinary grounded movement in
the supported Windows build of Darksiders Warmastered Edition.

## Build requirements

- 64-bit Windows with .NET Framework 4.8 and its C# compiler.
- No NuGet packages, Visual Studio installation, internet access, game files,
  or Cheat Engine are required to compile this source.

## Build instructions

Open PowerShell in this repository's root folder and run:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /out:DarksidersSprint.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll .\Source\DarksidersSprint.cs
```

The output is `DarksidersSprint.exe` in the repository root. Alternatively,
run `Source/Build.ps1` using your normal PowerShell script policy. Compilation
does not launch the helper or attach to the game. The source was successfully
compiled with this command during preparation of this review package.

The original distributed executable's SHA256 is:

```text
8d6570f71291f77b8e8dfb23477d2c3266781fd536cdad662428d7a63ef32b29
```

This compiler workflow does not promise byte-identical rebuilds. The hash above
identifies the distributed executable, not the expected hash of every rebuild.

## Runtime behavior to review

- Opens the running `darksiders1.exe` process. Validates the executable hash,
  process architecture, and original instructions before installing the patch.
- Uses OpenProcess, ReadProcessMemory, WriteProcessMemory, VirtualAllocEx,
  VirtualProtectEx, FlushInstructionCache, and thread suspension/context APIs.
- Allocates an x86 trampoline and data in that game process. Installs a
  five-byte jump at module RVA 0x41052B and scales two horizontal movement
  components only when player, state, input and grounded checks pass.
- Polls an XInput controller for held L3 and left-stick movement every 8 ms.
- Keeps code RX and telemetry RW. Checks suspended thread instruction pointers
  while applying or restoring the patch. Retains trampolines until game exit
  to avoid freeing code that a game thread might still use.
- Restores original code when disabled or normally closed. A heartbeat timeout
  disables scaling if the helper stops updating; game restart clears the patch.
- Writes a local `DarksidersSprint.log`. Optional diagnostic command-line modes
  write the requested diagnostic output files. It reads the game's executable
  for version/hash validation; it does not edit game or save files on disk.
- Contains no networking, updater, persistence, service, driver, or registry
  functionality. This is a statement of source behavior, not an antivirus
  certification or a guarantee of moderator approval.

The process-memory and executable-code operations are relevant to the manual
security review. The quarantine's specific trigger has not been established.

## Supported game executable

Build tested: cs:2679 / master, 32-bit game on 64-bit Windows.

SHA256:

```text
bb46640ddc728b2365cf3515ce173b25648ce5df649a85707dff8b9a8df42a43
```

No game executable, PDB, saves, or other game assets are included here.

## Testing and limitations

The corrected movement path was confirmed working by the user. The 2x build
passed 32 emulated machine-code cases and a live install/restore check. The
running multiplier was read back as 2.0. Full gameplay regression coverage is
incomplete. See the notes and validation record under `Source/`.

## Sharing this source for the Nexus review

Publish these extracted files as a public source repository on GitHub or a
similar code-hosting platform. Supply its URL and the Nexus mod-page URL in
the manual review request. This local ZIP is not itself a public source URL.
No hosting account, upload, email submission, or moderator approval is included
in the preparation of this package.
