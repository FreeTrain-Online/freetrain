// WheelEvent has no standard device type. Discrete wheels usually report lines
// or large, whole pixel steps; trackpads report smooth, often two-axis deltas.
export function createWheelClassifier() {
  let lastPan = -Infinity;
  return (event: Pick<WheelEvent, 'deltaMode' | 'deltaX' | 'deltaY' | 'ctrlKey'> & { wheelDeltaY?: number }, now: number): 'pan' | 'zoom' => {
    if (event.ctrlKey || event.deltaMode !== 0) return 'zoom';
    // Chromium/WebKit expose this extra hint, including for low-delta macOS
    // mouse wheels. Trackpad values normally scale as -3 * deltaY.
    if (event.deltaX === 0 && event.wheelDeltaY && Math.abs(event.wheelDeltaY) >= 120 && Math.abs(event.wheelDeltaY) !== Math.abs(event.deltaY) * 3) return 'zoom';
    const step = event.deltaX === 0 && Number.isInteger(event.deltaY) && Math.abs(event.deltaY) >= 50;
    // Keep fast motion and momentum in the same smooth-scroll gesture.
    if (!step || now - lastPan < 200) { lastPan = now; return 'pan'; }
    return 'zoom';
  };
}
