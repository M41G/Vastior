// Vastior - camera component for Grim Dawn.
//
// This DLL is auto-loaded by the winmm proxy (see VastiorProxy.cpp). All real
// work runs on a worker thread, never in DllMain. The mod only widens the
// camera zoom / draw distance and, optionally, tilts the camera slightly when
// zoomed very close. It performs no file or save access of any kind.
//
// SAFETY MODEL
//   * Every game function is resolved dynamically by its mangled export name.
//     If a required export is missing, the build is treated as unsupported and
//     the mod applies nothing - the game runs vanilla (see VastiorVersionInfo.h).
//   * Byte patches are installed once and restored once, and only on an explicit
//     FreeLibrary - never at process termination (see DllMain).
//   * No heap allocation, no exceptions, no logging.
//
// It targets Grim Dawn's own exported camera functions (resolved by name) and
// bundles no third-party code.

#include <windows.h>
#include <stdlib.h>   // strtod
#include <string.h>   // strrchr

#include "VastiorConstants.h"
#include "VastiorVersionInfo.h"
#include "VastiorCameraSettings.h"

namespace
{
	using namespace Vastior;

	// A neutralised engine setter: the bytes we overwrote and where, so the exact
	// original can be restored. address == nullptr means "not installed".
	struct BytePatch
	{
		void* address;
		unsigned char originalBytes[kMaxPatchBytes];
		int length;
	};

	// ----- module-wide state (single writer: the worker thread / DllMain) -----
	char g_configPath[MAX_PATH] = { 0 };
	CameraSettings g_settings = { 0 };
	volatile LONG g_tiltThreadRunning = 0;
	BytePatch g_farClipPatch = { nullptr, { 0 }, 0 };
	BytePatch g_pitchPatch = { nullptr, { 0 }, 0 };
	BytePatch g_movementExtentsPatch = { nullptr, { 0 }, 0 };

	// ------------------------------------------------------------ config path --
	// Resolve the absolute path to Vastior.ini next to the game. The x64 client
	// dll lives in <game>\x64 while the shared config sits in <game>, so the x64
	// build climbs one extra folder. Falls back to a relative path on any failure.
	void ResolveConfigPath(HINSTANCE moduleInstance)
	{
		lstrcpynA(g_configPath, ".\\", MAX_PATH);
		lstrcatA(g_configPath, kConfigFileName);

		char modulePath[MAX_PATH];
		const DWORD length = GetModuleFileNameA(moduleInstance, modulePath, MAX_PATH);
		if (length == 0 || length >= MAX_PATH)
		{
			return;
		}

		char* lastSlash = strrchr(modulePath, '\\');
		if (lastSlash == nullptr)
		{
			return;
		}
		*lastSlash = '\0';   // drop the dll filename -> the dll's folder

#ifdef __x86_64__
		// <game>\x64 -> <game>
		char* parentSlash = strrchr(modulePath, '\\');
		if (parentSlash != nullptr)
		{
			*parentSlash = '\0';
		}
#endif

		if (lstrlenA(modulePath) + 1 + lstrlenA(kConfigFileName) >= MAX_PATH)
		{
			return;   // keep the safe relative fallback rather than truncate
		}
		lstrcpynA(g_configPath, modulePath, MAX_PATH);
		lstrcatA(g_configPath, "\\");
		lstrcatA(g_configPath, kConfigFileName);
	}

	// ----------------------------------------------------------- config load --
	// Read one numeric key, returning the named default when the key is absent.
	// A sentinel distinguishes "missing" from a real value.
	double ReadCameraValue(const char* key, double fallback)
	{
		char buffer[kMaxConfigValueLength];
		GetPrivateProfileStringA(kConfigSection, key, "\x01", buffer, sizeof(buffer), g_configPath);
		if (buffer[0] == '\x01' && buffer[1] == '\0')
		{
			return fallback;
		}
		return strtod(buffer, nullptr);
	}

