const json = (body, status = 200) => Response.json(body, { status, headers: { 'Cache-Control': 'no-store' } });
const fail = (message, status = 400) => { throw Object.assign(new Error(message), { status }); };
const hash = async text => Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text)))).map(x => x.toString(16).padStart(2, '0')).join('');
const identifier = x => typeof x === 'string' && /^[a-zA-Z0-9_-]{1,80}$/.test(x);
async function body(request) {
  const text = await request.text();
  if (text.length > 8192) fail('Request too large', 413);
  try { return JSON.parse(text); } catch { fail('Invalid JSON'); }
}
// Fold per-player lots so one player's delivery cannot clear another player's cargo.
function transitBalances(events) {
  let lots=[];
  const priority=(lot,preferred)=>{
    const inbound=lot.carrier_id.startsWith('inbound:');
    if(!preferred)return inbound?1:0;
    if(inbound&&(lot.carrier_id==='inbound:'+preferred||lot.carrier_id==='inbound:any'))return 0;
    if(lot.carrier_id===preferred)return 1;
    return inbound?2:3;
  };
  const consume=(e,commodity,amount,preferred='',flow=null)=>{
    const matches=lots.filter(x=>x.player_id===e.player_id&&x.commodity===commodity&&(flow===null||x.carrier_id.startsWith('inbound:')===(flow==='inbound'))).sort((a,b)=>priority(a,preferred)-priority(b,preferred));
    for(const lot of matches){const n=Math.min(lot.quantity,amount);lot.quantity-=n;amount-=n;if(!amount)break;}
  };
  for(const e of events) {
    if(e.kind==='withdraw')lots.push({player_id:e.player_id,carrier_id:e.carrier_id,commodity:e.commodity,quantity:e.amount});
    else if(e.kind==='consume')consume(e,e.commodity,e.amount,e.carrier_id);
    else if(e.kind==='reset') {for(const lot of lots)if(lot.player_id===e.player_id)lot.quantity=0;}
    else if(e.kind==='cap'||e.kind==='snapshot') {
      const cargo=e.kind==='snapshot'?JSON.parse(e.cargo):{[e.commodity]:e.amount};
      const keys=e.kind==='snapshot'?[...new Set(lots.filter(x=>x.player_id===e.player_id).map(x=>x.commodity))]:[e.commodity];
      for(const key of keys){const flow=e.kind==='cap'?(e.carrier_id==='inbound'?'inbound':'outbound'):null;const n=lots.filter(x=>x.player_id===e.player_id&&x.commodity===key&&(flow===null||x.carrier_id.startsWith('inbound:')===(flow==='inbound'))).reduce((a,x)=>a+x.quantity,0);consume(e,key,Math.max(0,n-(cargo[key]||0)),'',flow);}
    }
    lots=lots.filter(x=>x.quantity>0);
  }
  const totals=new Map();for(const lot of lots)if(lot.quantity>0){const key=lot.carrier_id+'|'+lot.commodity;const row=totals.get(key)||{carrier_id:lot.carrier_id,commodity:lot.commodity,quantity:0};row.quantity+=lot.quantity;totals.set(key,row);}
  return {transit:[...totals.values()].filter(x=>!x.carrier_id.startsWith('inbound:')),inbound:[...totals.values()].filter(x=>x.carrier_id.startsWith('inbound:')).map(x=>({...x,carrier_id:x.carrier_id.slice(8)}))};
}
export default {
  async fetch(request, env) {
    try {
      const url = new URL(request.url);
      if (url.pathname === '/health' && request.method === 'GET') {
        if (!env.DB) return json({ ok: false, error: 'DB binding missing' }, 503);
        const row = await env.DB.prepare("SELECT count(*) AS count FROM sqlite_master WHERE type='table' AND name IN ('players','inventory','ledger','carrier_groups','carrier_inventory','carrier_ledger','carrier_registry','carrier_registry_versions')").first();
        return json({ ok: row.count === 8, service: 'ColonizationNeeds', schema: row.count === 8 ? 2 : row.count === 3 ? 1 : 0, inbound:true, transit: !!(await env.DB.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name='transit_events'").first()) }, row.count === 8 ? 200 : 503);
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


      if(url.pathname === '/v2/carriers' && request.method === 'GET') {
        const registry=await db.prepare('SELECT carrier_id AS id,name,role,callsign,last_seen AS lastSeen,verified FROM carrier_registry WHERE group_id=? ORDER BY role DESC').bind(player.group_id).all();
        const meta=await db.prepare('SELECT version FROM carrier_registry_versions WHERE group_id=?').bind(player.group_id).first();
        return json({groupId:player.group_id,role:player.role,carriers:registry.results.map(x=>({...x,verified:!!x.verified})),version:meta?.version||0});
      }
      if(url.pathname === '/v2/carriers' && request.method === 'POST') {
        if(player.role!=='admin') fail('Only a shared group admin can configure carriers',403);
        const b=await body(request);
        if(!identifier(b.requestId)||!Number.isSafeInteger(b.expectedVersion)||b.expectedVersion<0||!Array.isArray(b.carriers)||b.carriers.length>2) fail('Invalid carrier configuration');
        const carriers=b.carriers.map(c=>{
          if(!c||typeof c.id!=='string'||!/^[1-9][0-9]{0,19}$/.test(c.id)||typeof c.name!=='string'||!c.name.trim()||c.name.length>120||!['Squadron','Personal'].includes(c.role)) fail('Choose valid carrier IDs, names and roles');
          const callsign=c.callsign||'',lastSeen=c.lastSeen||'';
          if(typeof callsign!=='string'||callsign.length>40||typeof lastSeen!=='string'||lastSeen.length>40|| (lastSeen&&!Number.isFinite(Date.parse(lastSeen)))) fail('Invalid discovery metadata');
          return {id:c.id,name:c.name.trim(),role:c.role,callsign,lastSeen,verified:c.verified===true};
        }).sort((a,b)=>a.role.localeCompare(b.role));
        if(new Set(carriers.map(c=>c.id)).size!==carriers.length||new Set(carriers.map(c=>c.role)).size!==carriers.length) fail('Each carrier and role must be unique');
        const payloadHash=await hash(JSON.stringify(carriers));
        const before=await db.prepare('SELECT * FROM carrier_registry_versions WHERE group_id=?').bind(player.group_id).first();
        if(before?.request_id===b.requestId) {if(before.payload_hash!==payloadHash) fail('Configuration request ID collision',409);return json({version:before.version,duplicate:true});}
        const ids=carriers.map(c=>c.id); while(ids.length<2)ids.push('');
        // CAS and the in-batch usage check prevent removing an ID that gained stock concurrently.
        const safe="NOT EXISTS(SELECT 1 FROM carrier_registry r WHERE r.group_id=carrier_registry_versions.group_id AND r.carrier_id NOT IN (?,?) AND (EXISTS(SELECT 1 FROM carrier_inventory i WHERE i.group_id=r.group_id AND i.carrier_id=r.carrier_id AND i.quantity<>0) OR EXISTS(SELECT 1 FROM carrier_ledger l WHERE l.group_id=r.group_id AND l.carrier_id=r.carrier_id)))";
        const gate='EXISTS(SELECT 1 FROM carrier_registry_versions v WHERE v.group_id=? AND v.request_id=? AND v.payload_hash=?)';
        const statements=[
          db.prepare('INSERT OR IGNORE INTO carrier_registry_versions(group_id) VALUES(?)').bind(player.group_id),
          db.prepare('UPDATE carrier_registry_versions SET version=version+1,request_id=?,payload_hash=? WHERE group_id=? AND version=? AND '+safe).bind(b.requestId,payloadHash,player.group_id,b.expectedVersion,...ids),
          db.prepare('DELETE FROM carrier_registry WHERE group_id=? AND '+gate).bind(player.group_id,player.group_id,b.requestId,payloadHash),
          ...carriers.map(c=>db.prepare('INSERT INTO carrier_registry(group_id,carrier_id,name,role,callsign,last_seen,verified) SELECT ?,?,?,?,?,?,? WHERE '+gate).bind(player.group_id,c.id,c.name,c.role,c.callsign,c.lastSeen,c.verified?1:0,player.group_id,b.requestId,payloadHash))
        ];
        await db.batch(statements);
        const saved=await db.prepare('SELECT * FROM carrier_registry_versions WHERE group_id=?').bind(player.group_id).first();
        if(saved.request_id!==b.requestId||saved.payload_hash!==payloadHash) fail('Configuration changed, or a removed carrier has stock/history. Refresh and keep those carrier IDs.',409);
        return json({version:saved.version});
      }

      if(url.pathname === '/v2/transit' && request.method === 'POST') {
        const b=await body(request),e=b.transit;
        if(!identifier(b.requestId)||!e||e.id!==b.requestId||!['inbound','consume','cap','snapshot','reset'].includes(e.kind)||typeof e.timestamp!=='string'||!/^\d{4}-\d\d-\d\dT/.test(e.timestamp)||!Number.isFinite(Date.parse(e.timestamp)))fail('Invalid transit event');
        const commodity=e.commodity||'',amount=e.amount||0;
        if(['inbound','consume','cap'].includes(e.kind)&&!/^[a-z0-9_]{1,80}$/.test(commodity))fail('Invalid transit commodity');
        if(!Number.isSafeInteger(amount)||amount<0||amount>1000000000)fail('Invalid transit quantity');
        const cargo={};if(e.kind==='snapshot') {
          if(!e.cargo||typeof e.cargo!=='object'||Array.isArray(e.cargo)||Object.keys(e.cargo).length>500)fail('Invalid cargo snapshot');
          for(const key of Object.keys(e.cargo).sort()){const n=e.cargo[key];if(!/^[a-z0-9_]{1,80}$/.test(key)||!Number.isSafeInteger(n)||n<0||n>1000000000)fail('Invalid cargo snapshot');cargo[key]=n;}
        }
        let carrier='',kind=e.kind;
        if(e.kind==='inbound') {
          if(e.carrier!=='any' && (typeof e.carrier!=='string'||!/^[1-9][0-9]{0,19}$/.test(e.carrier)||!await db.prepare('SELECT carrier_id FROM carrier_registry WHERE group_id=? AND carrier_id=?').bind(player.group_id,e.carrier).first()))fail('Choose a configured carrier or Either carrier');
          carrier='inbound:'+e.carrier;kind='withdraw';
        } else if(e.kind==='cap')carrier=e.carrier==='inbound'?'inbound':'outbound';
        const at=new Date(e.timestamp).toISOString(),payload=JSON.stringify(cargo);
        await db.prepare("INSERT INTO transit_events(group_id,request_id,player_id,kind,carrier_id,commodity,amount,cargo,observed_at) VALUES(?,?,?,?,?,?,?,?,?) ON CONFLICT(group_id,request_id) DO NOTHING").bind(player.group_id,b.requestId,player.id,kind,carrier,commodity,amount,payload,at).run();
        const saved=await db.prepare('SELECT * FROM transit_events WHERE group_id=? AND request_id=?').bind(player.group_id,b.requestId).first();
        if(saved.player_id!==player.id||saved.kind!==kind||(saved.carrier_id!==carrier&&!(e.kind==='cap'&&carrier==='outbound'&&saved.carrier_id===''))||saved.commodity!==commodity||saved.amount!==amount||saved.cargo!==payload||saved.observed_at!==at)fail('Transit event ID collision',409);
        return json({ok:true});
      }
      if (url.pathname === '/v2/inventory' && request.method === 'GET') {
        // Read indexed high-water marks before loading the snapshot. Concurrent writes
        // remain visible on the next poll even if this response includes newer data.
        const marks=await db.prepare(`SELECT
          (SELECT COALESCE(MAX(sequence),0) FROM carrier_ledger WHERE group_id=?) AS stock,
          (SELECT COALESCE(MAX(sequence),0) FROM transit_events WHERE group_id=?) AS transit,
          (SELECT COALESCE(MAX(sequence),0) FROM ledger WHERE group_id=?) AS legacy,
          (SELECT COALESCE(MAX(version),0) FROM carrier_registry_versions WHERE group_id=?) AS registry`)
          .bind(player.group_id,player.group_id,player.group_id,player.group_id).first();
        const revision=await hash(JSON.stringify([player.group_id,player.id,player.name,player.role,marks]));
        if(url.searchParams.get('since')===revision)
          return json({schema:2,unchanged:true,revision});
        const result=await db.batch([
          db.prepare("SELECT carrier_id,commodity,quantity,version,reconciled_at FROM carrier_inventory WHERE group_id=? UNION ALL SELECT 'legacy' AS carrier_id,commodity,quantity,version,'' AS reconciled_at FROM inventory WHERE group_id=? ORDER BY carrier_id,commodity").bind(player.group_id,player.group_id),
          db.prepare('SELECT COALESCE(MAX(sequence),0) AS cursor FROM carrier_ledger WHERE group_id=?').bind(player.group_id),
          db.prepare('SELECT * FROM transit_events WHERE group_id=? ORDER BY julianday(observed_at),sequence').bind(player.group_id),
          db.prepare('SELECT carrier_id AS id,name,role,callsign,last_seen AS lastSeen,verified FROM carrier_registry WHERE group_id=? ORDER BY role DESC').bind(player.group_id)
        ]);
        const events=result[2],supply=transitBalances(events.results);
        return json({schema:2,revision,carriers:result[3].results.map(x=>({...x,verified:!!x.verified})),registryVersion:marks.registry,groupId:player.group_id,player:player.name,role:player.role,inventory:result[0].results,transit:supply.transit,inbound:supply.inbound,cursor:result[1].results[0].cursor});
      }
      if(url.pathname === '/v2/ledger' && request.method === 'GET') {
        const after=Number(url.searchParams.get('after')||0);
        if(!Number.isSafeInteger(after)||after<0) fail('Invalid ledger cursor');
        const rows=await db.prepare('SELECT l.*,p.name AS player FROM carrier_ledger l JOIN players p ON p.id=l.player_id WHERE l.group_id=? AND l.sequence>? ORDER BY l.sequence LIMIT 200').bind(player.group_id,after).all();
        return json({transactions:rows.results});
      }
      if(url.pathname === '/v2/transactions' && request.method === 'POST') {
        const b=await body(request);
        if(!identifier(b.requestId)||typeof b.commodity!=='string'||!/^[a-z0-9_]{1,80}$/.test(b.commodity)||!['add','remove','set','migrate','colonize'].includes(b.operation)||!Number.isSafeInteger(b.amount)||b.amount<0||b.amount>1000000000||!['Manual','Market','Transfer','Collect','Colonize'].includes(b.source)||!(b.carrierId==='legacy'||/^[1-9][0-9]{0,19}$/.test(b.carrierId))) fail('Invalid carrier transaction');
        if(b.carrierId!=='legacy' && !await db.prepare('SELECT carrier_id FROM carrier_registry WHERE group_id=? AND carrier_id=?').bind(player.group_id,b.carrierId).first()) fail('Carrier is not configured for this shared group. Ask the admin to configure it.',409);
        const automatic=b.source==='Market'||b.source==='Transfer';
        if(automatic && (b.carrierId==='legacy'||!['add','remove'].includes(b.operation)||typeof b.observedAt!=='string'||!/^\d{4}-\d\d-\d\dT/.test(b.observedAt)||!Number.isFinite(Date.parse(b.observedAt)))) fail('Automatic movement requires a carrier and valid journal timestamp');
        if(['Collect','Colonize'].includes(b.source) && b.carrierId!=='legacy') fail('Old queued tracking operations must stay in legacy stock');
        if(b.operation==='colonize' && b.carrierId!=='legacy') fail('Legacy operation');
        if(['set','migrate'].includes(b.operation)&&player.role!=='admin') fail('Only admins can reconcile or migrate stock',403);
        if(['set','migrate'].includes(b.operation)&&(!Number.isSafeInteger(b.expectedVersion)||b.expectedVersion<0)) fail('Current version required');
        b.observedAt=automatic?new Date(b.observedAt).toISOString():'';
        const matches=row=>row.player_id===player.id&&row.carrier_id===b.carrierId&&row.commodity===b.commodity&&row.operation===b.operation&&row.amount===b.amount&&row.source===b.source&&row.observed_at===b.observedAt;
        const find=()=>db.prepare('SELECT * FROM carrier_ledger WHERE group_id=? AND request_id=?').bind(player.group_id,b.requestId).first();
        // A preserved v1 outbox may have lost its acknowledgment before upgrade.
        // Recognise the old receipt before applying anything to legacy stock again.
        if(b.carrierId==='legacy') {
          const old=await db.prepare('SELECT * FROM ledger WHERE group_id=? AND request_id=?').bind(player.group_id,b.requestId).first();
          if(old) {
            if(old.player_id!==player.id || old.commodity!==b.commodity || old.operation!==b.operation || old.amount!==b.amount || old.source!==b.source) fail('Old request ID collision',409);
            return json({transaction:old,duplicate:true});
          }
        }
        const existing=await find();
        if(existing) { if(!matches(existing)) fail('Event ID reused for different movement',409); return json({transaction:existing,duplicate:true}); }
        const now=new Date().toISOString();
        if(b.operation==='migrate') {
          if(b.carrierId==='legacy'||b.source!=='Manual') fail('Select destination carrier');
          // Move legacy stock and credit the destination in a single D1 batch. Old ledger history remains intact.
          const oldId='v2_'+b.requestId;
          await db.batch([
            db.prepare('INSERT OR IGNORE INTO carrier_groups(group_id) VALUES(?)').bind(player.group_id),
            db.prepare('INSERT OR IGNORE INTO carrier_inventory(group_id,carrier_id,commodity) VALUES(?,?,?)').bind(player.group_id,b.carrierId,b.commodity),
            db.prepare("INSERT INTO ledger(group_id,request_id,player_id,commodity,operation,amount,source,before_balance,after_balance) SELECT group_id,?,?,commodity,'remove',?,'Manual',quantity,quantity-? FROM inventory WHERE group_id=? AND commodity=? AND quantity>=? AND version=? ON CONFLICT(group_id,request_id) DO NOTHING").bind(oldId,player.id,b.amount,b.amount,player.group_id,b.commodity,b.amount,b.expectedVersion),
            db.prepare("INSERT INTO carrier_ledger(group_id,request_id,player_id,carrier_id,commodity,operation,amount,source,observed_at,before_balance,after_balance,reconciled_at) SELECT group_id,?,?,carrier_id,commodity,'migrate',?,'Manual','',quantity,quantity+?,'' FROM carrier_inventory WHERE group_id=? AND carrier_id=? AND commodity=? AND EXISTS(SELECT 1 FROM ledger WHERE group_id=? AND request_id=?) ON CONFLICT(group_id,request_id) DO NOTHING").bind(b.requestId,player.id,b.amount,b.amount,player.group_id,b.carrierId,b.commodity,player.group_id,oldId)
          ]);
        } else if(b.carrierId==='legacy') {
          if(automatic) fail('Register a real carrier');
          const expr={add:'quantity+?',remove:'quantity-?',set:'?',colonize:'MAX(0,quantity-?)'}[b.operation];
          const condition=b.operation==='remove'?' AND quantity>=?':b.operation==='set'?' AND version=?':'';
          const args=[b.requestId,player.id,b.operation,b.amount,b.source,b.amount,player.group_id,b.commodity];
          if(b.operation==='remove') args.push(b.amount); if(b.operation==='set') args.push(b.expectedVersion);
          const oldId='v2_'+b.requestId; args[0]=oldId;
          await db.batch([
            db.prepare('INSERT OR IGNORE INTO inventory(group_id,commodity) VALUES(?,?)').bind(player.group_id,b.commodity),
            db.prepare('INSERT INTO ledger(group_id,request_id,player_id,commodity,operation,amount,source,before_balance,after_balance) SELECT group_id,?,?,commodity,?,?,?,quantity,'+expr+' FROM inventory WHERE group_id=? AND commodity=?'+condition+' ON CONFLICT(group_id,request_id) DO NOTHING').bind(...args),
            db.prepare("INSERT INTO carrier_ledger(group_id,request_id,player_id,carrier_id,commodity,operation,amount,source,observed_at,before_balance,after_balance,reconciled_at) SELECT group_id,?,player_id,'legacy',commodity,operation,amount,source,'',before_balance,after_balance,'' FROM ledger WHERE group_id=? AND request_id=? ON CONFLICT(group_id,request_id) DO NOTHING").bind(b.requestId,player.group_id,oldId)
          ]);
        } else {
          const base=b.operation==='set'?'?':b.operation==='add'?'quantity+?':'quantity-?';
          const expr=automatic?"CASE WHEN reconciled_at<>'' AND julianday(?)<=julianday(reconciled_at) THEN quantity ELSE "+base+' END':base;
          const condition=!automatic&&b.operation==='remove'?' AND quantity>=?':b.operation==='set'?' AND version=?':'';
          const args=[b.requestId,player.id,b.operation,b.amount,b.source,b.observedAt,b.operation==='set'?now:''];
          if(automatic) args.push(b.observedAt); args.push(b.amount,player.group_id,b.carrierId,b.commodity);
          if(!automatic&&b.operation==='remove') args.push(b.amount); if(b.operation==='set') args.push(b.expectedVersion);
          await db.batch([
            db.prepare('INSERT OR IGNORE INTO carrier_groups(group_id) VALUES(?)').bind(player.group_id),
            db.prepare('INSERT OR IGNORE INTO carrier_inventory(group_id,carrier_id,commodity) VALUES(?,?,?)').bind(player.group_id,b.carrierId,b.commodity),
            db.prepare('INSERT INTO carrier_ledger(group_id,request_id,player_id,carrier_id,commodity,operation,amount,source,observed_at,reconciled_at,before_balance,after_balance) SELECT group_id,?,?,carrier_id,commodity,?,?,?,?,?,quantity,'+expr+' FROM carrier_inventory WHERE group_id=? AND carrier_id=? AND commodity=?'+condition+' ON CONFLICT(group_id,request_id) DO NOTHING').bind(...args)
          ]);
        }
        const saved=await find();
        if(!saved) fail('Stock changed or insufficient legacy stock; refresh and review the pending correction',409);
        if(!matches(saved)) fail('Event ID collision',409);
        return json({transaction:saved,discrepancy:saved.after_balance<0});
      }
      if(['/inventory','/transactions'].includes(url.pathname) && await db.prepare('SELECT group_id FROM carrier_groups WHERE group_id=?').bind(player.group_id).first()) fail('This group uses carrier tracking v2. Upgrade the application; existing stock is retained.',409);

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
