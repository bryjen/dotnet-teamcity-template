// Dropdown click-outside detection
const clickOutsideHandlers = new Map();

export function registerClickOutside(dropdownId, dotNetRef) {
    // Remove existing handler if any
    unregisterClickOutside(dropdownId);

    const handler = (event) => {
        const dropdownElement = document.querySelector(`[data-dropdown-id="${dropdownId}"]`);
        const triggerElement = document.querySelector(`[data-dropdown-trigger="${dropdownId}"]`);

        if (!dropdownElement || !triggerElement) {
            unregisterClickOutside(dropdownId);
            return;
        }

        // Check if click is outside both dropdown and trigger
        if (!dropdownElement.contains(event.target) && !triggerElement.contains(event.target)) {
            dotNetRef.invokeMethodAsync('HandleClickOutside', dropdownId);
        }
    };

    // Use capture phase to catch clicks before they bubble
    document.addEventListener('click', handler, true);
    clickOutsideHandlers.set(dropdownId, handler);
}

export function unregisterClickOutside(dropdownId) {
    const handler = clickOutsideHandlers.get(dropdownId);
    if (handler) {
        document.removeEventListener('click', handler, true);
        clickOutsideHandlers.delete(dropdownId);
    }
}
