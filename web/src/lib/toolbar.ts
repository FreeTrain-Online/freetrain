// One tab stop per toolbar; arrows/Home/End move between enabled, visible buttons.
// Native form editing and browser shortcuts keep their usual behavior.
export function toolbar(node: HTMLElement, orientation: 'horizontal' | 'vertical' = 'horizontal') {
  let current: HTMLButtonElement | undefined;
  const buttons = () => [...node.querySelectorAll<HTMLButtonElement>('button')]
    .filter(button => !button.disabled && button.getClientRects().length > 0);
  const sync = () => {
    const available = buttons();
    if (!current || !available.includes(current)) current = available[0];
    for (const button of node.querySelectorAll<HTMLButtonElement>('button')) button.tabIndex = button === current ? 0 : -1;
  };
  const focus = (event: FocusEvent) => {
    if (event.target instanceof HTMLButtonElement) { current = event.target; sync(); }
  };
  const key = (event: KeyboardEvent) => {
    if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey || event.isComposing || event.keyCode === 229 || !(event.target instanceof HTMLButtonElement)) return;
    const previous = orientation === 'vertical' ? 'ArrowUp' : 'ArrowLeft';
    const next = orientation === 'vertical' ? 'ArrowDown' : 'ArrowRight';
    if (![previous, next, 'Home', 'End'].includes(event.key)) return;
    const available = buttons(), index = available.indexOf(event.target);
    if (!available.length) return;
    event.preventDefault(); event.stopPropagation();
    const target=available[event.key === 'Home' ? 0 : event.key === 'End' ? available.length - 1
      : (index + (event.key === next ? 1 : -1) + available.length) % available.length];
    target.focus({ preventScroll: true });
    if(node.scrollWidth>node.clientWidth||node.scrollHeight>node.clientHeight)target.scrollIntoView({block:'nearest',inline:'nearest'});
  };
  const observer = new MutationObserver(sync);
  observer.observe(node, { childList: true, subtree: true, attributes: true, attributeFilter: ['disabled', 'hidden'] });
  node.addEventListener('focusin', focus); node.addEventListener('keydown', key);
  window.addEventListener('resize', sync); sync();
  return { update(value: 'horizontal' | 'vertical') { orientation = value; sync(); }, destroy() {
    observer.disconnect(); node.removeEventListener('focusin', focus); node.removeEventListener('keydown', key); window.removeEventListener('resize', sync);
  } };
}
