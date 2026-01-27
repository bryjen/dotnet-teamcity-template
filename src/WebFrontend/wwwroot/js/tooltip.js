// Tooltip component JavaScript interop
const tooltipHandlers = new Map();

export function markTooltipTrigger(tooltipId) {
    // Find the tooltip container
    const tooltipContainer = document.querySelector(`[data-tooltip-id="${tooltipId}"]`);
    if (!tooltipContainer) {
        return;
    }
    
    const contentElement = document.querySelector(`[data-tooltip-content="${tooltipId}"]`);
    
    // Find the first interactive element (button, input, etc.) that's not the content
    // This will be the actual trigger element
    const allElements = tooltipContainer.querySelectorAll('button, a, input, select, textarea, [role="button"], [tabindex]');
    
    for (const element of allElements) {
        // Skip if it's the content element or already marked
        if (element === contentElement || element.closest('[data-tooltip-content]') === contentElement) {
            continue;
        }
        if (element.hasAttribute('data-tooltip-trigger')) {
            continue;
        }
        
        // Mark this element as the trigger
        element.setAttribute('data-tooltip-trigger', tooltipId);
        break; // Only mark the first one
    }
}

export function initializeTooltip(tooltipId, dotNetRef, delayDuration = 0) {
    // Find trigger element - look for element with the data attribute
    const triggerElement = document.querySelector(`[data-tooltip-trigger="${tooltipId}"]`);
    const contentElement = document.querySelector(`[data-tooltip-content="${tooltipId}"]`);
    
    if (!triggerElement || !contentElement) {
        return;
    }

    const handler = {
        triggerElement,
        contentElement,
        dotNetRef,
        showTimeout: null,
        hideTimeout: null,
        isVisible: false,
        handleTriggerMouseEnter: () => {
            clearTimeout(handler.hideTimeout);
            const side = contentElement.getAttribute('data-side') || 'top';
            const sideOffset = parseInt(contentElement.getAttribute('data-side-offset') || '0', 10);
            handler.showTimeout = setTimeout(() => {
                if (handler.isVisible === false) {
                    showTooltip(tooltipId, side, sideOffset);
                    handler.isVisible = true;
                }
            }, delayDuration);
        },
        handleTriggerMouseLeave: (e) => {
            // Always clear show timeout
            clearTimeout(handler.showTimeout);
            
            // Check if moving to tooltip content
            const relatedTarget = e.relatedTarget;
            if (relatedTarget && (relatedTarget === contentElement || contentElement.contains(relatedTarget))) {
                return; // Don't hide if moving to tooltip
            }
            
            // Always hide if tooltip is visible
            clearTimeout(handler.hideTimeout);
            handler.hideTimeout = setTimeout(() => {
                if (handler.isVisible) {
                    hideTooltip(tooltipId);
                    handler.isVisible = false;
                }
            }, 100);
        },
        handleContentMouseEnter: () => {
            clearTimeout(handler.hideTimeout);
        },
        handleContentMouseLeave: () => {
            clearTimeout(handler.showTimeout);
            clearTimeout(handler.hideTimeout);
            handler.hideTimeout = setTimeout(() => {
                hideTooltip(tooltipId);
                handler.isVisible = false;
            }, 100);
        },
        handleMouseMove: (e) => {
            // Fallback: check if mouse is outside both elements
            if (!handler.isVisible) return;
            
            const x = e.clientX;
            const y = e.clientY;
            
            const triggerRect = triggerElement.getBoundingClientRect();
            const contentRect = contentElement.getBoundingClientRect();
            
            const isOverTrigger = x >= triggerRect.left && x <= triggerRect.right &&
                                 y >= triggerRect.top && y <= triggerRect.bottom;
            const isOverContent = x >= contentRect.left && x <= contentRect.right &&
                                 y >= contentRect.top && y <= contentRect.bottom;
            
            if (!isOverTrigger && !isOverContent) {
                clearTimeout(handler.hideTimeout);
                handler.hideTimeout = setTimeout(() => {
                    hideTooltip(tooltipId);
                    handler.isVisible = false;
                }, 100);
            } else {
                clearTimeout(handler.hideTimeout);
            }
        },
        handleFocus: () => {
            clearTimeout(handler.hideTimeout);
            const side = contentElement.getAttribute('data-side') || 'top';
            const sideOffset = parseInt(contentElement.getAttribute('data-side-offset') || '0', 10);
            handler.showTimeout = setTimeout(() => {
                if (handler.isVisible === false) {
                    showTooltip(tooltipId, side, sideOffset);
                    handler.isVisible = true;
                }
            }, delayDuration);
        },
        handleBlur: () => {
            clearTimeout(handler.showTimeout);
            clearTimeout(handler.hideTimeout);
            handler.hideTimeout = setTimeout(() => {
                hideTooltip(tooltipId);
                handler.isVisible = false;
            }, 50);
        }
    };

    triggerElement.addEventListener('mouseenter', handler.handleTriggerMouseEnter);
    triggerElement.addEventListener('mouseleave', handler.handleTriggerMouseLeave);
    triggerElement.addEventListener('focus', handler.handleFocus);
    triggerElement.addEventListener('blur', handler.handleBlur);
    
    // Also listen to tooltip content mouse events
    contentElement.addEventListener('mouseenter', handler.handleContentMouseEnter);
    contentElement.addEventListener('mouseleave', handler.handleContentMouseLeave);
    
    // Add document-level mouse move as fallback to detect when mouse leaves both elements
    document.addEventListener('mousemove', handler.handleMouseMove);

    tooltipHandlers.set(tooltipId, handler);
}

