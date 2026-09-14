import test from 'node:test';
import assert from 'node:assert/strict';
import './workflow.js';
const state = overrides => globalThis.designWorkflow({ connected:true, applied:true, ...overrides });
test('primary action follows preparation, editing, saving and application', () => {
  assert.equal(state({connected:false}).action, 'setup');
  assert.equal(state({dirty:true}).action, 'save');
  assert.equal(state({saving:true,dirty:true}).disabled, true);
  assert.deepEqual([state({applied:false}).action, state({applied:false}).text], ['apply', '可以交给 AI']);
  assert.deepEqual([state({applied:true}).action, state({applied:true}).label], ['apply', '复制给 AI']);
});
test('recovery and conflict take precedence over normal actions', () => {
  assert.equal(state({lost:true,dirty:true}).label, '恢复编辑');
  assert.equal(state({conflict:true,dirty:true}).action, 'conflict');
  assert.equal(state({failed:true,dirty:true}).label, '重试保存');
  assert.equal(state({saving:true,lost:true}).disabled, true);
});
