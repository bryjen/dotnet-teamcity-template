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

// Dropdown menu positioning
export function positionDropdownMenu(dropdownId, side = 'bottom', align = 'start', sideOffset = 4) {
    const triggerElement = document.querySelector(`[data-dropdown-menu-trigger="${dropdownId}"]`);
    const contentElement = document.querySelector(`[data-dropdown-menu-content="${dropdownId}"]`);
    
    if (!triggerElement || !contentElement) {
        return;
    }

    const triggerRect = triggerElement.getBoundingClientRect();
    const contentRect = contentElement.getBoundingClientRect();
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    let top = 0;
    let left = 0;

    // Calculate position based on side
    switch (side) {
        case 'top':
            top = triggerRect.top - contentRect.height - sideOffset;
            break;
        case 'bottom':
            top = triggerRect.bottom + sideOffset;
            break;
        case 'left':
            left = triggerRect.left - contentRect.width - sideOffset;
            top = triggerRect.top;
            break;
        case 'right':
            left = triggerRect.right + sideOffset;
            top = triggerRect.top;
            break;
        default:
            top = triggerRect.bottom + sideOffset;
    }

    // Calculate alignment
    if (side === 'top' || side === 'bottom') {
        switch (align) {
            case 'start':
                left = triggerRect.left;
                break;
            case 'center':
                left = triggerRect.left + (triggerRect.width / 2) - (contentRect.width / 2);
                break;
            case 'end':
                left = triggerRect.right - contentRect.width;
                break;
        }
    } else {
        switch (align) {
            case 'start':
                top = triggerRect.top;
                break;
            case 'center':
                top = triggerRect.top + (triggerRect.height / 2) - (contentRect.height / 2);
                break;
            case 'end':
                top = triggerRect.bottom - contentRect.height;
                break;
        }
    }

    // Keep within viewport bounds
    if (left < 0) left = 8;
    if (left + contentRect.width > viewportWidth) left = viewportWidth - contentRect.width - 8;
    if (top < 0) top = 8;
    if (top + contentRect.height > viewportHeight) top = viewportHeight - contentRect.height - 8;

    contentElement.style.position = 'fixed';
    contentElement.style.top = `${top}px`;
    contentElement.style.left = `${left}px`;
    contentElement.style.zIndex = '50';
}
