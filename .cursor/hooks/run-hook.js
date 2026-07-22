'use strict';

const fs = require('fs');
const {
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
    const timeout = setTimeout(() => {
      reject(new Error('Timed out reading hook stdin'));
    }, 10000);

    process.stdin.setEncoding('utf8');
    process.stdin.on('data', (chunk) => {
      raw += chunk;
    });
    process.stdin.on('end', () => {
      clearTimeout(timeout);
      resolve(raw);
    });
    process.stdin.on('error', (err) => {
      clearTimeout(timeout);
      reject(err);
    });
  });
}

async function main() {
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
