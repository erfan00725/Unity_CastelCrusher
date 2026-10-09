using System;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileSelector : MonoBehaviour
{
    public List<SO_ProjectileType> availableTypes = new List<SO_ProjectileType>();

    public event Action<SO_ProjectileType> OnSelectionChanged;
    
    public int SelectedIndex { get; private set; }
    
    public SO_ProjectileType SelectedType =>
        availableTypes.Count > 0 ? availableTypes[SelectedIndex] : null;
    
    public void Initialize(List<SO_ProjectileType> types)
    {
        availableTypes = types ?? new List<SO_ProjectileType>();
        SelectedIndex = 0;
        OnSelectionChanged?.Invoke(SelectedType);
    }
    
    public void SelectIndex(int index)
    {
        if (availableTypes.Count == 0) return;
        if (index == SelectedIndex) return;

        SelectedIndex = ((index % availableTypes.Count) + availableTypes.Count) % availableTypes.Count;
        OnSelectionChanged?.Invoke(SelectedType);
    }
    
    public void SelectNext()
    {
        if (availableTypes.Count == 0) return;
        SelectIndex((SelectedIndex + 1) % availableTypes.Count);
    }
    
    public bool SelectNextWithAmmo(Func<SO_ProjectileType, bool> hasAmmo)
    {
        if (availableTypes.Count == 0) return false;

        for (int i = 1; i <= availableTypes.Count; i++)
        {
            int idx = (SelectedIndex + i) % availableTypes.Count;
            if (hasAmmo(availableTypes[idx]))
            {
                SelectIndex(idx);
                return true;
            }
        }
        return false;
    }
}