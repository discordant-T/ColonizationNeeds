const json = (body, status = 200) => Response.json(body, { status, headers: { 'Cache-Control': 'no-store' } });
const fail = (message, status = 400) => { throw Object.assign(new Error(message), { status }); };
const hash = async text => Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text)))).map(x => x.toString(16).padStart(2, '0')).join('');
const identifier = x => typeof x === 'string' && /^[a-zA-Z0-9_-]{1,80}$/.test(x);
async function body(request) {
  const text = await request.text();
  if (text.length > 8192) fail('Request too large', 413);
  try { return JSON.parse(text); } catch { fail('Invalid JSON'); }
}
export default {
  async fetch(request, env) {
    try {
      const url = new URL(request.url);
      if (url.pathname === '/health' && request.method === 'GET') {
        if (!env.DB) return json({ ok: false, error: 'DB binding missing' }, 503);
        const row = await env.DB.prepare("SELECT count(*) AS count FROM sqlite_master WHERE type='table' AND name IN ('players','inventory','ledger')").first();
        return json({ ok: row.count === 3, service: 'ColonizationNeeds', schema: row.count === 3 ? 1 : 0 }, row.count === 3 ? 200 : 503);
      }
      const token = (request.headers.get('Authorization') || '').replace(/^Bearer /i, '');
      if (!token || token.length > 512) fail('Player access token required', 401);
      const tokenHash = await hash(token);
      // OWNER_TOKEN is a Cloudflare secret, used only for account administration.
      if (url.pathname === '/admin/players' && request.method === 'POST') {
        if (!env.OWNER_TOKEN || tokenHash !== await hash(env.OWNER_TOKEN)) fail('Owner authorization required', 403);
        const b = await body(request);
        if (!identifier(b.groupId) || typeof b.name !== 'string' || !b.name.trim() || b.name.length > 80 || !['member','admin'].includes(b.role)) fail('Invalid player details');
        const accessToken = crypto.randomUUID() + crypto.randomUUID();
        const id = crypto.randomUUID();
        await env.DB.prepare('INSERT INTO players(id,group_id,name,token_hash,role) VALUES(?,?,?,?,?)').bind(id,b.groupId,b.name.trim(),await hash(accessToken),b.role).run();
        return json({ playerId: id, groupId: b.groupId, name: b.name.trim(), role: b.role, accessToken }, 201);
      }
      const db = env.DB.withSession('first-primary');
      const player = await db.prepare('SELECT id,group_id,name,role FROM players WHERE token_hash=? AND active=1').bind(tokenHash).first();
      if (!player) fail('Invalid or disabled player token', 401);
      if (url.pathname === '/inventory' && request.method === 'GET') {
        const result = await db.batch([
          db.prepare('SELECT commodity,quantity,version FROM inventory WHERE group_id=? ORDER BY commodity').bind(player.group_id),
          db.prepare('SELECT COALESCE(MAX(sequence),0) AS cursor FROM ledger WHERE group_id=?').bind(player.group_id)
        ]);
        return json({ groupId: player.group_id, player: player.name, role: player.role, inventory: result[0].results, cursor: result[1].results[0].cursor });
      }
      if (url.pathname === '/ledger' && request.method === 'GET') {
        const after = Number(url.searchParams.get('after') || 0);
        if (!Number.isSafeInteger(after) || after < 0) fail('Invalid ledger cursor');
        const result = await db.prepare('SELECT l.*,p.name AS player FROM ledger l JOIN players p ON p.id=l.player_id WHERE l.group_id=? AND l.sequence>? ORDER BY l.sequence LIMIT 200').bind(player.group_id,after).all();
        return json({ transactions: result.results });
      }
      if (url.pathname === '/transactions' && request.method === 'POST') {
        const b = await body(request);
        if (!identifier(b.requestId) || typeof b.commodity !== 'string' || !/^[a-z0-9_]{1,80}$/.test(b.commodity) || !['add','remove','set','colonize'].includes(b.operation) || !Number.isSafeInteger(b.amount) || b.amount < 0 || b.amount > 1000000000 || !['Manual','Collect','Colonize'].includes(b.source)) fail('Invalid transaction');
        if (b.operation === 'set' && player.role !== 'admin') fail('Only an admin can reconcile stock with Set total', 403);
        if (b.operation === 'set' && (!Number.isSafeInteger(b.expectedVersion) || b.expectedVersion < 0)) fail('Set total requires the displayed inventory version');
        const existing = await db.prepare('SELECT * FROM ledger WHERE group_id=? AND request_id=?').bind(player.group_id,b.requestId).first();
        const matches = row => row.player_id === player.id && row.commodity === b.commodity && row.operation === b.operation && row.amount === b.amount && row.source === b.source;
        if (existing) { if (!matches(existing)) fail('Request ID already used for a different transaction',409); return json({ transaction: existing, duplicate: true }); }
        const expression = { add: 'quantity+?', remove: 'quantity-?', set: '?', colonize: 'MAX(0,quantity-?)' }[b.operation];
        const condition = b.operation === 'remove' ? ' AND quantity>=?' : b.operation === 'set' ? ' AND version=?' : '';
        const args = [b.requestId,player.id,b.operation,b.amount,b.source,b.amount,player.group_id,b.commodity];
        if (b.operation === 'remove') args.push(b.amount);
        if (b.operation === 'set') args.push(b.expectedVersion);
        // INSERT SELECT and the trigger commit the ledger and balance as one transaction.
        await db.batch([
          db.prepare('INSERT OR IGNORE INTO inventory(group_id,commodity) VALUES(?,?)').bind(player.group_id,b.commodity),
          db.prepare('INSERT INTO ledger(group_id,request_id,player_id,commodity,operation,amount,source,before_balance,after_balance) SELECT group_id,?,?,commodity,?,?,?,quantity,'+expression+' FROM inventory WHERE group_id=? AND commodity=?'+condition+' ON CONFLICT(group_id,request_id) DO NOTHING').bind(...args)
        ]);
        const saved = await db.prepare('SELECT * FROM ledger WHERE group_id=? AND request_id=?').bind(player.group_id,b.requestId).first();
        if (!saved) fail('Stock changed or insufficient stock. Refresh and try again with a new request ID.',409);
        if (!matches(saved)) fail('Request ID collision',409);
        return json({ transaction: saved, shortfall: b.operation === 'colonize' && saved.before_balance < b.amount });
      }
      return json({ error: 'Endpoint not found' },404);
    } catch (error) {
      return json({ error: error.status ? error.message : 'Backend unavailable. Check database setup and retry with the same request ID.' },error.status || 503);
    }
  }
};
