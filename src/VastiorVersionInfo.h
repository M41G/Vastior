#ifndef VASTIOR_VERSION_INFO_H
#define VASTIOR_VERSION_INFO_H

// Vastior - centralised, version-specific knowledge.
//
// Everything that can change between Grim Dawn builds lives in this one header:
// the exported symbol names Vastior resolves, the camera struct layout, and the
// build the layout was validated against.
//
// VERSION SAFETY
//   Vastior never reads a raw build number at runtime. The game exe's FileVersion
//   is a static placeholder, and the real Steam build id only exists in the
//   appmanifest (outside the process). Instead, a supported build is identified
//   by the presence of the exact mangled exports below. If any required export is
//   missing, the memory layout cannot be trusted, so the mod patches nothing and
//   the game runs vanilla (see DetectSupportedVersion()).

namespace Vastior
{
	// Documentation of the build this layout was validated against.
	constexpr char kSupportedGameVersion[] = "1.2.1.6";
	constexpr char kSupportedSteamBuildId[] = "19149150";
	constexpr char kSteamAppId[] = "219990";

	// Game modules that must already be loaded before we resolve any symbol.
	constexpr char kEngineModule[] = "engine.dll";
	constexpr char kGameModule[] = "game.dll";
}

// The C++ mangled export names differ between the 32-bit and 64-bit clients, as
// does the calling convention used to invoke them.
#ifdef __x86_64__
	#define VASTIOR_SYM_GAME_ENGINE   "?gGameEngine@GAME@@3PEAVGameEngine@1@EA"
	#define VASTIOR_SYM_GET_CAMERA    "?GetCamera@GameEngine@GAME@@QEAAPEAVGameCamera@2@XZ"
	#define VASTIOR_SYM_SET_EXTENTS   "?SetMovementExtents@GameCamera@GAME@@QEAAXMMMMM@Z"
	#define VASTIOR_SYM_SET_FAR_PLANE "?SetCameraFarPlane@WorldCamera@GAME@@QEAAXM@Z"
	#define VASTIOR_SYM_SET_PITCH     "?SetCameraPitch@WorldCamera@GAME@@QEAAXM@Z"
	#define VASTIOR_THISCALL
#else
	#define VASTIOR_SYM_GAME_ENGINE   "?gGameEngine@GAME@@3PAVGameEngine@1@A"
	#define VASTIOR_SYM_GET_CAMERA    "?GetCamera@GameEngine@GAME@@QAEPAVGameCamera@2@XZ"
	#define VASTIOR_SYM_SET_EXTENTS   "?SetMovementExtents@GameCamera@GAME@@QAEXMMMMM@Z"
	#define VASTIOR_SYM_SET_FAR_PLANE "?SetCameraFarPlane@WorldCamera@GAME@@QAEXM@Z"
	#define VASTIOR_SYM_SET_PITCH     "?SetCameraPitch@WorldCamera@GAME@@QAEXM@Z"
	#define VASTIOR_THISCALL __thiscall
#endif

namespace Vastior
{
	// The camera object Vastior reads and writes. The leading vtable pointer is
	// pointer-sized, so the compiler places the float fields at the correct
	// (architecture-dependent) offset automatically - the offsets are never
	// hardcoded, which is what keeps the layout valid for both clients.
	struct GameCamera
	{
		void* vtable;
		float distance;
		float yaw;
		float pitch;
		float fieldOfView;
		float farPlane;
		float nearPlane;
	};

	// Resolved game functions, typed with the correct calling convention so the
	// hidden 'this' pointer is passed exactly as the game expects.
	typedef GameCamera* (VASTIOR_THISCALL* GetCameraFn)(void* gameEngine);
	typedef void (VASTIOR_THISCALL* SetMovementExtentsFn)(void* camera, float minDistance, float maxDistance, float arg3, float arg4, float fogClamp);
}

#endif // VASTIOR_VERSION_INFO_H