	// Load and validate the full camera configuration. Every value is clamped to
	// the named bounds in VastiorCameraSettings.h before it is stored.
	void LoadConfiguration(CameraSettings& settings)
	{
		settings.minDistance = ClampFloat((float)ReadCameraValue(kKeyMinZoomDistance, kCameraDefaultMinDistance),
			kCameraMinAllowedDistance, kCameraMaxAllowedDistance);

		settings.maxDistance = ClampFloat((float)ReadCameraValue(kKeyMaxZoomDistance, kCameraDefaultMaxDistance),
			settings.minDistance + kMinZoomSpan, kCameraMaxAllowedDistance);

		settings.farClip = ClampFloat((float)ReadCameraValue(kKeyFarClipDistance, kCameraDefaultFarClip),
			settings.maxDistance, kFarClipMax);

		settings.fogClamp = ClampFloat((float)ReadCameraValue(kKeyDepthFogClamp, kDefaultFogClamp),
			kFogClampMin, kFogClampMax);

		settings.pitchThreshold = ClampFloat((float)ReadCameraValue(kKeyTiltZoomThreshold, kDefaultPitchThreshold),
			0.0f, settings.maxDistance);

		const float tiltDegrees = ClampFloat((float)ReadCameraValue(kKeyTiltAngleDegrees, kDefaultTiltDegrees),
			kTiltMinDegrees, kTiltMaxDegrees);
		settings.tiltRadians = tiltDegrees * (kPi / 180.0f);

		settings.smooth = ClampFloat((float)ReadCameraValue(kKeyTiltSmoothing, kDefaultSmooth),
			kSmoothMin, kSmoothMax);
	}

	// ------------------------------------------------------ version detection --
	// A supported build is identified by both game modules being present and the
	// core game.dll exports resolving. The optional engine.dll setters are checked
	// at patch time, so a partial match still degrades safely.
	bool DetectSupportedVersion(HMODULE& engineModule, HMODULE& gameModule)
	{
		engineModule = GetModuleHandleA(kEngineModule);
		if (engineModule == nullptr)
		{
			return false;
		}
		gameModule = GetModuleHandleA(kGameModule);
		if (gameModule == nullptr)
		{
			return false;
		}
		if (GetProcAddress(gameModule, VASTIOR_SYM_GAME_ENGINE) == nullptr)
		{
			return false;
		}
		if (GetProcAddress(gameModule, VASTIOR_SYM_GET_CAMERA) == nullptr)
		{
			return false;
		}
		if (GetProcAddress(gameModule, VASTIOR_SYM_SET_EXTENTS) == nullptr)
		{
			return false;
		}
		return true;
	}

	// --------------------------------------------------------- camera access --
	GameCamera* GetGameCamera(HMODULE gameModule)
	{
		void** engineSlot = reinterpret_cast<void**>(GetProcAddress(gameModule, VASTIOR_SYM_GAME_ENGINE));
		if (engineSlot == nullptr || *engineSlot == nullptr)
		{
			return nullptr;
		}
		GetCameraFn getCamera = reinterpret_cast<GetCameraFn>(GetProcAddress(gameModule, VASTIOR_SYM_GET_CAMERA));
		if (getCamera == nullptr)
		{
			return nullptr;
		}
		return getCamera(*engineSlot);
	}

	// Reject null / unreadable / nonsensical camera objects before any access, so
	// a transient bad pointer during load can never fault the game.
	bool IsCameraPlausible(GameCamera* camera)
	{
		if (camera == nullptr || IsBadReadPtr(camera, sizeof(GameCamera)))
		{
			return false;
		}
		const float distance = camera->distance;
		return distance == distance /* not NaN */
			&& distance >= 0.0f
			&& distance < kCameraDistanceSanityMax;
	}

