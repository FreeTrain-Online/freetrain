import type { messages } from './localization.ts';
export type Tool = 'inspect' | 'build' | 'remove';
export const tools = [
  { id: 'inspect', label: 'inspectTool', icon: '↖' },
  { id: 'build', label: 'buildTool', icon: '╫' },
  { id: 'remove', label: 'removeTool', icon: '×' },
] as const satisfies readonly { id: Tool; label: keyof typeof messages.en; icon: string }[];
export const panels = {
  inspector: 'selectionInspector', catalog: 'buildings', saves: 'saves', view: 'view',
  settings: 'settings', session: 'session', help: 'help',
} as const satisfies Record<string, keyof typeof messages.en>;
export type Panel = keyof typeof panels;
