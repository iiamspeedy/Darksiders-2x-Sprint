Current build: v0.4, hold L3 for 2x ordinary grounded movement.

The supplied v0.1 scales acceleration/braking steps passed to gfc::Accelerate
without changing the target. It was not a verified sprint mod.

The first standalone v0.2 build hooked updateMovement+0x7D4, a branch normal
collision-enabled movement bypasses. Live telemetry identified this error.
The corrected hook is updateMovement+0x69B, RVA 0x41052B, on the shared path.
It replays LEA ECX,[ESP+6C]; PUSH ECX (five bytes) and continues at +0x6A0.

Only frame locals +0x4C/+0x50 (horizontal movement) are scaled, before external
forces and collision integration. Vertical movement is unchanged. With the
already pushed argument and 32 bytes saved, these locals are at hook ESP+0x70
and +0x74. XMM0, MXCSR, EAX, EDX and flags are restored; XMM1-7 and x87 are
untouched. The original LEA/PUSH and argument stack are preserved.

Guards require the exact PlayerMove state, singleton War instance, positive
movement input, grounded/non-sliding status, collision and move control.
Water, mounted/Chaos form and combat/input exclusions remain. Native LThumb
was unreliable in the observed game path; XInput supplies held L3 plus stick
movement through an 8ms polling worker.

Code/data use separate RX/RW pages. Thread contexts are checked while game
threads are suspended before patch/restore, and original protection/cache
state is restored. Trampolines remain until game exit to avoid freeing code
still in use. A five-second heartbeat expiry latches scaling off.

The corrected 1.5x build was confirmed working by the user. Position samples
measured a 1.50057 speed ratio. For v0.4, 32 machine-code cases and the live
five-byte install/restore check pass; the running multiplier was read back as
2.0. Separate 2x distance and full gameplay regression checks are incomplete.

API references:
https://learn.microsoft.com/en-us/windows/win32/api/dbghelp/nf-dbghelp-symgettypeinfo
https://learn.microsoft.com/en-us/windows/win32/api/wow64apiset/nf-wow64apiset-wow64getthreadcontext
https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-virtualprotectex
https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntddk/ns-ntddk-kuser_shared_data
https://learn.microsoft.com/en-us/windows/win32/api/xinput/ns-xinput-xinput_gamepad