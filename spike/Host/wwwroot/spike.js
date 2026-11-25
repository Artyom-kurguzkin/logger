// Host side of the MCP Apps postMessage contract. Registered at page load (before Blazor boots), messages wait for the .NET reference.
window.spike = (() => {
  let dn = null; const queue = [];
  const frame = () => document.getElementById('extframe');
  const send = msg => frame().contentWindow.postMessage(msg, '*');   // the sandboxed iframe has an opaque origin: only '*' reaches it
  async function handle(m) {
    dn.invokeMethodAsync('ViewEvent', 'view->host ' + m.method + (m.id !== undefined ? ' id=' + m.id : ''));
    if (m.method === 'ui/initialize') {
      send({ jsonrpc: '2.0', id: m.id, result: { protocolVersion: '2026-01-26', hostInfo: { name: 'spike-host', version: '0' }, hostCapabilities: { serverTools: {} }, hostContext: { theme: document.documentElement.dataset.theme || 'light' } } });
    } else if (m.method === 'tools/call') {
      try { const json = await dn.invokeMethodAsync('CallTool', m.params.name, JSON.stringify(m.params.arguments ?? {})); send({ jsonrpc: '2.0', id: m.id, result: JSON.parse(json) }); }
      catch (e) { send({ jsonrpc: '2.0', id: m.id, error: { code: -32000, message: String(e) } }); }
    }
  }
  window.addEventListener('message', e => {
    const f = frame(); if (!f || e.source !== f.contentWindow) return;   // only our iframe
    const m = e.data; if (!m || m.jsonrpc !== '2.0' || !m.method) return;
    dn ? handle(m) : queue.push(m);
  });
  return {
    init(ref) { dn = ref; while (queue.length) handle(queue.shift()); },
    setTheme(t) { document.documentElement.dataset.theme = t; send({ jsonrpc: '2.0', method: 'ui/notifications/host-context-changed', params: { theme: t } }); },
  };
})();
