'use strict';

const fs = require('fs');
const {
  appendDebug,
  decideSubagentModelPolicy,
  parseHookInput,
  recordFailure,
  extractConversationId,
  extractSubagentType,
  extractModel,
} = require('./subagent-model-policy-lib.js');

function respond(payload, exitCode) {
  fs.writeSync(1, JSON.stringify(payload));
  process.exit(exitCode);
}

function readStdin() {
  return new Promise((resolve, reject) => {
    let raw = '';
    let settled = false;
    let poll = null;
    let timeout = null;

    const finish = (value, err) => {
      if (settled) return;
      settled = true;
      if (timeout) clearTimeout(timeout);
      if (poll) clearInterval(poll);
      if (err) reject(err);
      else resolve(value);
    };

    timeout = setTimeout(() => {
      // Prefer partial payload over hanging: some hosts never emit 'end'.
      if (raw.length > 0) finish(raw);
      else finish('', new Error('Timed out reading hook stdin'));
    }, 8000);

    process.stdin.setEncoding('utf8');
    process.stdin.on('data', (chunk) => {
      raw += chunk;
    });
    process.stdin.on('end', () => finish(raw));
    process.stdin.on('error', (err) => finish('', err));

    if (process.stdin.isTTY) {
      finish('');
      return;
    }

    // If stdin is already fully buffered without 'end', accept after a short quiet period.
    let lastLen = -1;
    let quietTicks = 0;
    poll = setInterval(() => {
      const len = raw.length;
      const readable =
        typeof process.stdin.readableLength === 'number'
          ? process.stdin.readableLength
          : 0;
      if (len > 0 && len === lastLen && readable === 0) {
        quietTicks += 1;
        if (quietTicks >= 3) finish(raw);
      } else {
        quietTicks = 0;
        lastLen = len;
      }
    }, 15);

    process.stdin.resume();
  });
}

async function main() {
  appendDebug({
    ts: new Date().toISOString(),
    phase: 'boot',
    cwd: process.cwd(),
    script: __filename,
    pid: process.pid,
    node: process.execPath,
  });

  try {
    const raw = await readStdin();
    const input = parseHookInput(raw);
    const hookEventName = String(
      input.hook_event_name || process.env.CURSOR_HOOK_EVENT || 'unknown'
    );

    if (hookEventName === 'postToolUseFailure') {
      const conversationId = extractConversationId(input);
      const subagentType = extractSubagentType(input);
      const modelRaw = extractModel(input, hookEventName);
      recordFailure(conversationId, subagentType, modelRaw);
      respond({}, 0);
      return;
    }

    const decision = decideSubagentModelPolicy(input, hookEventName);
    const exitCode = decision.permission === 'deny' ? 2 : 0;

    if (hookEventName === 'subagentStart') {
      respond(
        {
          permission: decision.permission,
          user_message: decision.user_message,
        },
        exitCode
      );
      return;
    }

    respond(
      {
        permission: decision.permission,
        user_message: decision.user_message,
        agent_message: decision.agent_message,
      },
      exitCode
    );
  } catch (err) {
    appendDebug({
      ts: new Date().toISOString(),
      phase: 'error',
      cwd: process.cwd(),
      script: __filename,
      error: String(err && err.message ? err.message : err),
    });
    respond(
      {
        permission: 'deny',
        user_message: `Subagent hook error: ${err.message}`,
        agent_message:
          'Subagent hook failed while validating the model. Do not spawn subagents; continue in the parent agent.',
      },
      2
    );
  }
}

main();
