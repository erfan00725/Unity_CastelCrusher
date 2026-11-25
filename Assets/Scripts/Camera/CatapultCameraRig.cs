using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;

// Top-level camera owner. The camera is a child at local (0,0,0), so this
// transform IS the camera's world pose. Glides between per-catapult anchors;
// straight when the anchor has < 2 path points, Catmull-Rom otherwise.
public class CatapultCameraRig : MonoBehaviour
{
    public float moveDuration = 1.2f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Range(0.1f, 20f)] public float followSharpness = 3f;   // aim-follow lag (used in Task 2.6)

    private enum RigState { Idle, Moving, Following }
    private RigState _state = RigState.Idle;

    private Coroutine _moveRoutine;
    private Catapult _followTarget;

    private void LateUpdate()
    {
        if (_state != RigState.Following || !_followTarget || !_followTarget.cameraAnchor)
            return;
    
        CameraAnchor anchor = _followTarget.cameraAnchor;
    
        // 1. "How far has the body rotated away from its rest pose", in world space.
        Quaternion bodyDelta = _followTarget.transform.rotation * Quaternion.Inverse(_followTarget.RestRotation);
    
        // 2. Where the anchor would be if it were welded to that rotation.
        Vector3 anchorRestLocal = anchor.transform.localPosition;
        Vector3 anchorRestWorld = _followTarget.RestPosition + _followTarget.RestRotation * anchorRestLocal;
        Vector3 targetPos = _followTarget.RestPosition + bodyDelta * (anchorRestWorld - _followTarget.RestPosition);
        Quaternion targetRot = bodyDelta * _followTarget.RestRotation * anchor.transform.localRotation;
    
        // 3. Move toward it at a RATE, not a ratio — frame-rate independent.
        float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, k);
        transform.position = Vector3.Lerp(transform.position, targetPos, k);
    
        // 4. Kill the drift once it is imperceptible.
        if (Quaternion.Angle(transform.rotation, targetRot) < 0.05f)
        {
            transform.SetPositionAndRotation(targetPos, targetRot);
        }
    }

    public void SnapTo(Catapult catapult)
    {
        if (catapult == null || catapult.cameraAnchor == null)
        {
            WarnNoAnchor(catapult);
            return;
        }

        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = null;

        EvaluateAnchorPose(catapult, out Vector3 pos, out Quaternion rot);
        transform.SetPositionAndRotation(pos, rot);

        _followTarget = catapult;
        _state = RigState.Following;
    }

    public void MoveTo(Catapult catapult)
    {
        if (catapult == null || catapult.cameraAnchor == null)
        {
            WarnNoAnchor(catapult);
            return;
        }

        _followTarget = catapult;

        // Capture BEFORE StopCoroutine: the new glide starts from where the camera
        // is right now, so interrupting a move never snaps back to the old start.
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        EvaluateAnchorPose(catapult, out Vector3 endPos, out Quaternion endRot);

        var controlPoints = new List<Vector3> { startPos };
        foreach (Transform waypoint in catapult.cameraAnchor.pathPoints)
        {
            if (!waypoint) continue;
            controlPoints.Add(RelativeToRestFrame(catapult, waypoint.localPosition));
        }
        controlPoints.Add(endPos);

        PolylinePath path = new PolylinePath(controlPoints);

        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(MoveRoutine(path, startRot, endRot));
    }

    private IEnumerator MoveRoutine(PolylinePath path, Quaternion startRot, Quaternion endRot)
    {
        _state = RigState.Moving;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float eased = ease.Evaluate(Mathf.Clamp01(elapsed / moveDuration));
            transform.position = path.Evaluate(eased);
            transform.rotation = Quaternion.Slerp(startRot, endRot, eased);
            yield return null;
        }

        // Snap to the exact end pose so no float drift is left behind
        // (same trick as ProjectileLuncher.ResetHeadRotationRoutine).
        transform.SetPositionAndRotation(path.Evaluate(1f), endRot);
        _moveRoutine = null;
        _state = RigState.Following;
    }

    private static void WarnNoAnchor(Catapult catapult)
    {
        if (catapult)
            Debug.LogWarning($"CatapultCameraRig: '{catapult.name}' has no CameraAnchor — camera stays put.", catapult);
    }

    // The anchor is authored as a child of the catapult root, in the catapult's REST
    // frame. RestRotation * localRotation maps it to world space frozen at that rest pose.
    // Both entry points already guarded cameraAnchor != null, so this can assume it.
    private static void EvaluateAnchorPose(Catapult catapult, out Vector3 pos, out Quaternion rot)
    {
        CameraAnchor anchor = catapult.cameraAnchor;
        pos = catapult.RestPosition + catapult.RestRotation * anchor.transform.localPosition;
        rot = catapult.RestRotation * anchor.transform.localRotation;
    }

    private static Vector3 RelativeToRestFrame(Catapult catapult, Vector3 localPosition)
    {
        return catapult.RestPosition + catapult.RestRotation * localPosition;
    }
}