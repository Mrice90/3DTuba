import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,mkdirSync,rmdirSync} from 'node:fs';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {createFileKv} from '../kv-file.js';
import {createKv} from '../kv.js';

test('failed disk write publishes neither rating in memory or on disk',async()=>{
    const file=path.join(mkdtempSync(path.join(tmpdir(),'ranked-atomic-')),'ledger.json');
    const kv=createFileKv(file);await kv.put('winner','1200');await kv.put('loser','1200');
    mkdirSync(file+'.tmp');
    try {
        await assert.rejects(kv.batch([{key:'winner',value:'1220'},{key:'loser',value:'1180'},{key:'settled',value:'yes'}]));
        for(const store of [kv,createFileKv(file)]) {assert.equal(await store.get('winner'),'1200');assert.equal(await store.get('loser'),'1200');assert.equal(await store.get('settled'),null);}
    } finally {rmdirSync(file+'.tmp');}
    await kv.batch([{key:'winner',value:'1220'},{key:'loser',value:'1180'},{key:'settled',value:'yes'}]);
    const reloaded=createFileKv(file);assert.equal(await reloaded.get('winner'),'1220');assert.equal(await reloaded.get('loser'),'1180');assert.equal(await reloaded.get('settled'),'yes');
});
test('invalid batch operation cannot partially update memory or persistent stores',async()=>{
    const file=path.join(mkdtempSync(path.join(tmpdir(),'ranked-invalid-')),'ledger.json');
    for(const kv of [createKv(),createFileKv(file)]) {
        await kv.put('first','original');await assert.rejects(kv.batch([{key:'first',value:'changed'},{key:'bad',value:42}]));assert.equal(await kv.get('first'),'original');
    }
});