	// ---------------------------------------------------------- byte patching --
	// Overwrite an export's prologue with a "return immediately" instruction.
	// Install-once: a second call on an already-installed patch is a no-op, so the
	// saved original bytes can never be overwritten with already-patched bytes.
	bool InstallReturnPatch(HMODULE module, const char* exportName, BytePatch& patch, int x86ArgBytes)
	{
		if (patch.address != nullptr)
		{
			return true;   // already installed
		}
		if (module == nullptr)
		{
			return false;
		}

		void* target = reinterpret_cast<void*>(GetProcAddress(module, exportName));
		if (target == nullptr)
		{
			return false;
		}

#ifdef __x86_64__
		(void)x86ArgBytes;
		unsigned char patchBytes[1] = { kX64ReturnOpcode };
		const int length = 1;
#else
		unsigned char patchBytes[3] = { kX86ReturnImm16Opcode, static_cast<unsigned char>(x86ArgBytes), 0x00 };
		const int length = 3;
#endif

		const HANDLE process = GetCurrentProcess();
		if (!ReadProcessMemory(process, target, patch.originalBytes, length, nullptr))
		{
			return false;
		}
		if (!WriteProcessMemory(process, target, patchBytes, length, nullptr))
		{
			return false;
		}
		FlushInstructionCache(process, target, length);

		patch.address = target;
		patch.length = length;
		return true;
	}

	// Restore-once: writes the saved bytes back, then clears the patch so a second
	// call (e.g. a redundant unload path) does nothing.
	void RestorePatch(BytePatch& patch)
	{
		if (patch.address == nullptr || patch.length <= 0)
		{
			return;
		}
		const HANDLE process = GetCurrentProcess();
		if (WriteProcessMemory(process, patch.address, patch.originalBytes, patch.length, nullptr))
		{
			FlushInstructionCache(process, patch.address, patch.length);
		}
		patch.address = nullptr;
		patch.length = 0;
	}

	void RemoveCameraHooks()
	{
		RestorePatch(g_farClipPatch);
		RestorePatch(g_pitchPatch);
		RestorePatch(g_movementExtentsPatch);
	}

	// ----------------------------------------------------- apply the settings --
	// Set the camera's draw distance and zoom extents once, then neutralise the
	// engine setters so the game cannot revert the values we now own.
	void ApplyCameraSettings(HMODULE engineModule, HMODULE gameModule, GameCamera* camera, const CameraSettings& settings)
	{
		// Far clip: extend draw distance so distant terrain still renders when
		// zoomed out, then stop the engine resetting it every frame.
		camera->farPlane = settings.farClip;
		InstallReturnPatch(engineModule, VASTIOR_SYM_SET_FAR_PLANE, g_farClipPatch, kOneFloatArgBytes);

		// Zoom range: apply the new min/max once, then neutralise the setter.
		SetMovementExtentsFn setExtents =
			reinterpret_cast<SetMovementExtentsFn>(GetProcAddress(gameModule, VASTIOR_SYM_SET_EXTENTS));
		if (setExtents != nullptr)
		{
			setExtents(camera, settings.minDistance, settings.maxDistance,
				kMovementExtentArg3, kMovementExtentArg4, settings.fogClamp);
		}
		InstallReturnPatch(gameModule, VASTIOR_SYM_SET_EXTENTS, g_movementExtentsPatch, kFiveFloatArgBytes);
	}