export function showTooltip(tooltipId, side = 'top', sideOffset = 0) {
    const handler = tooltipHandlers.get(tooltipId);
    if (!handler) return;

    const { contentElement } = handler;
    // Make visible first
    contentElement.style.visibility = 'visible';
    contentElement.style.opacity = '1';
    contentElement.setAttribute('data-state', 'open');
    // Re-enable pointer events when showing
    contentElement.style.pointerEvents = 'auto';
    // Wait for animation classes to apply, then position
    setTimeout(() => {
        positionTooltip(tooltipId, side, sideOffset);
    }, 10);
}

export function hideTooltip(tooltipId) {
    const handler = tooltipHandlers.get(tooltipId);
    if (!handler) return;

    const { contentElement } = handler;
    const currentState = contentElement.getAttribute('data-state');
    
    // Always hide regardless of state
    contentElement.setAttribute('data-state', 'closed');
    contentElement.style.pointerEvents = 'none';
    
    // Hide visually after animation completes
    setTimeout(() => {
        if (contentElement.getAttribute('data-state') === 'closed') {
            contentElement.style.opacity = '0';
            contentElement.style.visibility = 'hidden';
        }
    }, 150);
    
    handler.isVisible = false;
}

export function positionTooltip(tooltipId, side = 'top', sideOffset = 0) {
    const handler = tooltipHandlers.get(tooltipId);
    if (!handler) return;

    const { triggerElement, contentElement } = handler;
    
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
            left = triggerRect.left + (triggerRect.width / 2) - (contentRect.width / 2);
            break;
        case 'bottom':
            top = triggerRect.bottom + sideOffset;
            left = triggerRect.left + (triggerRect.width / 2) - (contentRect.width / 2);
            break;
        case 'left':
            left = triggerRect.left - contentRect.width - sideOffset;
            top = triggerRect.top + (triggerRect.height / 2) - (contentRect.height / 2);
            break;
        case 'right':
            left = triggerRect.right + sideOffset;
            top = triggerRect.top + (triggerRect.height / 2) - (contentRect.height / 2);
            break;
        default:
            top = triggerRect.top - contentRect.height - sideOffset;
            left = triggerRect.left + (triggerRect.width / 2) - (contentRect.width / 2);
    }

    // Keep within viewport bounds
    if (left < 8) left = 8;
    if (left + contentRect.width > viewportWidth - 8) left = viewportWidth - contentRect.width - 8;
    if (top < 8) top = 8;
    if (top + contentRect.height > viewportHeight - 8) top = viewportHeight - contentRect.height - 8;

    contentElement.style.position = 'fixed';
    contentElement.style.top = `${top}px`;
    contentElement.style.left = `${left}px`;
    contentElement.style.zIndex = '50';
}

export function disposeTooltip(tooltipId) {
    const handler = tooltipHandlers.get(tooltipId);
    if (!handler) return;

    const { triggerElement, contentElement } = handler;
    triggerElement.removeEventListener('mouseenter', handler.handleTriggerMouseEnter);
    triggerElement.removeEventListener('mouseleave', handler.handleTriggerMouseLeave);
    triggerElement.removeEventListener('focus', handler.handleFocus);
    triggerElement.removeEventListener('blur', handler.handleBlur);
    
    contentElement.removeEventListener('mouseenter', handler.handleContentMouseEnter);
    contentElement.removeEventListener('mouseleave', handler.handleContentMouseLeave);
    
    document.removeEventListener('mousemove', handler.handleMouseMove);

    clearTimeout(handler.showTimeout);
    clearTimeout(handler.hideTimeout);

    tooltipHandlers.delete(tooltipId);
}
