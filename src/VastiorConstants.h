#ifndef VASTIOR_CONSTANTS_H
#define VASTIOR_CONSTANTS_H

// Vastior - shared tuning and safety constants.
//
// These are tuning / safety bounds, not debug switches. The mod emits no runtime
// diagnostic logging; there are deliberately no TRACE/DEBUG flags here.

namespace Vastior
{
	// --- identity / config file ---
	constexpr char kConfigFileName[] = "Vastior.ini";
	constexpr char kConfigSection[] = "Settings";

	// Config keys (descriptive, self-documenting names).
	constexpr char kKeyMinZoomDistance[] = "CameraMinimumZoomDistance";
	constexpr char kKeyMaxZoomDistance[] = "CameraMaximumZoomDistance";
	constexpr char kKeyFarClipDistance[] = "CameraFarClipDistance";
	constexpr char kKeyDepthFogClamp[] = "CameraDepthFogClamp";
	constexpr char kKeyTiltZoomThreshold[] = "CameraTiltZoomThreshold";
	constexpr char kKeyTiltAngleDegrees[] = "CameraTiltAngleDegrees";
	constexpr char kKeyTiltSmoothing[] = "CameraTiltSmoothing";

	// Config values are short numeric strings; this buffer keeps config reads on
	// the stack so the mod performs no heap allocation at all.
	constexpr int kMaxConfigValueLength = 48;

	// --- math ---
	constexpr float kPi = 3.14159265358979323846f;

	// --- camera availability polling ---
	// After loading we wait (bounded) for the game to construct its camera before
	// touching it. Bounded so a wrong / not-yet-initialised host can never spin a
	// thread forever.
	constexpr int kCameraWaitMaxAttempts = 200;
	constexpr int kCameraWaitIntervalMs = 50;          // up to 200 * 50ms = 10s

	// --- pitch tween thread ---
	constexpr int kPitchTickIntervalMs = 16;           // ~60 Hz light polling loop
	constexpr float kPitchCaptureMaxDistance = 1000.0f; // capture the native pitch only from a normal gameplay frame

	// --- plausibility guard performed before any camera field write ---
	constexpr float kCameraDistanceSanityMax = 100000.0f;

	// --- unload ---
	// On an explicit FreeLibrary we give the tween thread one tick to observe the
	// stop flag before restoring patched bytes. We never restore at process
	// termination (see DllMain), so this only runs on a deliberate unload.
	constexpr int kUnloadThreadGraceMs = 40;

	// --- byte patches ---
	// We neutralise a few engine setters by overwriting their prologue with a
	// "return immediately" instruction, so the game cannot reset values we own.
	//   x64: a bare RET (caller cleans the stack; args arrive in registers).
	//   x86: RET imm16 (callee-cleanup __thiscall) that pops the argument bytes.
	constexpr unsigned char kX64ReturnOpcode = 0xC3;
	constexpr unsigned char kX86ReturnImm16Opcode = 0xC2;
	constexpr int kOneFloatArgBytes = 4;               // one float argument
	constexpr int kFiveFloatArgBytes = 20;             // five float arguments
	constexpr int kMaxPatchBytes = 3;                  // largest patch we read/save (x86 RET imm16)

	// --- SetMovementExtents fixed arguments ---
	// The 3rd / 4th arguments are forwarded unchanged to match the call the game
	// expects. Their exact meaning is undocumented, so these known-good values are
	// preserved rather than guessed at.
	constexpr float kMovementExtentArg3 = 30.0f;
	constexpr float kMovementExtentArg4 = 50.0f;
}

#endif // VASTIOR_CONSTANTS_H
