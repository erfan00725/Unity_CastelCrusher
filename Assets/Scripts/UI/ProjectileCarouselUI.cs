using System.Collections;
using System.Collections.Generic;
using Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProjectileCarouselUI : MonoBehaviour
{
    [Header("Wiring")]
    public ProjectileSelector selector;
    public BulletSpawner spawner;
    public RectTransform contentRoot;
    public GameObject buttonPrefab;

    [Header("Adaptive size")]
    [Min(1)] public int minVisibleSideButtons = 1;
    [Min(1)] public int maxVisibleSideButtons = 3;
    public float sideButtonWidth = 100f;

    [Header("Look")]
    public float centerScale = 1f;
    [Range(0f, 1f)] public float sideScale = 0.65f;
    [Range(0f, 1f)] public float sideAlpha = 0.4f;
    public float animationDuration = 0.15f;

    public int CurrentVisibleSideButtons { get; private set; } = 2;

    private readonly List<GameObject> _buttons = new List<GameObject>();
    private Coroutine _tweenRoutine;
    private int _lastSeenWidth = -1;
    
    private void OnEnable()
    {
        if (selector) selector.OnSelectionChanged += HandleSelectionChanged;
        if (spawner) spawner.OnAmmoChanged += HandleAmmoChanged;
        UpdateVisibleSideCount();
        Rebuild(false);
    }

    private void OnDisable()
    {
        if (selector) selector.OnSelectionChanged -= HandleSelectionChanged;
        if (spawner) spawner.OnAmmoChanged -= HandleAmmoChanged;
    }
    
    private void HandleSelectionChanged(SO_ProjectileType type) => Rebuild(true);

    private void HandleAmmoChanged(SO_ProjectileType type, int remaining) => RefreshAmmoDisplays();

    private void OnRectTransformDimensionsChange()
    {
        if (!contentRoot) return;
        int width = Mathf.RoundToInt(contentRoot.rect.width);
        if (width == _lastSeenWidth) return;
        _lastSeenWidth = width;

        UpdateVisibleSideCount();
        Rebuild(false);
    }

    public void UpdateVisibleSideCount()
    {
        float available = CarouselLayoutCalculator.AvailableSideWidth(contentRoot.rect.width, sideButtonWidth);
        CurrentVisibleSideButtons = CarouselLayoutCalculator.VisibleSideButtons(
            available, sideButtonWidth, minVisibleSideButtons, maxVisibleSideButtons);
    }
    
        public void Rebuild(bool animate)
    {
        if (!selector || !buttonPrefab || !contentRoot) return;

        foreach (GameObject button in _buttons) Destroy(button);
        _buttons.Clear();

        int[] window = CarouselWindow.Window(
            selector.SelectedIndex, selector.availableTypes.Count, CurrentVisibleSideButtons);

        var targets = new List<ButtonTarget>();

        for (int i = 0; i < window.Length; i++)
        {
            int typeIndex = window[i];
            SO_ProjectileType type = selector.availableTypes[typeIndex];
            bool isCenter = typeIndex == selector.SelectedIndex;

            GameObject button = Instantiate(buttonPrefab, contentRoot);
            button.name = "ProjectileButton_" + type.displayName;

            Image icon = button.transform.Find("Icon")?.GetComponent<Image>();
            if (icon && type.icon) icon.sprite = type.icon;

            TMP_Text nameText = button.transform.Find("Name")?.GetComponent<TMP_Text>();
            if (nameText) nameText.text = type.displayName;

            TMP_Text ammoText = button.transform.Find("Ammo")?.GetComponent<TMP_Text>();
            if (ammoText && spawner) ammoText.text = spawner.GetRemaining(type).ToString();

            Button uiButton = button.GetComponent<Button>();
            if (uiButton)
            {
                // Safe to close over typeIndex directly: it is a fresh local per iteration.
                // (Only the `for` control variable `i` would be shared across iterations.)
                uiButton.onClick.AddListener(() => selector.SelectIndex(typeIndex));
                if (spawner) uiButton.interactable = spawner.HasAmmo(type);
            }

            CanvasGroup group = button.GetComponent<CanvasGroup>();
            targets.Add(new ButtonTarget
            {
                transform = button.transform,
                group = group,
                scale = isCenter ? centerScale : sideScale,
                alpha = isCenter ? 1f : sideAlpha
            });

            _buttons.Add(button);
        }

        if (_tweenRoutine != null) StopCoroutine(_tweenRoutine);
        if (animate && animationDuration > 0f && isActiveAndEnabled)
            _tweenRoutine = StartCoroutine(TweenButtons(targets));
        else
            ApplyTargets(targets);
    }
        
    // Refresh ammo counts / interactability of already-built buttons (ammo changed, no rebuild).
    private void RefreshAmmoDisplays()
    {
        if (!spawner || !selector) return;
        foreach (GameObject button in _buttons)
        {
            SO_ProjectileType type = FindTypeForButton(button.name);
            if (!type) continue;

            TMP_Text ammoText = button.transform.Find("Ammo")?.GetComponent<TMP_Text>();
            if (ammoText) ammoText.text = spawner.GetRemaining(type).ToString();

            Button uiButton = button.GetComponent<Button>();
            if (uiButton) uiButton.interactable = spawner.HasAmmo(type);
        }
    }

    private SO_ProjectileType FindTypeForButton(string buttonName)
    {
        foreach (SO_ProjectileType type in selector.availableTypes)
            if (buttonName == "ProjectileButton_" + type.displayName) return type;
        return null;
    }

    private void ApplyTargets(List<ButtonTarget> targets)
    {
        foreach (var t in targets)
        {
            t.transform.localScale = Vector3.one * t.scale;
            if (t.group) t.group.alpha = t.alpha;
        }
    }

    private struct ButtonTarget
    {
        public Transform transform;
        public CanvasGroup group;
        public float scale;
        public float alpha;
    }
    
    private IEnumerator TweenButtons(List<ButtonTarget> targets)
    {
        float[] startScale = new float[targets.Count];
        float[] startAlpha = new float[targets.Count];
        for (int i = 0; i < targets.Count; i++)
        {
            startScale[i] = targets[i].transform.localScale.x;
            startAlpha[i] = targets[i].group ? targets[i].group.alpha : 1f;
        }

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].transform.localScale =
                    Vector3.one * Mathf.Lerp(startScale[i], targets[i].scale, t);
                if (targets[i].group)
                    targets[i].group.alpha = Mathf.Lerp(startAlpha[i], targets[i].alpha, t);
            }
            yield return null;
        }

        ApplyTargets(targets);   // snap to exact final values (kills float drift)
        _tweenRoutine = null;
    }
    
}