	// -------------------------------------------------------- optional tilt ----
	// Light ~60 Hz loop that nudges the camera pitch down slightly when zoomed in
	// past the threshold, easing toward the target so the move is not abrupt. Only
	// started when a non-zero tilt is configured. No logging, no allocation.
	DWORD WINAPI CameraTiltThread(LPVOID parameter)
	{
		(void)parameter;
		HMODULE gameModule = GetModuleHandleA(kGameModule);
		if (gameModule == nullptr)
		{
			return 0;
		}

		bool capturedNativePitch = false;
		float nativePitch = 0.0f;

		while (InterlockedCompareExchange(&g_tiltThreadRunning, 1, 1) != 0)
		{
			GameCamera* camera = GetGameCamera(gameModule);
			if (IsCameraPlausible(camera))
			{
				const float distance = camera->distance;
				if (!capturedNativePitch)
				{
					// Learn the game's natural pitch from a normal, zoomed-out frame.
					if (distance > g_settings.pitchThreshold && distance <= kPitchCaptureMaxDistance)
					{
						nativePitch = camera->pitch;
						capturedNativePitch = true;
					}
				}
				else
				{
					const float target = nativePitch +
						((distance <= g_settings.pitchThreshold) ? g_settings.tiltRadians : 0.0f);
					camera->pitch += (target - camera->pitch) * g_settings.smooth;
				}
			}
			Sleep(kPitchTickIntervalMs);
		}
		return 0;
	}

	// ------------------------------------------------------------- worker -----
	// All initialisation happens here, off the loader lock. Each step guards and
	// returns early; on an unsupported build nothing is patched.
	DWORD WINAPI RunCameraMod(LPVOID parameter)
	{
		(void)parameter;

		LoadConfiguration(g_settings);

		HMODULE engineModule = nullptr;
		HMODULE gameModule = nullptr;
		if (!DetectSupportedVersion(engineModule, gameModule))
		{
			return 0;   // unsupported build: leave the game vanilla
		}

		GameCamera* camera = nullptr;
		for (int attempt = 0; attempt < kCameraWaitMaxAttempts; ++attempt)
		{
			camera = GetGameCamera(gameModule);
			if (IsCameraPlausible(camera))
			{
				break;
			}
			camera = nullptr;
			Sleep(kCameraWaitIntervalMs);
		}
		if (camera == nullptr)
		{
			return 0;   // camera never became available
		}

		ApplyCameraSettings(engineModule, gameModule, camera, g_settings);

		// Optional close-zoom tilt: only patch the pitch setter and start the
		// thread when the user actually configured a tilt.
		if (g_settings.tiltRadians != 0.0f &&
			InstallReturnPatch(engineModule, VASTIOR_SYM_SET_PITCH, g_pitchPatch, kOneFloatArgBytes))
		{
			InterlockedExchange(&g_tiltThreadRunning, 1);
			HANDLE tiltThread = CreateThread(nullptr, 0, CameraTiltThread, nullptr, 0, nullptr);
			if (tiltThread != nullptr)
			{
				CloseHandle(tiltThread);   // fire-and-forget; stopped via the flag
			}
			else
			{
				InterlockedExchange(&g_tiltThreadRunning, 0);
			}
		}
		return 0;
	}
}

// ------------------------------------------------------------------ DllMain --
// Minimal by design: spawn the worker on attach; signal + (only on an explicit
// unload) restore on detach. No heavy work, no waiting on threads, no risky APIs.
BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID reserved)
{
	switch (reason)
	{
		case DLL_PROCESS_ATTACH:
		{
			DisableThreadLibraryCalls(instance);
			ResolveConfigPath(instance);
			HANDLE worker = CreateThread(nullptr, 0, RunCameraMod, nullptr, 0, nullptr);
			if (worker != nullptr)
			{
				CloseHandle(worker);
			}
			break;
		}

		case DLL_PROCESS_DETACH:
		{
			InterlockedExchange(&g_tiltThreadRunning, 0);
			if (reserved != nullptr)
			{
				// Process is terminating: other threads are already gone and game
				// modules may be unmapping. Writing to game memory here is unsafe
				// and pointless, so we do nothing. (This is why patch restoration
				// is explicit rather than an RAII destructor that would run now.)
				break;
			}
			// Explicit FreeLibrary: let the tween thread observe the stop flag,
			// then restore every byte we patched.
			Sleep(Vastior::kUnloadThreadGraceMs);
			RemoveCameraHooks();
			break;
		}

		default:
			break;
	}
	return TRUE;
}
