'use strict';

const assert = require('assert');
const {
  ALLOWED_MODELS,
  decideSubagentModelPolicy,
  normalizeModel,
  recordFailure,
  hasRecordedFailure,
} = require('./subagent-model-policy-lib.js');

function testNormalize() {
  const cases = [
    ['cursor-grok-4.5-high', 'cursor-grok-4.5-high'],
    ['Cursor Grok 4.5 High', 'cursor-grok-4.5-high'],
    ['composer-2.5', 'composer-2.5'],
    ['composer-2.5[fast=false]', 'composer-2.5'],
    ['composer-2.5-fast', 'composer-2.5-fast'],
    ['composer-2.5[fast=true]', 'composer-2.5-fast'],
    ['cursor-grok-4.5-high-fast', 'cursor-grok-4.5-high-fast'],
    ['gpt-5.6-sol-high', null],
    ['claude-opus-4-8-thinking-xhigh', null],
  ];

  for (const [raw, expected] of cases) {
    const got = normalizeModel(raw).canonical;
    assert.strictEqual(got, expected, `normalizeModel(${JSON.stringify(raw)})`);
  }
}

function testAllowNonFast() {
  const input = {
    hook_event_name: 'preToolUse',
    tool_name: 'Task',
    conversation_id: 'conv-test-1',
    tool_input: {
      subagent_type: 'explore',
      model: 'composer-2.5',
      prompt: 'ping',
    },
  };
  const decision = decideSubagentModelPolicy(input, 'preToolUse');
  assert.strictEqual(decision.permission, 'allow');
}

function testDenyUnknownModel() {
  const input = {
    hook_event_name: 'preToolUse',
    tool_name: 'Task',
    conversation_id: 'conv-test-2',
    tool_input: {
      subagent_type: 'explore',
      model: 'gpt-5.6-sol-high',
    },
  };
  const decision = decideSubagentModelPolicy(input, 'preToolUse');
  assert.strictEqual(decision.permission, 'deny');
}

function testDenyFastWithoutFailure() {
  const input = {
    hook_event_name: 'preToolUse',
    tool_name: 'Task',
    conversation_id: 'conv-test-3',
    tool_input: {
      subagent_type: 'explore',
      model: 'composer-2.5-fast',
    },
  };
  const decision = decideSubagentModelPolicy(input, 'preToolUse');
  assert.strictEqual(decision.permission, 'deny');
  assert.match(decision.user_message, /non-fast/i);
}

function testAllowFastAfterFailure() {
  const conversationId = 'conv-test-4';
  recordFailure(conversationId, 'explore', 'composer-2.5');
  assert.strictEqual(
    hasRecordedFailure(conversationId, 'explore', 'composer-2.5'),
    true
  );

  const input = {
    hook_event_name: 'preToolUse',
    tool_name: 'Task',
    conversation_id: conversationId,
    tool_input: {
      subagent_type: 'explore',
      model: 'composer-2.5-fast',
    },
  };
  const decision = decideSubagentModelPolicy(input, 'preToolUse');
  assert.strictEqual(decision.permission, 'allow');
}

function main() {
  testNormalize();
  testAllowNonFast();
  testDenyUnknownModel();
  testDenyFastWithoutFailure();
  testAllowFastAfterFailure();

  console.log('OK: subagent model policy tests passed');
  console.log('Allowed canonical slugs:');
  for (const [slug, meta] of Object.entries(ALLOWED_MODELS)) {
    console.log(`  ${slug}  (${meta.display})`);
  }
}

main();
