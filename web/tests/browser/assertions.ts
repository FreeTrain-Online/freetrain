import { test, type TestInfo } from '@playwright/test';
import { createHash } from 'node:crypto';
import { writeFile } from 'node:fs/promises';
import { inspect } from 'node:util';

export interface ComparisonContext {
  phase: string;
  peer: string;
  worldEpoch?: string;
  revision?: string;
  tick?: string;
}
interface Difference {
  path: string;
  expected: string;
  actual: string;
}
interface ArrayDifference {
  path: string;
  expectedLength: number;
  actualLength: number;
  mismatchedIndices: number;
  samples: Difference[];
}
export interface Comparison {
  differences: number;
  samples: Difference[];
  arrays: ArrayDifference[];
  arrayCount: number;
}
const sampleLimit = 5;
const missing = Symbol('missing');
function preview(value: unknown): string {
  if (value === missing) return '<missing>';
  if (Array.isArray(value)) return `Array(length=${value.length})`;
  if (value && typeof value === 'object') {
    return `Object(keys=${Object.keys(value).length})`;
  }
  const text = inspect(value, {
    colors: false,
    maxStringLength: 100,
    breakLength: Infinity,
  });
  return text.replace(/\r?\n/g, '\\n').slice(0, 140);
}
function property(path: string, key: string) {
  return /^[A-Za-z_$][\w$]*$/.test(key)
    ? `${path}.${key}`
    : `${path}[${JSON.stringify(key)}]`;
}

