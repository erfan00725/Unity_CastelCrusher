using UnityEngine;

// Per-catapult facade: one place to ask for this catapult's launcher/spawner/ammo/anchor.
// Owns no behaviour; CatapultManager drives it.
public class Catapult : MonoBehaviour
{
    public ProjectileLuncher launcher;
    public ProjectileSpawner spawner;
    public AmmoManager ammoManager;
    public CameraAnchor cameraAnchor;
    public GameObject highlightObject;   // optional visual; toggled on/off

    public int Index { get; internal set; }
    public bool IsActive { get; internal set; }

    // The authored (un-aimed) pose. The camera anchor is evaluated against this frame,
    // so the camera's destination is stable even while the body yaws during aiming.
    public Vector3 RestPosition { get; private set; }
    public Quaternion RestRotation { get; private set; }

    // Called by CatapultManager.Awake — deliberately NOT Awake() on this component.
    // See "The lifecycle-ordering trap" in the plan for why the manager must call this.
    public void CaptureRestPose()
    {
        RestPosition = transform.position;
        RestRotation = transform.rotation;
    }

    public void SetHighlight(bool on)
    {
        if (highlightObject) highlightObject.SetActive(on);
    }
}