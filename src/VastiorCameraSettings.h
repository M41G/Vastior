#ifndef VASTIOR_CAMERA_SETTINGS_H
#define VASTIOR_CAMERA_SETTINGS_H

// Vastior - camera configuration model, valid ranges, and defaults.
//
// All user-tunable values are clamped to these bounds. Out-of-range config is
// clamped (never rejected with a crash), so a typo in Vastior.ini can change how
// far the camera zooms but can never destabilise the game.

namespace Vastior
{
	// --- valid ranges (named bounds, used for clamping) ---
	constexpr float kCameraMinAllowedDistance = 3.0f;
	constexpr float kCameraMaxAllowedDistance = 500.0f;
	constexpr float kFarClipMax = 2000.0f;
	constexpr float kTiltMinDegrees = -45.0f;
	constexpr float kTiltMaxDegrees = 45.0f;
	constexpr float kSmoothMin = 0.001f;
	constexpr float kSmoothMax = 1.0f;
	constexpr float kFogClampMin = 0.0f;
	constexpr float kFogClampMax = 1.0f;

	// Smallest gap enforced between min and max zoom so the range is never empty
	// or inverted (max is clamped to at least min + this).
	constexpr float kMinZoomSpan = 1.0f;

	// --- conservative defaults (used when a key is absent from Vastior.ini) ---
	constexpr float kCameraDefaultMinDistance = 7.0f;
	constexpr float kCameraDefaultMaxDistance = 60.0f;
	constexpr float kCameraDefaultFarClip = 250.0f;
	constexpr float kDefaultPitchThreshold = 7.0f;
	constexpr float kDefaultTiltDegrees = 0.0f;   // tilt disabled by default
	constexpr float kDefaultSmooth = 0.02f;
	constexpr float kDefaultFogClamp = 0.0f;

	// Validated, ready-to-apply camera settings. All fields are already clamped
	// and unit-converted; consumers never re-validate.
	struct CameraSettings
	{
		float minDistance;
		float maxDistance;
		float farClip;
		float fogClamp;
		float pitchThreshold;
		float tiltRadians;   // converted from clamped degrees
		float smooth;
	};

	inline float ClampFloat(float value, float low, float high)
	{
		if (value < low)
		{
			return low;
		}
		if (value > high)
		{
			return high;
		}
		return value;
	}
}

#endif // VASTIOR_CAMERA_SETTINGS_H
