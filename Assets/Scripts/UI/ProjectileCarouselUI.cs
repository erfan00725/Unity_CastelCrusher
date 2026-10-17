using System.Collections;
using System.Collections.Generic;
using Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Bottom-carousel for projectile types. Keeps the selected type in the
// middle slot and slides the neighbours around it; tapping a side button
// "swaps" it into the middle.
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
    private readonly Dictionary<SO_ProjectileType, float> _previousSlotX = new Dictionary<SO_ProjectileType, float>();
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

        CapturePreviousSlotPositions();

        foreach (GameObject button in _buttons) Destroy(button);
        _buttons.Clear();

        int[] window = CarouselWindow.Window(
            selector.SelectedIndex, selector.availableTypes.Count, CurrentVisibleSideButtons);

        // The selected type sits at this slot; everything else fans out
        // around it, one sideButtonWidth of spacing per slot.
        int middle = (window.Length - 1) / 2;
        float spacing = sideButtonWidth;

        var targets = new List<ButtonTarget>();

        for (int i = 0; i < window.Length; i++)
        {
            int typeIndex = window[i];
            SO_ProjectileType type = selector.availableTypes[typeIndex];
            bool isCenter = i == middle;

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
                // typeIndex is a fresh local per iteration, so closing over it
                // captures the right slot for this button.
                uiButton.onClick.AddListener(() => selector.SelectIndex(typeIndex));
                if (spawner) uiButton.interactable = spawner.HasAmmo(type);
            }

            // Lay the button out on the horizontal line of the strip. The
            // prefab's own anchor is irrelevant here - we normalise it so
            // every button is positioned relative to the strip's centre.
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            float targetX = (i - middle) * spacing;
            float startX = targetX;

            // If this type was already on screen and its new slot is at most
            // one hop away, slide it across instead of snapping. Farther
            // jumps (wrap-around, fresh spawns) start at their target.
            if (_previousSlotX.TryGetValue(type, out float previousX) &&
                Mathf.Abs(previousX - targetX) <= spacing * 1.5f)
            {
                startX = previousX;
            }

            rect.anchoredPosition = new Vector2(startX, 0f);

            CanvasGroup group = button.GetComponent<CanvasGroup>();
            targets.Add(new ButtonTarget
            {
                rect = rect,
                group = group,
                startX = startX,
                targetX = targetX,
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

    // Records where each on-screen button is right now, so the next Rebuild
    // can animate buttons from their old slots to their new ones.
    private void CapturePreviousSlotPositions()
    {
        _previousSlotX.Clear();
        foreach (GameObject button in _buttons)
        {
            SO_ProjectileType type = FindTypeForButton(button.name);
            if (!type) continue;

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect) _previousSlotX[type] = rect.anchoredPosition.x;
        }
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
            t.rect.anchoredPosition = new Vector2(t.targetX, 0f);
            t.rect.localScale = Vector3.one * t.scale;
            if (t.group) t.group.alpha = t.alpha;
        }
    }

    private struct ButtonTarget
    {
        public RectTransform rect;
        public CanvasGroup group;
        public float startX;
        public float targetX;
        public float scale;
        public float alpha;
    }

    private IEnumerator TweenButtons(List<ButtonTarget> targets)
    {
        float[] startScale = new float[targets.Count];
        float[] startAlpha = new float[targets.Count];
        for (int i = 0; i < targets.Count; i++)
        {
            startScale[i] = targets[i].rect.localScale.x;
            startAlpha[i] = targets[i].group ? targets[i].group.alpha : 1f;
        }

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            for (int i = 0; i < targets.Count; i++)
            {
                float x = Mathf.Lerp(targets[i].startX, targets[i].targetX, eased);
                targets[i].rect.anchoredPosition = new Vector2(x, 0f);
                targets[i].rect.localScale =
                    Vector3.one * Mathf.Lerp(startScale[i], targets[i].scale, eased);
                if (targets[i].group)
                    targets[i].group.alpha = Mathf.Lerp(startAlpha[i], targets[i].alpha, eased);
            }
            yield return null;
        }

        ApplyTargets(targets);   // snap to exact final values (kills float drift)
        _tweenRoutine = null;
    }
}
