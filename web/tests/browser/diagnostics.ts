import type {
  Browser,
  BrowserContext,
  Page,
  TestInfo,
  WebSocketRoute,
} from '@playwright/test';
import { appendFileSync, rmSync, writeFileSync } from 'node:fs';

/** Passive evidence only: this recorder does not decode state or satisfy waits. */
export class BrowserDiagnostics {
  private recent: object[] = [];
  private errors: string[] = [];
  private contexts = new Set<BrowserContext>();
  private restores: (() => void)[] = [];
  private timeline: string;
  private payloads: string;
  private nextSocket = 0;
  private peers = new Map<Page, string>();
  constructor(private info: TestInfo) {
    this.timeline = info.outputPath('browser-timeline.ndjson');
    this.payloads = info.outputPath('protocol-payloads.ndjson');
    writeFileSync(this.timeline, '');
    writeFileSync(this.payloads, '');
  }
  private safe(action: () => void) {
    try {
      action();
    } catch (error) {
      if (this.errors.length < 10) this.errors.push(String(error));
    }
  }
  record(kind: string, details: object = {}) {
    this.safe(() => {
      const entry = { time: new Date().toISOString(), kind, ...details };
      appendFileSync(this.timeline, JSON.stringify(entry) + '\n');
      this.recent.push(entry);
      if (this.recent.length > 200) this.recent.shift();
    });
  }
  private frame(
    peer: string,
    socket: string,
    direction: string,
    payload: string | Buffer,
  ) {
    this.safe(() => {
      const raw = typeof payload === 'string'
        ? payload
        : payload.toString('utf8');
      const bytes = typeof payload === 'string'
        ? Buffer.byteLength(payload)
        : payload.length;
      let metadata: Record<string, unknown> = {};
      try {
        const value = JSON.parse(raw), state = value.snapshot ?? value;
        for (
          const key of [
            'type',
            'requestId',
            'participantId',
            'role',
            'worldEpoch',
            'revision',
            'tick',
            'baseRevision',
            'cityHeatBaseRevision',
          ]
        ) {
          const v = value[key] ?? state[key];
          if (['string', 'number', 'boolean'].includes(typeof v)) {
            metadata[key] = v;
          }
        }
      } catch {
        metadata = { format: 'non-JSON' };
      }
      this.record('protocol', {
        peer,
        socket,
        direction,
        bytes,
        ...metadata,
      });
      appendFileSync(
        this.payloads,
        JSON.stringify({
          time: new Date().toISOString(),
          peer,
          socket,
          direction,
          encoding: typeof payload === 'string' ? 'utf8' : 'base64',
          payload: typeof payload === 'string'
            ? payload
            : payload.toString('base64'),
        }) + '\n',
      );
    });
  }
  install(browser: Browser) {
    const original = browser.newContext;
    browser.newContext = async (options) => {
      const context = await original.call(browser, options);
      try {
        await this.observeContext(context);
      } catch (error) {
        this.errors.push(String(error));
        this.info.annotations.push({
          type: 'diagnostic-capture-error',
          description: String(error),
        });
      }
      return context;
    };
    this.restores.push(() => {
      browser.newContext = original;
    });
  }
  private route(
    route: WebSocketRoute,
    peer: string,
    socket: string,
    endpoint = 'browser',
    forward?: WebSocketRoute,
  ): WebSocketRoute {
    const recorder = this;
    let server: WebSocketRoute | undefined;
    // Playwright's default route forwarding also runs in this process. Record
    // that path when a spec has not installed its own onMessage handler.
    route.onMessage((message) => {
      recorder.frame(peer, socket, `from-${endpoint}`, message);
      (forward ?? server)?.send(message);
    });
    return new Proxy(route, {
      get(target, property) {
        if (property === 'connectToServer') {
          return () => {
            server = target.connectToServer();
            return recorder.route(server, peer, socket, 'server', target);
          };
        }
        if (property === 'send') {
          return (message: string | Buffer) => {
            recorder.frame(peer, socket, `to-${endpoint}`, message);
            return target.send(message);
          };
        }
        if (property === 'onMessage') {
          return (handler: (message: string | Buffer) => void) =>
            target.onMessage((message) => {
              recorder.frame(peer, socket, `from-${endpoint}`, message);
              handler(message);
            });
        }
        if (property === 'onClose') {
          return (handler: Parameters<WebSocketRoute['onClose']>[0]) =>
            target.onClose((code, reason) => {
              recorder.record('socket-close', {
                peer,
                socket,
                endpoint,
                code,
                reason,
              });
              handler(code, reason);
            });
        }
        if (property === 'close') {
          return (options: Parameters<WebSocketRoute['close']>[0]) => {
            recorder.record('socket-close-command', {
              peer,
              socket,
              endpoint,
              ...options,
            });
            return target.close(options);
          };
        }
        const value = Reflect.get(target, property, target);
        return typeof value === 'function' ? value.bind(target) : value;
      },
    });
  }
  private observeRoutes(target: Page | BrowserContext, peer: string) {
    const original = target.routeWebSocket as Page['routeWebSocket'];
    target.routeWebSocket = (
      url: Parameters<Page['routeWebSocket']>[0],
      handler: Parameters<Page['routeWebSocket']>[1],
    ) =>
      original.call(target, url, (route) => {
        const socket = `route-${++this.nextSocket}`;
        this.record('socket-open', { peer, socket, url: route.url() });
        return handler(this.route(route, peer, socket));
      });
    this.restores.push(() => {
      target.routeWebSocket = original;
    });
  }
  private async observeContext(context: BrowserContext) {
    if (this.contexts.has(context)) return;
    this.contexts.add(context);
    const name = `context-${this.contexts.size}`;
    let pages = 0;
    this.record('context-open', { peer: name });
    // Routed sockets do not emit Playwright's native websocket events. Observe
    // browser close events without taking over the route's close forwarding.
    await context.exposeBinding(
      '__freetrainDiagnosticSocketClose',
      ({ page }, event) => {
        this.record('socket-close', {
          peer: this.peers.get(page) ?? name,
          ...event,
        });
      },
    );
    await context.addInitScript(`(() => {
      const Native = globalThis.WebSocket;
      globalThis.WebSocket = new Proxy(Native, { construct(target, args, newTarget) {
        const socket = Reflect.construct(target, args, newTarget);
        socket.addEventListener('close', event => {
          globalThis.__freetrainDiagnosticSocketClose({ url: socket.url, code: event.code, reason: event.reason, wasClean: event.wasClean }).catch(() => {});
        });
        return socket;
      } });
    })()`);
    this.observeRoutes(context, name);
    const observe = (page: Page) => {
      const peer = `${name}/page-${++pages}`;
      this.peers.set(page, peer);
      this.record('page-open', { peer });
      this.observeRoutes(page, peer);
      page.on('console', (message) => {
        if (message.type() === 'error') {
          this.record('console-error', {
            peer,
            message: message.text(),
            location: message.location(),
          });
        }
      });
      page.on(
        'pageerror',
        (error) =>
          this.record('page-error', {
            peer,
            error: error.stack ?? String(error),
          }),
      );
      page.on(
        'requestfailed',
        (request) =>
          this.record('request-failed', {
            peer,
            url: request.url(),
            method: request.method(),
            failure: request.failure(),
          }),
      );
      page.on('request', (request) => {
        if (request.method() !== 'GET') {
          this.record('http-command', {
            peer,
            method: request.method(),
            url: request.url(),
          });
        }
      });
      page.on('response', (response) => {
        if (response.status() >= 400) {
          this.record('http-error', {
            peer,
            url: response.url(),
            status: response.status(),
          });
        }
      });
      page.on('framenavigated', (frame) => {
        if (frame === page.mainFrame()) {
          this.record('navigation', { peer, url: frame.url() });
        }
      });
      page.on('close', () => this.record('page-close', { peer }));
      page.on('websocket', (ws) => {
        const socket = `native-${++this.nextSocket}`;
        this.record('socket-open', { peer, socket, url: ws.url() });
        ws.on(
          'framesent',
          ({ payload }) => this.frame(peer, socket, 'sent', payload),
        );
        ws.on(
          'framereceived',
          ({ payload }) => this.frame(peer, socket, 'received', payload),
        );
        ws.on(
          'socketerror',
          (error) => this.record('socket-error', { peer, socket, error }),
        );
        ws.on('close', () => this.record('socket-close', { peer, socket }));
      });
    };
    context.on('page', observe);
    context.on('close', () => this.record('context-close', { peer: name }));
    for (const page of context.pages()) observe(page);
  }
  async finish() {
    // Specs also create peers through browser.newPage() and sometimes leave
    // them open. Close this test's remaining contexts while Playwright's
    // artifact recorder is still active, preventing cross-test event leakage.
    const closed = await Promise.allSettled(
      [...this.contexts].map((context) => context.close()),
    );
    for (const result of closed) {
      if (result.status === 'rejected') {
        this.errors.push(String(result.reason));
      }
    }
    for (const restore of this.restores.reverse()) this.safe(restore);
    try {
      if (
        this.info.status !== this.info.expectedStatus || this.info.errors.length
      ) {
        const recent = this.info.outputPath('recent-browser-events.json');
        writeFileSync(
          recent,
          JSON.stringify(
            { events: this.recent, captureErrors: this.errors },
            null,
            2,
          ),
        );
        for (
          const [name, path, contentType] of [
            ['browser-timeline', this.timeline, 'application/x-ndjson'],
            ['protocol-payloads', this.payloads, 'application/x-ndjson'],
            ['recent-browser-events', recent, 'application/json'],
          ]
        ) await this.info.attach(name, { path, contentType });
      } else {
        rmSync(this.timeline, { force: true });
        rmSync(this.payloads, { force: true });
      }
    } catch (error) {
      // Evidence collection must never replace the test's original error.
      this.info.annotations.push({
        type: 'diagnostic-capture-error',
        description: String(error),
      });
    }
  }
}
