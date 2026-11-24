using System.Collections.Generic;
using UnityEngine;

// Child of a catapult root. Its local transform IS the desired camera pose.
// pathPoints (optional) are children of the SAME catapult root: 2+ of them makes
// the camera glide along a Catmull-Rom path; 0–1 makes it glide straight.
public class CameraAnchor : MonoBehaviour
{
    public List<Transform> pathPoints = new List<Transform>();
}