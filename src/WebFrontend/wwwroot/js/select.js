export function chooseSelectDirection(triggerElement) {
    if (!triggerElement) {
        return "down";
    }

    const rect = triggerElement.getBoundingClientRect();
    const viewportHeight = window.innerHeight || document.documentElement.clientHeight || 0;

    const spaceAbove = rect.top;
    const spaceBelow = viewportHeight - rect.bottom;

    // Heuristic: estimated dropdown height in pixels. If there isn't enough
    // room below but there is more room above, prefer opening upwards.
    const ESTIMATED_DROPDOWN_HEIGHT = 260;

    if (spaceBelow < ESTIMATED_DROPDOWN_HEIGHT && spaceAbove > spaceBelow) {
        return "up";
    }

    return "down";
}

