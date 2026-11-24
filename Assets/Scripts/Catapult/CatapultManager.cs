using System;
using System.Collections.Generic;
using Core;
using UnityEngine;

// Level brain: finds the catapults, owns the ONE global type list, builds the ammo
// pool(s) according to SO_LevelData.ammoMode, and tracks which catapult is active.
public class CatapultManager : MonoBehaviour
{
    public SO_LevelData levelData;
    public ProjectileSelector selector;          // the ONE global selector
    public CatapultCameraRig cameraRig;          // optional in code; assign in the scene

    public event Action<Catapult> OnActiveCatapultChanged;

    public Catapult ActiveCatapult { get; private set; }
    public IReadOnlyList<Catapult> Catapults => _catapults;

    private readonly List<Catapult> _catapults = new List<Catapult>();

    private void Awake()
    {
        FindCatapults();

        if (_catapults.Count == 0)
        {
            Debug.LogError("CatapultManager: no Catapult components in the scene. Arrows and aiming are disabled.", this);
            return;
        }

        List<SO_ProjectileType> types = BuildTypeList();   // the ONE place the type list is built

        List<AmmoPool<SO_ProjectileType>> pools = BuildPools(types);
        AttachPools(pools);

        selector.Initialize(types);

        SetActive(_catapults[0]);   // first activation snaps the rig (no glide on frame one)
    }

    private void FindCatapults()
    {
        _catapults.AddRange(FindObjectsByType<Catapult>());
        for (int i = 0; i < _catapults.Count; i++)
        {
            Catapult catapult = _catapults[i];
            catapult.Index = i;
            catapult.CaptureRestPose();          // BEFORE anything reads RestPosition/RestRotation
            catapult.SetHighlight(false);
        }
    }

    // Loadout wins when non-empty; otherwise fall back to the selector's pre-authored
    // availableTypes + each type's own ammoCount — exactly what AmmoManager.Awake did pre-refactor.
    private List<SO_ProjectileType> BuildTypeList()
    {
        List<SO_ProjectileType> types = new List<SO_ProjectileType>();

        if (levelData && levelData.projectileLoadouts.Count > 0)
        {
            foreach (ProjectileLoadout entry in levelData.projectileLoadouts)
                if (entry.projectileType) types.Add(entry.projectileType);
        }
        else
        {
            types.AddRange(selector.availableTypes);
        }

        return types;
    }

    private List<AmmoPool<SO_ProjectileType>> BuildPools(List<SO_ProjectileType> types)
    {
        // The loadout numbers (or each type's own ammoCount when there is no loadout).
        int CountFor(SO_ProjectileType type)
        {
            if (levelData && levelData.projectileLoadouts.Count > 0)
            {
                foreach (ProjectileLoadout entry in levelData.projectileLoadouts)
                    if (entry.projectileType == type) return Mathf.Max(0, entry.ammoCount);
                return 0;
            }
            return Mathf.Max(0, type.ammoCount);
        }

        var pools = new List<AmmoPool<SO_ProjectileType>>();

        if (!levelData || levelData.ammoMode == AmmoMode.Shared)
        {
            var shared = new AmmoPool<SO_ProjectileType>();
            foreach (SO_ProjectileType type in types) shared.Set(type, CountFor(type));
            pools.Add(shared);                                     // ONE pool...
        }
        else
        {
            for (int i = 0; i < _catapults.Count; i++)
            {
                var pool = new AmmoPool<SO_ProjectileType>();
                foreach (SO_ProjectileType type in types) pool.Set(type, CountFor(type));
                pools.Add(pool);                                   // ...or one per catapult
            }
        }

        return pools;
    }

    private void AttachPools(List<AmmoPool<SO_ProjectileType>> pools)
    {
        for (int i = 0; i < _catapults.Count; i++)
        {
            // Shared mode: everyone indexes [0] and shares the same pool object.
            _catapults[i].ammoManager.AttachPool(pools[i % pools.Count]);
        }
    }

    public void SetActive(Catapult catapult)
    {
        if (catapult == null || catapult == ActiveCatapult) return;

        bool firstActivation = ActiveCatapult == null;

        if (ActiveCatapult)
        {
            ActiveCatapult.IsActive = false;
            ActiveCatapult.SetHighlight(false);
        }

        ActiveCatapult = catapult;
        catapult.IsActive = true;
        catapult.SetHighlight(true);

        // Only the active catapult loads a projectile on demand — inactive ones may have
        // an empty pool for this type (PerCatapult mode) and must not pre-load.
        if (catapult.launcher) catapult.launcher.LoadProjectile();

        OnActiveCatapultChanged?.Invoke(catapult);

        if (!cameraRig) return;
        if (firstActivation) cameraRig.SnapTo(catapult);   // the camera should just BE there at level start
        else cameraRig.MoveTo(catapult);
    }

    // Wrap-around is deliberate: from the last catapult, next goes to the first.
    public void NextCatapult()
    {
        if (!ActiveCatapult) return;
        SetActive(_catapults[RingIndex.Next(ActiveCatapult.Index, _catapults.Count, 1)]);
    }

    public void PreviousCatapult()
    {
        if (!ActiveCatapult) return;
        SetActive(_catapults[RingIndex.Next(ActiveCatapult.Index, _catapults.Count, -1)]);
    }
}