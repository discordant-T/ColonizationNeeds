import {readFileSync, mkdirSync, rmSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {join,dirname} from 'node:path';
import {webcrypto} from 'node:crypto';
import assert from 'node:assert/strict';
if(!globalThis.crypto) globalThis.crypto=webcrypto;
const root=dirname(fileURLToPath(import.meta.url));
const python=process.argv[2]; if(!python) throw Error('Pass the Python executable as the first argument');
const scratch=join(root,'.backend-test'); mkdirSync(scratch,{recursive:true});
const dbfile=join(scratch,'test.sqlite');
const bridge=payload=>{
 const r=spawnSync(python,[join(root,'sqlite_bridge.py'),dbfile],{input:JSON.stringify(payload),encoding:'utf8',windowsHide:true});
 const v=JSON.parse(r.stdout||'{}'); if(r.status||v.error) throw Error(v.error||r.stderr); return v.result;
};
class Statement {
 constructor(sql){this.sql=sql;this.args=[];}
 bind(...args){this.args=args;return this;}
 async all(){return bridge({queries:[this]})[0];}
 async first(){return (await this.all()).results[0]||null;}
 async run(){return this.all();}
}
const db={prepare:sql=>new Statement(sql),withSession(){return this;},async batch(queries){return bridge({queries});}};
const worker=(await import('data:text/javascript;base64,'+Buffer.from(readFileSync(join(root,'../backend/worker.js'),'utf8')).toString('base64'))).default;
const env={DB:db,OWNER_TOKEN:'local-test-owner-secret'};
let tests=0;
async function request(path,token,payload,status=200){
 const r=await worker.fetch(new Request('https://local.test'+path,{method:payload?'POST':'GET',headers:{Authorization:'Bearer '+token},body:payload?JSON.stringify(payload):undefined}),env);
 const v=await r.json(); assert.equal(r.status,status,JSON.stringify(v)); tests++; return v;
}
try {
 bridge({schema:readFileSync(join(root,'../backend/schema.sql'),'utf8')+'\n'+readFileSync(join(root,'../backend/schema-v2.sql'),'utf8')+'\n'+readFileSync(join(root,'../backend/schema-transit.sql'),'utf8')});
 const admin=(await request('/admin/players',env.OWNER_TOKEN,{groupId:'squad',name:'A',role:'admin'},201)).accessToken;
 const member=(await request('/admin/players',env.OWNER_TOKEN,{groupId:'squad',name:'B',role:'member'},201)).accessToken;
 const other=(await request('/admin/players',env.OWNER_TOKEN,{groupId:'other',name:'C',role:'admin'},201)).accessToken;
 assert.equal((await request('/health',admin)).schema,2);
 const registry={requestId:'registry-1',expectedVersion:0,carriers:[{id:'123',name:'Squadron',role:'Squadron',verified:true},{id:'456',name:'Personal',role:'Personal',verified:true}]};
 await request('/v2/carriers',admin,registry);
 await request('/v2/carriers',admin,registry);
 assert.equal((await request('/v2/carriers',member)).carriers.length,2);
 assert.equal((await request('/v2/carriers',other)).carriers.length,0);
 await request('/v2/carriers',member,{...registry,requestId:'member-edit',expectedVersion:1},403);
 await request('/v2/carriers',admin,{...registry,requestId:'stale-registry'},409);
 await request('/v2/carriers',admin,{...registry,requestId:'duplicate-carrier',expectedVersion:1,carriers:[registry.carriers[0],{...registry.carriers[1],id:'123'}]},400);
 await request('/transactions',admin,{requestId:'old',commodity:'steel',operation:'add',amount:100,source:'Collect'});
 const tx=(id,carrier,op,amount,source='Market',at='2026-10-13T01:00:00Z',extra={})=>({requestId:id,carrierId:carrier,commodity:'steel',operation:op,amount,source,observedAt:at,...extra});
 const stock=async(token=admin)=>(await request('/v2/inventory',token)).inventory;
 let rows=await stock(); assert.equal(rows.find(x=>x.carrier_id==='legacy').quantity,100);
 await request('/v2/transactions',admin,tx('migration','123','migrate',60,'Manual','',{expectedVersion:1}));
 await request('/v2/transactions',admin,tx('migration','123','migrate',60,'Manual','',{expectedVersion:1}));
 rows=await stock(); assert.equal(rows.find(x=>x.carrier_id==='123').quantity,60); assert.equal(rows.find(x=>x.carrier_id==='legacy').quantity,40);
 await request('/v2/transactions',admin,tx('migration2','456','migrate',40,'Manual','',{expectedVersion:2}));
 await request('/v2/transactions',member,tx('noadmin','456','migrate',1,'Manual','',{expectedVersion:3}),403);
 await request('/v2/transactions',member,tx('buy','123','remove',80));
 rows=await stock(); assert.equal(rows.find(x=>x.carrier_id==='123').quantity,-20);
 await request('/v2/transactions',admin,tx('late-sale','123','add',50,'Market','2026-10-13T00:59:00Z'));
 rows=await stock(); assert.equal(rows.find(x=>x.carrier_id==='123').quantity,30);
 await request('/v2/transactions',admin,tx('late-sale','123','add',50,'Market','2026-10-13T00:59:00Z'));
 await request('/v2/transactions',admin,tx('late-sale','456','add',50),409);
 await request('/v2/transactions',member,tx('direct-in','456','add',20,'Transfer'));
 await request('/v2/transactions',member,tx('direct-out','123','remove',10,'Transfer'));
 await request('/v2/carriers',admin,{requestId:'remove-used',expectedVersion:1,carriers:[registry.carriers[1]]},409);
 await request('/v2/carriers',admin,{...registry,requestId:'rename',expectedVersion:1,carriers:[{...registry.carriers[0],name:'Squadron renamed'},registry.carriers[1]]});
 assert((await request('/v2/carriers',member)).carriers.some(x=>x.name==='Squadron renamed'));
 await request('/v2/transactions',admin,tx('unknown-carrier','999','add',1),409);
 rows=await stock(); assert.equal(rows.filter(x=>x.carrier_id!=='legacy').reduce((a,x)=>a+x.quantity,0),80);
 const version=rows.find(x=>x.carrier_id==='123').version;
 await request('/v2/transactions',admin,tx('stale-set','123','set',9,'Manual','',{expectedVersion:version-1}),409);
 await request('/v2/transactions',admin,tx('correct-set','123','set',9,'Manual','',{expectedVersion:version}));
 await request('/v2/transactions',admin,tx('older-event','123','add',1000,'Market','2020-01-01T00:00:00Z'));
 rows=await stock(); assert.equal(rows.find(x=>x.carrier_id==='123').quantity,9);
 await request('/v2/transactions',admin,tx('newer-event','123','add',1,'Transfer',new Date(Date.now()+1000).toISOString()));
 rows=await stock(); assert.equal(rows.find(x=>x.carrier_id==='123').quantity,10);
 await request('/v2/transactions',admin,tx('manual-underflow','123','remove',99,'Manual',''),409);
 await request('/v2/transactions',member,tx('member-set','123','set',9,'Manual','',{expectedVersion:version}),403);
 await request('/v2/transactions',member,tx('invalid-timestamp','123','add',1,'Market',''),400);
 await request('/v2/transactions',member,tx('missing-carrier','legacy','add',1),400);
 assert.equal((await stock(other)).length,0);
 await request('/transactions',admin,{requestId:'old-client',commodity:'steel',operation:'add',amount:99,source:'Collect'},409);
 // Preserved old outbox changes can be applied explicitly to the legacy balance via v2.
 await request('/v2/transactions',admin,tx('old-outbox','legacy','add',7,'Collect',''));
 await request('/v2/transactions',admin,tx('old-outbox','legacy','add',7,'Collect',''));
 rows=await stock(); assert.equal(rows.find(x=>x.carrier_id==='legacy').quantity,7);
 await request('/v2/transactions',admin,tx('old','legacy','add',100,'Collect',''));
 rows=await stock(); assert.equal(rows.find(x=>x.carrier_id==='legacy').quantity,7);
 await request('/v2/transactions',admin,tx('bad-migration','456','migrate',8,'Manual','',{expectedVersion:4}),409);
 rows=await stock(); assert.equal(rows.find(x=>x.carrier_id==='legacy').quantity,7);
 const ledger=await request('/v2/ledger',admin); assert(ledger.transactions.length>8);
 await request('/v2/inventory','invalid',undefined,401);
 // Shared transit: exact requested example, independent players, return, losses and retries.
 const transit=async(token=admin)=>(await request('/v2/inventory',token)).transit;
 const titanium=(id,carrier,op,n,at,source='Market')=>({...tx(id,carrier,op,n,source,at),commodity:'titanium'});
 const consume=(id,n,at,kind='consume',cargo)=>({requestId:id,transit:{id,kind,commodity:kind==='consume'||kind==='cap'?'titanium':'',amount:n,timestamp:at,cargo}});
 await request('/v2/transactions',admin,titanium('transit-start','123','add',16260,'2026-10-14T01:00:00Z'));
 await request('/v2/transactions',member,titanium('transit-withdraw','123','remove',16260,'2026-10-14T01:01:00Z'));
 await request('/v2/transactions',member,titanium('transit-withdraw','123','remove',16260,'2026-10-14T01:01:00Z'));
 assert.equal((await transit()).find(x=>x.commodity==='titanium').quantity,16260);
 assert.equal((await stock()).find(x=>x.commodity==='titanium').quantity,0);
 await request('/v2/transit',admin,consume('other-player-delivery',1000,'2026-10-14T01:02:00Z'));
 assert.equal((await transit()).find(x=>x.commodity==='titanium').quantity,16260);
 await request('/v2/transit',member,consume('partial-delivery',260,'2026-10-14T01:03:00Z'));
 await request('/v2/transit',member,consume('partial-delivery',260,'2026-10-14T01:03:00Z'));
 assert.equal((await transit()).find(x=>x.commodity==='titanium').quantity,16000);
 await request('/v2/transit',member,consume('partial-delivery',261,'2026-10-14T01:03:00Z'),409);
 await request('/v2/transit',member,consume('full-delivery',16000,'2026-10-14T01:04:00Z'));
 assert(!((await transit()).some(x=>x.commodity==='titanium')));
 assert.equal((await stock()).find(x=>x.commodity==='titanium').quantity,0);
 await request('/v2/transactions',member,titanium('transfer-withdraw','123','remove',100,'2026-10-14T02:00:00Z','Transfer'));
 await request('/v2/transactions',member,titanium('transfer-deposit','456','add',100,'2026-10-14T02:01:00Z','Transfer'));
 assert(!((await transit()).some(x=>x.commodity==='titanium')));
 await request('/v2/transactions',member,titanium('withdraw-again','456','remove',100,'2026-10-14T02:02:00Z'));
 await request('/v2/transit',member,consume('cargo-cap',0,'2026-10-14T02:03:00Z','snapshot',{titanium:60}));
 assert.equal((await transit()).find(x=>x.commodity==='titanium').quantity,60);
 await request('/v2/transit',member,consume('cargo-external',0,'2026-10-14T02:04:00Z','snapshot',{titanium:500}));
 assert.equal((await transit()).find(x=>x.commodity==='titanium').quantity,60);
 await request('/v2/transit',member,consume('ship-loss',0,'2026-10-14T02:05:00Z','reset'));
 assert(!((await transit()).some(x=>x.commodity==='titanium')));
 // Delayed arrival is folded by observation time, not receipt time.
 await request('/v2/transit',member,consume('late-delivery',30,'2026-10-14T03:01:00Z'));
 await request('/v2/transactions',member,titanium('late-withdrawal','456','remove',50,'2026-10-14T03:00:00Z'));
 assert.equal((await transit()).find(x=>x.commodity==='titanium').quantity,20);
 assert.equal((await transit(other)).length,0);
 await request('/v2/transit',member,consume('manual-clear',0,'2026-10-14T03:02:00Z','cap'));
 assert(!((await transit()).some(x=>x.commodity==='titanium')));
 await request('/v2/transit',member,consume('invalid-snapshot',0,'2026-10-14T03:03:00Z','snapshot',{titanium:-1}),400);
 const inbound=(id,n,destination,at)=>({requestId:id,transit:{id,kind:'inbound',carrier:destination,commodity:'aluminium',amount:n,timestamp:at}});
 const supply=async()=>await request('/v2/inventory',admin);
 await request('/v2/transit',member,inbound('inbound-member',100,'123','2026-10-15T01:00:00Z'));
 await request('/v2/transit',member,inbound('inbound-member',100,'123','2026-10-15T01:00:00Z'));
 await request('/v2/transit',admin,inbound('inbound-admin',50,'any','2026-10-15T01:00:00Z'));
 let snapshot=await supply();assert.equal(snapshot.inbound.reduce((a,x)=>a+x.quantity,0),150);assert(!snapshot.transit.some(x=>x.commodity==='aluminium'));
 await request('/v2/transactions',member,{...tx('mixed-outbound','123','remove',40,'Market','2026-10-15T01:01:00Z'),commodity:'aluminium'});
 await request('/v2/transactions',member,{...tx('partial-inbound-deposit','123','add',60,'Market','2026-10-15T01:02:00Z'),commodity:'aluminium'});
 snapshot=await supply();assert.equal(snapshot.inbound.find(x=>x.carrier_id==='123').quantity,40);assert.equal(snapshot.transit.find(x=>x.commodity==='aluminium').quantity,40);
 await request('/v2/transit',member,{requestId:'mixed-delivery',transit:{id:'mixed-delivery',kind:'consume',commodity:'aluminium',amount:20,timestamp:'2026-10-15T01:03:00Z'}});
 snapshot=await supply();assert.equal(snapshot.transit.find(x=>x.commodity==='aluminium').quantity,20);assert.equal(snapshot.inbound.find(x=>x.carrier_id==='123').quantity,40);
 await request('/v2/transit',member,{requestId:'clear-inbound-only',transit:{id:'clear-inbound-only',kind:'cap',carrier:'inbound',commodity:'aluminium',amount:0,timestamp:'2026-10-15T01:04:00Z'}});
 snapshot=await supply();assert.equal(snapshot.transit.find(x=>x.commodity==='aluminium').quantity,20);assert.equal(snapshot.inbound.reduce((a,x)=>a+x.quantity,0),50);
 await request('/v2/transit',member,{requestId:'mixed-loss',transit:{id:'mixed-loss',kind:'reset',timestamp:'2026-10-15T01:05:00Z'}});
 snapshot=await supply();assert(!snapshot.transit.some(x=>x.commodity==='aluminium'));assert.equal(snapshot.inbound.reduce((a,x)=>a+x.quantity,0),50);
 await request('/v2/transit',member,inbound('bad-target',1,'999','2026-10-15T01:06:00Z'),400);
 await request('/v2/transit',member,inbound('inbound-member',101,'123','2026-10-15T01:00:00Z'),409);
 const player=await db.prepare("SELECT id FROM players WHERE name='B'").first();
 await db.prepare("INSERT INTO transit_events(group_id,request_id,player_id,kind,commodity,amount,observed_at) VALUES('squad','old-cap',?,'cap','steel',0,'2026-10-15T02:00:00.000Z')").bind(player.id).run();
 await request('/v2/transit',member,{requestId:'old-cap',transit:{id:'old-cap',kind:'cap',commodity:'steel',amount:0,timestamp:'2026-10-15T02:00:00Z'}});
 console.log(`Backend tests passed: ${tests} HTTP assertions plus balance, isolation, migration, retry and reconciliation checks.`);
} finally {rmSync(scratch,{recursive:true,force:true});}