/** Visit every value; only the diagnostic samples are bounded. Array order is significant. */
export function compareValues(actual: unknown, expected: unknown): Comparison {
  const result: Comparison = {
    differences: 0,
    samples: [],
    arrays: [],
    arrayCount: 0,
  };
  function difference(path: string, a: unknown, e: unknown) {
    result.differences++;
    if (result.samples.length < sampleLimit) {
      result.samples.push({ path, expected: preview(e), actual: preview(a) });
    }
  }
  function visit(a: unknown, e: unknown, path: string): void {
    if (Object.is(a, e)) return;
    if (Array.isArray(a) && Array.isArray(e)) {
      const detail: ArrayDifference = {
        path,
        expectedLength: e.length,
        actualLength: a.length,
        mismatchedIndices: 0,
        samples: [],
      };
      const before = result.differences;
      // Reserve parent slots before descending so a large outer array remains visible.
      const slot = result.arrays.length < 3
        ? result.arrays.push(detail) - 1
        : -1;
      if (a.length !== e.length) {
        difference(`${path}.length`, a.length, e.length);
      }
      for (let i = 0; i < Math.max(a.length, e.length); i++) {
        const start = result.differences;
        const av = Object.hasOwn(a, i) ? a[i] : missing,
          ev = Object.hasOwn(e, i) ? e[i] : missing;
        visit(av, ev, `${path}[${i}]`);
        if (result.differences !== start) {
          detail.mismatchedIndices++;
          if (detail.samples.length < 3) {
            detail.samples.push({
              path: `${path}[${i}]`,
              expected: preview(ev),
              actual: preview(av),
            });
          }
        }
      }
      if (result.differences !== before) result.arrayCount++;
      else if (slot >= 0) result.arrays.splice(slot, 1);
      return;
    }
    if (
      a !== null && e !== null && typeof a === 'object' &&
      typeof e === 'object' && !Array.isArray(a) && !Array.isArray(e)
    ) {
      for (const key of new Set([...Object.keys(e), ...Object.keys(a)])) {
        visit(
          Object.hasOwn(a, key) ? (a as Record<string, unknown>)[key] : missing,
          Object.hasOwn(e, key) ? (e as Record<string, unknown>)[key] : missing,
          property(path, key),
        );
      }
      return;
    }
    difference(path, a, e);
  }
  visit(actual, expected, '$');
  return result;
}
export function formatComparison(result: Comparison): string {
  if (!result.differences) {
    return 'JSON values are structurally equal (bytes may differ in formatting or property order).';
  }
  return [
    `${result.differences} differing values; ${
      Math.max(0, result.differences - result.samples.length)
    } more beyond path sample.`,
    ...result.samples.map((d) =>
      `${d.path}: expected ${d.expected}; actual ${d.actual}`
    ),
    ...result.arrays.map((a) =>
      `${a.path}: expected length ${a.expectedLength}; actual length ${a.actualLength}; ${a.mismatchedIndices} mismatched indices; sample: ${
        a.samples.map((d) =>
          `${d.path} expected ${d.expected}, actual ${d.actual}`
        ).join('; ')
      }`
    ),
    ...(result.arrayCount > result.arrays.length
      ? [`${result.arrayCount - result.arrays.length} more differing arrays.`]
      : []),
  ].join('\n');
}
const ordinals = new WeakMap<TestInfo, number>();
/** Unique even when a helper is called repeatedly inside the same test. */
export function evidencePath(
  info: TestInfo,
  label: string,
  extension: string,
): string {
  const ordinal = (ordinals.get(info) ?? 0) + 1;
  ordinals.set(info, ordinal);
  return info.outputPath(
    `${String(ordinal).padStart(3, '0')}-${
      label.replace(/[^\w.-]+/g, '-').slice(0, 70)
    }.${extension}`,
  );
}
async function attachPair(
  info: TestInfo,
  label: string,
  actual: Uint8Array | string,
  expected: Uint8Array | string,
  extension: string,
  contentType: string,
) {
  const paths: string[] = [];
  for (
    const [side, value] of [['expected', expected], ['actual', actual]] as const
  ) {
    const path = evidencePath(info, `${label}-${side}`, extension);
    await writeFile(path, value);
    await info.attach(`${label}-${side}`, { path, contentType });
    paths.push(`${side}: ${path}`);
  }
  return paths.join('\n');
}
function fullValue(value: unknown) {
  // State/save values are JSON. Inspection also preserves accidental undefined properties.
  return inspect(value, {
    depth: null,
    maxArrayLength: null,
    maxStringLength: null,
    compact: false,
    colors: false,
  });
}
function metadata(value: unknown) {
  const state = value && typeof value === 'object'
    ? value as Record<string, unknown>
    : {};
  return ['worldEpoch', 'revision', 'tick'].map((key) =>
    `${key}=${preview(state[key])}`
  ).join(' ');
}
export async function assertCanonicalState(
  actual: unknown,
  expected: unknown,
  context: ComparisonContext,
  info = test.info(),
): Promise<void> {
  const comparison = compareValues(actual, expected);
  if (!comparison.differences) return;
  const evidence = await attachPair(
    info,
    'canonical-state',
    fullValue(actual),
    fullValue(expected),
    'txt',
    'text/plain',
  );
  throw new Error(
    `Canonical state mismatch: phase=${context.phase} peer=${context.peer}\nTarget ${
      metadata({
        ...expected as object,
        ...Object.fromEntries(
          Object.entries(context).filter(([, v]) => v !== undefined),
        ),
      })
    }\nActual ${metadata(actual)}\n${
      formatComparison(comparison)
    }\n${evidence}`,
  );
}
export type SaveData = string | Uint8Array;
function parseSave(value: SaveData): unknown {
  return JSON.parse(
    typeof value === 'string' ? value : Buffer.from(value).toString('utf8'),
  );
}
export async function assertJSONSave(
  actual: SaveData,
  expected: SaveData,
  context: ComparisonContext,
  info = test.info(),
): Promise<void> {
  let comparison: string;
  try {
    const result = compareValues(parseSave(actual), parseSave(expected));
    if (!result.differences) return;
    comparison = formatComparison(result);
  } catch (error) {
    comparison = `Invalid JSON save: ${String(error).slice(0, 200)}`;
  }
  const evidence = await attachPair(
    info,
    'json-save',
    actual,
    expected,
    'json',
    'application/json',
  );
  throw new Error(
    `JSON save mismatch: phase=${context.phase} peer=${context.peer}\n${comparison}\n${evidence}`,
  );
}
export async function assertSaveBytes(
  actual: Uint8Array,
  expected: Uint8Array,
  context: ComparisonContext,
  info = test.info(),
): Promise<void> {
  let offset = 0;
  while (
    offset < Math.min(actual.length, expected.length) &&
    actual[offset] === expected[offset]
  ) offset++;
  if (offset === actual.length && offset === expected.length) return;
  const hash = (value: Uint8Array) =>
    createHash('sha256').update(value).digest('hex');
  let structure: string;
  try {
    structure = formatComparison(
      compareValues(parseSave(actual), parseSave(expected)),
    );
  } catch {
    structure =
      'Structural diagnosis unavailable: at least one save is not valid JSON.';
  }
  const evidence = await attachPair(
    info,
    'save-bytes',
    actual,
    expected,
    'bin',
    'application/octet-stream',
  );
  throw new Error(
    `Exact save bytes mismatch: phase=${context.phase} peer=${context.peer}\nExpected length=${expected.length} sha256=${
      hash(expected)
    }\nActual length=${actual.length} sha256=${
      hash(actual)
    }\nFirst differing offset=${offset} expected=${
      expected[offset] ?? '<end>'
    } actual=${actual[offset] ?? '<end>'}\n${structure}\n${evidence}`,
  );
}
