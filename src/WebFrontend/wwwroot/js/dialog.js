// Dialog component JavaScript interop
const dialogHandlers = new Map();

export function initializeDialog(dialogId, dotNetRef) {
    const dialogElement = document.querySelector(`[data-dialog-id="${dialogId}"]`);
    const overlayElement = document.querySelector(`[data-dialog-overlay="${dialogId}"]`);
    const contentElement = document.querySelector(`[data-dialog-content="${dialogId}"]`);
    
    if (!dialogElement || !overlayElement || !contentElement) {
        return;
    }

    // Store handler
    const handler = {
        dialogElement,
        overlayElement,
        contentElement,
        dotNetRef,
        handleEscape: (e) => {
            if (e.key === 'Escape' && contentElement.getAttribute('data-state') === 'open') {
                dotNetRef.invokeMethodAsync('HandleEscape');
            }
        },
        handleOverlayClick: (e) => {
            if (e.target === overlayElement && contentElement.getAttribute('data-state') === 'open') {
                dotNetRef.invokeMethodAsync('HandleOverlayClick');
            }
        },
        trapFocus: (e) => {
            if (contentElement.getAttribute('data-state') !== 'open') {
                return;
            }

            const focusableElements = contentElement.querySelectorAll(
                'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
            );
            const firstElement = focusableElements[0];
            const lastElement = focusableElements[focusableElements.length - 1];

            if (e.key === 'Tab') {
                if (e.shiftKey) {
                    if (document.activeElement === firstElement) {
                        e.preventDefault();
                        lastElement?.focus();
                    }
                } else {
                    if (document.activeElement === lastElement) {
                        e.preventDefault();
                        firstElement?.focus();
                    }
                }
            }
        }
    };

    // Add event listeners
    document.addEventListener('keydown', handler.handleEscape);
    overlayElement.addEventListener('click', handler.handleOverlayClick);
    contentElement.addEventListener('keydown', handler.trapFocus);

    dialogHandlers.set(dialogId, handler);
}

export function openDialog(dialogId) {
    const handler = dialogHandlers.get(dialogId);
    if (!handler) return;

    const { overlayElement, contentElement } = handler;
    
    // Set state
    overlayElement.setAttribute('data-state', 'open');
    contentElement.setAttribute('data-state', 'open');
    
    // Prevent body scroll
    document.body.style.overflow = 'hidden';
    
    // Focus first element
    setTimeout(() => {
        const firstFocusable = contentElement.querySelector(
            'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
        );
        firstFocusable?.focus();
    }, 100);
}

export function closeDialog(dialogId) {
    const handler = dialogHandlers.get(dialogId);
    if (!handler) return;

    const { overlayElement, contentElement } = handler;
    
    // Set state
    overlayElement.setAttribute('data-state', 'closed');
    contentElement.setAttribute('data-state', 'closed');
    
    // Restore body scroll
    document.body.style.overflow = '';
}

export function disposeDialog(dialogId) {
    const handler = dialogHandlers.get(dialogId);
    if (!handler) return;

    // Remove event listeners
    document.removeEventListener('keydown', handler.handleEscape);
    handler.overlayElement.removeEventListener('click', handler.handleOverlayClick);
    handler.contentElement.removeEventListener('keydown', handler.trapFocus);

    // Restore body scroll
    document.body.style.overflow = '';

    dialogHandlers.delete(dialogId);
}

export function renderDialogPortal(dialogId, html) {
    // Create or get portal container
    let portalContainer = document.getElementById('blazor-dialog-portal');
    if (!portalContainer) {
        portalContainer = document.createElement('div');
        portalContainer.id = 'blazor-dialog-portal';
        document.body.appendChild(portalContainer);
    }

    // Render dialog content
    portalContainer.innerHTML = html;
    
    // Re-initialize after rendering
    setTimeout(() => {
        const dotNetRef = window[`dialog_${dialogId}_ref`];
        if (dotNetRef) {
            initializeDialog(dialogId, dotNetRef);
        }
    }, 10);
}
