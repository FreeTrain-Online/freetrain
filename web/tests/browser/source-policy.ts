import ts from 'typescript';

/** Run at discovery: feature checkpoints use the shared decoder and bounded
 * comparisons; a raw transport probe documents its purpose at the route. */
export function browserSourceViolations(file: string, source: string): string[] {
  const ast = ts.createSourceFile(file, source, ts.ScriptTarget.Latest, true);
  const errors: string[] = [], lines = source.split('\n');
  const report = (node: ts.Node, message: string) => errors.push(`${file}:${ast.getLineAndCharacterOfPosition(node.getStart(ast)).line + 1}: ${message}`);
  function visit(node: ts.Node) {
    if (ts.isIdentifier(node) && ['latestCanonicalPublication', 'collectSession'].includes(node.text)) report(node, 'Use the shared session decoder.');
    if (ts.isVariableDeclaration(node) && node.name.getText(ast) === 'latest' && node.initializer && /\.filter\(|\.at\(-1\)/.test(node.initializer.getText(ast))) report(node, 'latest() must read reconstructed state from the session fixture.');
    if (ts.isIfStatement(node) && /cityHeatBaseRevision/.test(node.expression.getText(ast))) report(node, 'Local heat reconstruction is forbidden; use the shared decoder.');
    if (ts.isCallExpression(node) && ts.isPropertyAccessExpression(node.expression)) {
      if (node.expression.name.text === 'routeWebSocket') {
        const line = ast.getLineAndCharacterOfPosition(node.getStart(ast)).line;
        if (!lines.slice(Math.max(0, line - 2), line + 1).some(value => /transport-test: \S/.test(value))) report(node, 'Document the dedicated transport probe with a transport-test: reason, or use sessions.observe().');
      }
      if (['toEqual', 'toStrictEqual'].includes(node.expression.name.text)) {
        const receiver = node.expression.expression;
        if (ts.isCallExpression(receiver) && /^(expect|expect\.poll)$/.test(receiver.expression.getText(ast))) {
          const actual = receiver.arguments[0]?.getText(ast) ?? '';
          if (/\bmixed\b|\bterrain\b|\b(?:latest|snapshot|canonicalState|bootstrap|state|save|saved|bytes)\(/.test(actual)) report(node, 'Use exact bounded assertions after convergence for state/save values.');
          if (/\.(filter|find)\(/.test(actual) && /['"](?:join|snapshot|delta)['"]/.test(actual)) report(node, 'Raw publications cannot satisfy full-state equality; read the shared session checkpoint.');
        }
      }
    }
    ts.forEachChild(node, visit);
  }
  visit(ast); return errors;
}
export function validateBrowserSources(sources: Record<string, string>) {
  const errors = Object.entries(sources).flatMap(([file, source]) => browserSourceViolations(file, source));
  if (errors.length) throw new Error(errors.join('\n'));
}
