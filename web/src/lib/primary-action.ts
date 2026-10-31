function primaryButton(node: HTMLElement) {
  return [...node.querySelectorAll<HTMLButtonElement>('button.primary')].find(button =>
    button.closest('[data-primary-scope]') === node && button.getClientRects().length > 0
  );
}

function defaultReturn(event: KeyboardEvent) {
  if (event.key !== 'Enter' || event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return false;
  if (event.isComposing || event.keyCode === 229 || event.repeat) {
    event.preventDefault();
    return false;
  }
  return true;
}

/** Also used by the map, which keeps focus while picking points for a tool card. */
export function activatePrimary(event: KeyboardEvent, node: HTMLElement) {
  if (!defaultReturn(event)) return;
  const button = primaryButton(node);
  if (!button) return;
  event.preventDefault();
  event.stopPropagation();
  // Native click preserves disabled state and submit-button form validation.
  button.click();
}

/** Give a dialog or tool card a default Return action via its .primary button. */
export function primaryAction(node: HTMLElement) {
  node.dataset.primaryScope = '';
  let composing = false;
  const compositionStart = () => { composing = true; };
  const compositionEnd = () => { composing = false; };
  function key(event: KeyboardEvent) {
    if (!defaultReturn(event)) return;
    const target = event.target;
    if (!(target instanceof HTMLElement) || target.closest('[data-primary-scope]') !== node) return;
    if (composing) {
      event.preventDefault();
      return;
    }
    // A preview form's submit button yields to its ready confirmation action.
    // Before the preview is ready, Return still submits the preview form.
    const native = target.closest('button, a[href], select, textarea, input[type="file"], input[type="button"], input[type="submit"], input[type="reset"]');
    if (target.isContentEditable) return;
    if (native && !(native instanceof HTMLButtonElement && native.type === 'submit' && native.form && primaryButton(node)?.matches(':enabled'))) return;
    activatePrimary(event, node);
  }
  node.addEventListener('keydown', key);
  node.addEventListener('compositionstart', compositionStart);
  node.addEventListener('compositionend', compositionEnd);
  return { destroy() {
    node.removeEventListener('keydown', key);
    node.removeEventListener('compositionstart', compositionStart);
    node.removeEventListener('compositionend', compositionEnd);
    delete node.dataset.primaryScope;
  } };
}
