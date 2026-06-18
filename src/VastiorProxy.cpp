// Vastior - winmm proxy loader.
//
// Built as winmm.dll and placed in the Grim Dawn folder. The game loads it in
// place of the system winmm; all real winmm calls are forwarded to a renamed
// copy of the genuine library (see the .def files) so audio/timing is unchanged.
//
// Its only job is to load the Vastior camera dll once the game engine is ready.
// It does nothing in any other process, and never touches files or saves.

#include <windows.h>
#include <stdio.h>    // snprintf
#include <string.h>   // strrchr

#include "VastiorConstants.h"
#include "VastiorVersionInfo.h"   // engine/game module names + game-engine symbol

namespace
{
	// Proxy-specific timing / identity.
	constexpr int kProxyTimeoutMs = 60000;        // give up waiting after 60s
	constexpr int kProxyPollIntervalMs = 100;
	constexpr int kEngineInitGraceMs = 750;       // let the engine finish setup
	constexpr char kHostProcessName[] = "Grim Dawn.exe";
	constexpr char kCameraDllName[] = "Vastior.dll";

	HINSTANCE g_proxyModule = nullptr;

	// Only ever act inside Grim Dawn. A same-named winmm could be loaded by an
	// unrelated process; in that case we must do nothing.
	bool HostProcessIsGrimDawn()
	{
		char exePath[MAX_PATH];
		if (GetModuleFileNameA(nullptr, exePath, MAX_PATH) == 0)
		{
			return false;
		}
		const char* slash = strrchr(exePath, '\\');
		const char* baseName = (slash != nullptr) ? slash + 1 : exePath;
		return lstrcmpiA(baseName, kHostProcessName) == 0;
	}

	// Build "<this dll's folder>\Vastior.dll". The proxy and the camera dll always
	// sit together (both in <game>, or both in <game>\x64), so the camera dll is a
	// sibling of this module.
	bool ResolveCameraDllPath(char* out, size_t capacity)
	{
		char selfPath[MAX_PATH];
		const DWORD length = GetModuleFileNameA(g_proxyModule, selfPath, MAX_PATH);
		if (length == 0 || length >= MAX_PATH)
		{
			return false;
		}
		char* slash = strrchr(selfPath, '\\');
		if (slash == nullptr)
		{
			return false;
		}
		slash[1] = '\0';   // keep the trailing backslash, drop the filename

		const int written = snprintf(out, capacity, "%s%s", selfPath, kCameraDllName);
		return written > 0 && static_cast<size_t>(written) < capacity;
	}

	// Wait (bounded) until both game modules are present and the game-engine
	// pointer is populated, which means the engine is far enough along to host us.
	bool WaitForGameEngine()
	{
		void** engineSlot = nullptr;
		for (int waited = 0; waited < kProxyTimeoutMs; waited += kProxyPollIntervalMs)
		{
			HMODULE gameModule = GetModuleHandleA(Vastior::kGameModule);
			HMODULE engineModule = GetModuleHandleA(Vastior::kEngineModule);
			if (gameModule != nullptr && engineModule != nullptr)
			{
				if (engineSlot == nullptr)
				{
					engineSlot = reinterpret_cast<void**>(GetProcAddress(gameModule, VASTIOR_SYM_GAME_ENGINE));
				}
				if (engineSlot != nullptr && *engineSlot != nullptr)
				{
					return true;
				}
			}
			Sleep(kProxyPollIntervalMs);
		}
		return false;
	}

	DWORD WINAPI LoadVastiorWhenReady(LPVOID parameter)
	{
		(void)parameter;

		if (!HostProcessIsGrimDawn())
		{
			return 0;
		}
		if (!WaitForGameEngine())
		{
			return 0;
		}
		Sleep(kEngineInitGraceMs);

		char cameraDllPath[MAX_PATH];
		if (!ResolveCameraDllPath(cameraDllPath, sizeof(cameraDllPath)))
		{
			return 0;
		}
		LoadLibraryA(cameraDllPath);   // the camera dll takes over from here
		return 0;
	}
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID reserved)
{
	(void)reserved;
	if (reason == DLL_PROCESS_ATTACH)
	{
		g_proxyModule = instance;
		DisableThreadLibraryCalls(instance);
		HANDLE worker = CreateThread(nullptr, 0, LoadVastiorWhenReady, nullptr, 0, nullptr);
		if (worker != nullptr)
		{
			CloseHandle(worker);
		}
	}
	return TRUE;
}
